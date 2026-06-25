using Microsoft.JSInterop;

namespace R2WAI.Web.Services;

public class TokenStorageService(IJSRuntime jsRuntime)
{
    private const string TokenKey = "r2wai_token";
    private const string RefreshTokenKey = "r2wai_refresh_token";
    private const string UserKey = "r2wai_user";

    public async Task SetTokenAsync(string token)
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetRefreshTokenAsync(string refreshToken)
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", RefreshTokenKey, refreshToken);
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", RefreshTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetUserAsync(string userInfo)
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", UserKey, userInfo);
    }

    public async Task<string?> GetUserAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", UserKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
            await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", RefreshTokenKey);
            await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", UserKey);
        }
        catch
        {
            // JS interop may fail during prerender
        }
    }
}
