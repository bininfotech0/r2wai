using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace R2WAI.Web.Services;

public class AuthenticatedHttpClient
{
    private readonly HttpClient _http;
    private readonly CircuitTokenProvider _tokenProvider;
    private readonly TokenStorageService _tokenStorage;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public AuthenticatedHttpClient(IHttpClientFactory factory, CircuitTokenProvider tokenProvider, TokenStorageService tokenStorage)
    {
        _http = factory.CreateClient("R2WAI");
        _tokenProvider = tokenProvider;
        _tokenStorage = tokenStorage;
    }

    private async Task EnsureTokenAsync()
    {
        if (!string.IsNullOrEmpty(_tokenProvider.Token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.Token);
            return;
        }

        try
        {
            var token = await _tokenStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _tokenProvider.Token = token;
                _tokenProvider.RefreshToken = await _tokenStorage.GetRefreshTokenAsync();
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return;
            }
        }
        catch { }

        _http.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var accessToken = _tokenProvider.Token ?? await _tokenStorage.GetTokenAsync();
            var refreshToken = _tokenProvider.RefreshToken ?? await _tokenStorage.GetRefreshTokenAsync();

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return false;

            var savedAuth = _http.DefaultRequestHeaders.Authorization;
            _http.DefaultRequestHeaders.Authorization = null;

            try
            {
                var response = await _http.PostAsJsonAsync("/api/v1/auth/refresh", new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                });

                if (!response.IsSuccessStatusCode)
                    return false;

                var result = await response.Content.ReadFromJsonAsync<RefreshResponse>(_jsonOptions);
                if (result is null || string.IsNullOrEmpty(result.Token) || string.IsNullOrEmpty(result.RefreshToken))
                    return false;

                _tokenProvider.Token = result.Token;
                _tokenProvider.RefreshToken = result.RefreshToken;
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);

                try
                {
                    await _tokenStorage.SetTokenAsync(result.Token);
                    await _tokenStorage.SetRefreshTokenAsync(result.RefreshToken);
                }
                catch { }

                return true;
            }
            catch
            {
                _http.DefaultRequestHeaders.Authorization = savedAuth;
                return false;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public event Action? OnSessionExpired;

    private void NotifySessionExpired()
    {
        _tokenProvider.Token = null;
        _tokenProvider.RefreshToken = null;
        _ = Task.Run(async () => { try { await _tokenStorage.ClearAllAsync(); } catch { } });
        OnSessionExpired?.Invoke();
    }

    private async Task<HttpResponseMessage> HandleUnauthorizedAsync(Func<Task<HttpResponseMessage>> request, HttpResponseMessage failedResponse)
    {
        if (await TryRefreshTokenAsync())
        {
            var retryResponse = await request();
            if (retryResponse.StatusCode != HttpStatusCode.Unauthorized)
                return retryResponse;
        }

        NotifySessionExpired();
        return failedResponse;
    }

    public async Task<T?> GetFromJsonAsync<T>(string url)
    {
        await EnsureTokenAsync();
        try
        {
            return await _http.GetFromJsonAsync<T>(url, _jsonOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (await TryRefreshTokenAsync())
            {
                try { return await _http.GetFromJsonAsync<T>(url, _jsonOptions); }
                catch { }
            }
            NotifySessionExpired();
            return default;
        }
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T value)
    {
        await EnsureTokenAsync();
        var response = await _http.PostAsJsonAsync(url, value);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync(() => _http.PostAsJsonAsync(url, value), response);
        return response;
    }

    public async Task<HttpResponseMessage> PostAsync(string url, HttpContent? content)
    {
        await EnsureTokenAsync();
        var response = await _http.PostAsync(url, content);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync(() => _http.PostAsync(url, content), response);
        return response;
    }

    public async Task<HttpResponseMessage> PutAsJsonAsync<T>(string url, T value)
    {
        await EnsureTokenAsync();
        var response = await _http.PutAsJsonAsync(url, value);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync(() => _http.PutAsJsonAsync(url, value), response);
        return response;
    }

    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        await EnsureTokenAsync();
        var response = await _http.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync(() => _http.GetAsync(url), response);
        return response;
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        await EnsureTokenAsync();
        var response = await _http.DeleteAsync(url);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync(() => _http.DeleteAsync(url), response);
        return response;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption)
    {
        await EnsureTokenAsync();
        var response = await _http.SendAsync(request, completionOption);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var retry = await CloneRequestAsync(request);
            return await HandleUnauthorizedAsync(() => _http.SendAsync(retry, completionOption), response);
        }
        return response;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content is not null)
        {
            var content = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);
            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }

    private class RefreshResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
