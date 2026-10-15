import { useCallback, useEffect, useRef, useState } from 'react'

// Native Web Speech API — no JS-interop bridge needed (unlike the Blazor
// port's wwwroot/js/voice.js + VoiceSession.cs DotNetObjectReference
// plumbing), React runs in the browser already.
type SpeechRecognitionCtor = new () => SpeechRecognitionLike

interface SpeechRecognitionLike extends EventTarget {
  lang: string
  continuous: boolean
  interimResults: boolean
  start(): void
  stop(): void
  onresult: ((event: SpeechRecognitionEventLike) => void) | null
  onerror: ((event: { error: string }) => void) | null
  onend: (() => void) | null
}

interface SpeechRecognitionEventLike {
  resultIndex: number
  results: ArrayLike<ArrayLike<{ transcript: string }> & { isFinal: boolean }>
}

function getSpeechRecognitionCtor(): SpeechRecognitionCtor | null {
  const w = window as unknown as {
    SpeechRecognition?: SpeechRecognitionCtor
    webkitSpeechRecognition?: SpeechRecognitionCtor
  }
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null
}

export interface UseVoiceResult {
  sttSupported: boolean
  ttsSupported: boolean
  isListening: boolean
  isSpeaking: boolean
  interimTranscript: string
  error: string | null
  startListening: (lang?: string) => void
  stopListening: () => void
  speak: (text: string, lang?: string) => void
  stopSpeaking: () => void
}

/** Speech-to-text + text-to-speech via the browser's native Web Speech API. */
export function useVoice(onFinalTranscript: (transcript: string) => void): UseVoiceResult {
  const Ctor = getSpeechRecognitionCtor()
  const sttSupported = Ctor !== null
  const ttsSupported = typeof window !== 'undefined' && 'speechSynthesis' in window

  const [isListening, setIsListening] = useState(false)
  const [isSpeaking, setIsSpeaking] = useState(false)
  const [interimTranscript, setInterimTranscript] = useState('')
  const [error, setError] = useState<string | null>(null)
  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const onFinalRef = useRef(onFinalTranscript)

  useEffect(() => {
    onFinalRef.current = onFinalTranscript
  }, [onFinalTranscript])

  useEffect(() => {
    return () => {
      recognitionRef.current?.stop()
      if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
        window.speechSynthesis.cancel()
      }
    }
  }, [])

  const startListening = useCallback(
    (lang = 'en-US') => {
      if (!Ctor) {
        setError('Speech recognition is not supported in this browser.')
        return
      }
      setError(null)
      setInterimTranscript('')
      const recognition = new Ctor()
      recognition.lang = lang
      recognition.continuous = false
      recognition.interimResults = true

      recognition.onresult = (event) => {
        let interim = ''
        for (let i = event.resultIndex; i < event.results.length; i++) {
          const result = event.results[i]
          const transcript = result[0]?.transcript ?? ''
          if (result.isFinal) {
            setInterimTranscript('')
            onFinalRef.current(transcript)
          } else {
            interim += transcript
          }
        }
        if (interim) setInterimTranscript(interim)
      }
      recognition.onerror = (event) => {
        setError(event.error === 'not-allowed' ? 'Microphone access denied.' : `Voice error: ${event.error}`)
        setIsListening(false)
      }
      recognition.onend = () => {
        setIsListening(false)
        setInterimTranscript('')
      }

      recognitionRef.current = recognition
      recognition.start()
      setIsListening(true)
    },
    [Ctor],
  )

  const stopListening = useCallback(() => {
    recognitionRef.current?.stop()
    setIsListening(false)
  }, [])

  const speak = useCallback(
    (text: string, lang = 'en-US') => {
      const spokenText = text
        .replace(/```[\s\S]*?```/g, 'Code block omitted.')
        .replace(/`([^`]+)`/g, '$1')
        .replace(/!\[([^\]]*)\]\([^)]+\)/g, '$1')
        .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
        .replace(/^\s{0,3}#{1,6}\s?/gm, '')
        .replace(/[*_~]/g, '')
        .trim()
      if (!ttsSupported || !spokenText) return
      window.speechSynthesis.cancel()
      const utterance = new SpeechSynthesisUtterance(spokenText)
      utterance.lang = lang
      utterance.onstart = () => setIsSpeaking(true)
      utterance.onend = () => setIsSpeaking(false)
      utterance.onerror = () => setIsSpeaking(false)
      window.speechSynthesis.speak(utterance)
    },
    [ttsSupported],
  )

  const stopSpeaking = useCallback(() => {
    if (ttsSupported) window.speechSynthesis.cancel()
    setIsSpeaking(false)
  }, [ttsSupported])

  return { sttSupported, ttsSupported, isListening, isSpeaking, interimTranscript, error, startListening, stopListening, speak, stopSpeaking }
}
