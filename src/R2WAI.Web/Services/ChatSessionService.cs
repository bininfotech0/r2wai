using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace R2WAI.Web.Services;

public class ChatMessageResult
{
    public bool Success { get; set; }
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Cancelled { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Wraps the SignalR chat hub connection and the send-message HTTP call shared by
/// Conversations.razor and CopilotPanel.razor. Not registered in DI — each component
/// creates its own instance (via `new`) since they track independent conversations
/// and must not share hub-connection/group state with each other.
/// </summary>
public class ChatSessionService(
    AuthenticatedHttpClient http,
    IConfiguration configuration,
    NavigationManager navigation,
    TokenStorageService tokenStorage) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private Guid? _joinedConversationId;

    public event Action<string>? OnChunk;
    public event Action? OnStreamComplete;

    public async Task ConnectAsync()
    {
        if (_hubConnection is not null) return;

        try
        {
            var apiBase = configuration["ApiPublicUrl"]
                ?? configuration["ApiBaseUrl"]
                ?? configuration["SignalR:HubUrl"]
                ?? navigation.BaseUri.TrimEnd('/');
            var token = await tokenStorage.GetTokenAsync();

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{apiBase}/hubs/chat", options =>
                {
                    if (!string.IsNullOrEmpty(token))
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<string>("StreamChunk", chunk => OnChunk?.Invoke(chunk));
            _hubConnection.On("StreamComplete", () => OnStreamComplete?.Invoke());

            _hubConnection.Reconnected += async _ =>
            {
                var rejoin = _joinedConversationId;
                _joinedConversationId = null;
                if (rejoin.HasValue) await JoinConversationAsync(rejoin.Value);
            };

            await _hubConnection.StartAsync();
        }
        catch
        {
            // Falls back to the plain "sending" spinner — the final reply still
            // arrives via the HTTP response once the assistant finishes.
        }
    }

    public async Task JoinConversationAsync(Guid conversationId)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected) return;
        if (_joinedConversationId == conversationId) return;

        try
        {
            await _hubConnection.InvokeAsync("JoinConversation", conversationId.ToString());
            _joinedConversationId = conversationId;
        }
        catch { }
    }

    public async Task<ChatMessageResult> SendMessageAsync(Guid conversationId, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            using var body = new MultipartFormDataContent { { new StringContent(content), "content" } };
            var response = await http.PostAsync($"/api/v1/chat/conversations/{conversationId}/messages", body, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new ChatMessageResult { Success = false, ErrorMessage = $"Request failed ({(int)response.StatusCode})." };

            var result = await response.Content.ReadFromJsonAsync<MessageReplyDto>(cancellationToken: cancellationToken);
            if (result is null)
                return new ChatMessageResult { Success = false, ErrorMessage = "Empty response from server." };

            return new ChatMessageResult { Success = true, Id = result.Id, Content = result.Content, CreatedAt = result.CreatedAt };
        }
        catch (OperationCanceledException)
        {
            // Cancelled==true covers both an explicit user Stop (cancellationToken.IsCancellationRequested)
            // and a request that simply timed out — callers distinguish the two via their own token.
            return new ChatMessageResult { Success = false, Cancelled = true };
        }
        catch (Exception ex)
        {
            return new ChatMessageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private class MessageReplyDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
