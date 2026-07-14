using Microsoft.JSInterop;

namespace R2WAI.Web.Services;

/// <summary>
/// Wraps the wwwroot/js/voice.js Web Speech API interop so pages don't each
/// re-declare the same module-import/DotNetObjectReference/JSInvokable plumbing.
/// </summary>
public sealed class VoiceSession : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<VoiceSession>? _selfRef;

    public bool SttSupported { get; private set; }
    public bool TtsSupported { get; private set; }
    public bool IsListening { get; private set; }
    public string InterimTranscript { get; private set; } = string.Empty;

    public event Action<string, bool>? TranscriptReceived; // (transcript, isFinal)
    public event Action<string>? StatusChanged; // "listening" | "stopped" | "no-speech" | "not-allowed" | "error"
    public event Action? SpeakDone;

    public VoiceSession(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitAsync()
    {
        if (_module is not null) return;
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/voice.js");
        _selfRef = DotNetObjectReference.Create(this);
        var support = await _module.InvokeAsync<VoiceSupport>("isSupported");
        SttSupported = support.Stt;
        TtsSupported = support.Tts;
    }

    public async Task StartListening(string? lang = null)
    {
        if (_module is null || _selfRef is null) return;
        InterimTranscript = string.Empty;
        await _module.InvokeAsync<bool>("startListening", _selfRef, nameof(OnTranscript), nameof(OnStatus), lang ?? "en-US");
    }

    public async Task StopListening()
    {
        if (_module is null) return;
        IsListening = false;
        InterimTranscript = string.Empty;
        await _module.InvokeVoidAsync("stopListening");
    }

    public async Task<bool> Speak(string text, string? lang = null, double rate = 1.0, double pitch = 1.0)
    {
        if (_module is null || _selfRef is null) return false;
        return await _module.InvokeAsync<bool>("speak", text, lang ?? "en-US", rate, pitch, _selfRef, nameof(OnSpeakDoneCallback));
    }

    public async Task StopSpeaking()
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("stopSpeaking");
    }

    public async Task<VoiceOption[]> GetVoices()
    {
        if (_module is null) return [];
        return await _module.InvokeAsync<VoiceOption[]>("getVoices");
    }

    public async Task SetVoiceChatMode(bool enabled)
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("setVoiceChatMode", enabled);
    }

    [JSInvokable]
    public void OnTranscript(string transcript, bool isFinal)
    {
        if (isFinal)
        {
            InterimTranscript = string.Empty;
            IsListening = false;
        }
        else
        {
            InterimTranscript = transcript;
        }
        TranscriptReceived?.Invoke(transcript, isFinal);
    }

    [JSInvokable]
    public void OnStatus(string status)
    {
        IsListening = status == "listening";
        StatusChanged?.Invoke(status);
    }

    [JSInvokable]
    public void OnSpeakDoneCallback()
    {
        SpeakDone?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("dispose"); } catch { }
            await _module.DisposeAsync();
        }
        _selfRef?.Dispose();
    }

    private sealed class VoiceSupport
    {
        public bool Stt { get; set; }
        public bool Tts { get; set; }
    }

    public sealed class VoiceOption
    {
        public string Name { get; set; } = string.Empty;
        public string Lang { get; set; } = string.Empty;
        public bool IsLocal { get; set; }
    }
}
