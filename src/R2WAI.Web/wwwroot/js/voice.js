// R2WAI Voice Module — Web Speech API (STT + TTS)

let _recognition = null;
let _recognitionCallback = null;
let _statusCallback = null;
let _isListening = false;
let _voiceChatMode = false;
let _currentUtterance = null;

const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

export function isSupported() {
    return {
        stt: !!SpeechRecognition,
        tts: 'speechSynthesis' in window
    };
}

export function startListening(dotNetRef, callbackMethod, statusMethod, lang) {
    if (!SpeechRecognition) return false;
    if (_isListening) return true;

    _recognition = new SpeechRecognition();
    _recognition.lang = lang || 'en-US';
    _recognition.interimResults = true;
    _recognition.continuous = false;
    _recognition.maxAlternatives = 1;

    _recognitionCallback = callbackMethod;
    _statusCallback = statusMethod;

    _recognition.onstart = () => {
        _isListening = true;
        dotNetRef.invokeMethodAsync(statusMethod, 'listening');
    };

    _recognition.onresult = (event) => {
        let interim = '';
        let final = '';
        for (let i = event.resultIndex; i < event.results.length; i++) {
            const transcript = event.results[i][0].transcript;
            if (event.results[i].isFinal) {
                final += transcript;
            } else {
                interim += transcript;
            }
        }
        if (final) {
            dotNetRef.invokeMethodAsync(callbackMethod, final, true);
        } else if (interim) {
            dotNetRef.invokeMethodAsync(callbackMethod, interim, false);
        }
    };

    _recognition.onerror = (event) => {
        _isListening = false;
        const msg = event.error === 'no-speech' ? 'no-speech'
            : event.error === 'not-allowed' ? 'not-allowed'
            : 'error';
        dotNetRef.invokeMethodAsync(statusMethod, msg);
    };

    _recognition.onend = () => {
        const wasListening = _isListening;
        _isListening = false;
        if (_voiceChatMode && wasListening) {
            setTimeout(() => {
                if (_voiceChatMode && !window.speechSynthesis.speaking) {
                    startListening(dotNetRef, callbackMethod, statusMethod, lang);
                }
            }, 300);
        } else {
            dotNetRef.invokeMethodAsync(statusMethod, 'stopped');
        }
    };

    try {
        _recognition.start();
        return true;
    } catch {
        return false;
    }
}

export function stopListening() {
    _isListening = false;
    if (_recognition) {
        try { _recognition.stop(); } catch { }
        _recognition = null;
    }
}

export function speak(text, lang, rate, pitch, dotNetRef, doneMethod) {
    if (!('speechSynthesis' in window)) return false;

    stopSpeaking();

    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = lang || 'en-US';
    utterance.rate = rate || 1.0;
    utterance.pitch = pitch || 1.0;

    const voices = window.speechSynthesis.getVoices();
    const preferred = voices.find(v => v.lang.startsWith(utterance.lang) && v.localService)
        || voices.find(v => v.lang.startsWith(utterance.lang))
        || voices.find(v => v.default);
    if (preferred) utterance.voice = preferred;

    utterance.onend = () => {
        _currentUtterance = null;
        if (dotNetRef && doneMethod) {
            dotNetRef.invokeMethodAsync(doneMethod);
        }
    };

    utterance.onerror = () => {
        _currentUtterance = null;
        if (dotNetRef && doneMethod) {
            dotNetRef.invokeMethodAsync(doneMethod);
        }
    };

    _currentUtterance = utterance;
    window.speechSynthesis.speak(utterance);
    return true;
}

export function stopSpeaking() {
    if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
    }
    _currentUtterance = null;
}

export function isSpeaking() {
    return 'speechSynthesis' in window && window.speechSynthesis.speaking;
}

export function setVoiceChatMode(enabled) {
    _voiceChatMode = enabled;
    if (!enabled) {
        stopListening();
        stopSpeaking();
    }
}

export function getVoiceChatMode() {
    return _voiceChatMode;
}

export function getVoices() {
    if (!('speechSynthesis' in window)) return [];
    return window.speechSynthesis.getVoices().map(v => ({
        name: v.name,
        lang: v.lang,
        isLocal: v.localService
    }));
}

export function dispose() {
    _voiceChatMode = false;
    stopListening();
    stopSpeaking();
    _recognitionCallback = null;
    _statusCallback = null;
}
