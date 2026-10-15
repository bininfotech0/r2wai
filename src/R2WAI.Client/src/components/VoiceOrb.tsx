import { useEffect, useRef, useState } from 'react'
import { Stack } from '@mui/material'
import MicIcon from '@mui/icons-material/Mic'
import MicNoneOutlinedIcon from '@mui/icons-material/MicNoneOutlined'
import VolumeUpIcon from '@mui/icons-material/VolumeUp'
import VolumeOffOutlinedIcon from '@mui/icons-material/VolumeOffOutlined'
import StopCircleOutlinedIcon from '@mui/icons-material/StopCircleOutlined'
import { useVoice } from '../lib/voice/useVoice'
import { TooltipIconButton } from './TooltipIconButton'

const TTS_PREFERENCE_KEY = 'r2wai_read_replies_aloud'

function readTtsPreference() {
  try {
    return typeof window !== 'undefined' && window.localStorage.getItem(TTS_PREFERENCE_KEY) === 'true'
  } catch {
    return false
  }
}

interface VoiceOrbProps {
  onTranscript: (text: string) => void
  disabled?: boolean
  /**
   * Latest completed assistant reply. When the speaker toggle is on, VoiceOrb speaks each new
   * value aloud via the browser's TTS (window.speechSynthesis) — off by default, opt-in per user.
   */
  speakText?: string
}

/** Mic button (speech-to-text) + an opt-in speaker toggle (text-to-speech) for the latest reply. */
export function VoiceOrb({ onTranscript, disabled, speakText }: VoiceOrbProps) {
  const { sttSupported, ttsSupported, isListening, isSpeaking, startListening, stopListening, speak, stopSpeaking, error } = useVoice(onTranscript)
  const [ttsEnabled, setTtsEnabled] = useState(readTtsPreference)
  const lastSpokenRef = useRef<string | undefined>(undefined)

  useEffect(() => {
    try {
      window.localStorage.setItem(TTS_PREFERENCE_KEY, String(ttsEnabled))
    } catch {
      // Voice output remains available for this session if storage is unavailable.
    }
  }, [ttsEnabled])

  useEffect(() => {
    if (!ttsEnabled) {
      lastSpokenRef.current = undefined
      stopSpeaking()
      return
    }
    if (!speakText || speakText === lastSpokenRef.current) return
    lastSpokenRef.current = speakText
    speak(speakText)
  }, [speakText, ttsEnabled, speak, stopSpeaking])

  if (!sttSupported && !ttsSupported) return null

  return (
    <Stack direction="row" spacing={0.5}>
      {sttSupported && (
        <TooltipIconButton
          size="small"
          color={isListening ? 'error' : 'default'}
          disabled={disabled}
          tooltip={error ?? (isListening ? 'Listening… click to stop' : 'Speak your message')}
          aria-label={isListening ? 'Stop voice input' : 'Start voice input'}
          aria-pressed={isListening}
          onClick={() => (isListening ? stopListening() : startListening())}
          sx={
            isListening
              ? {
                  animation: 'voice-orb-pulse 1.4s ease-in-out infinite',
                  '@keyframes voice-orb-pulse': {
                    '0%': { boxShadow: '0 0 0 0 rgba(220,38,38,0.4)' },
                    '70%': { boxShadow: '0 0 0 8px rgba(220,38,38,0)' },
                    '100%': { boxShadow: '0 0 0 0 rgba(220,38,38,0)' },
                  },
                }
              : undefined
          }
        >
          {isListening ? <MicIcon fontSize="small" /> : <MicNoneOutlinedIcon fontSize="small" />}
        </TooltipIconButton>
      )}
      {ttsSupported && (
        <TooltipIconButton
          size="small"
          color={ttsEnabled ? 'primary' : 'default'}
          disabled={disabled}
          tooltip={ttsEnabled ? 'Replies will be read aloud — click to mute' : 'Read replies aloud'}
          aria-label={ttsEnabled ? 'Mute spoken replies' : 'Read replies aloud'}
          aria-pressed={ttsEnabled}
          onClick={() => setTtsEnabled((prev) => !prev)}
        >
          {ttsEnabled ? <VolumeUpIcon fontSize="small" /> : <VolumeOffOutlinedIcon fontSize="small" />}
        </TooltipIconButton>
      )}
      {ttsSupported && isSpeaking && (
        <TooltipIconButton
          size="small"
          color="primary"
          tooltip="Stop spoken reply"
          aria-label="Stop spoken reply"
          onClick={stopSpeaking}
        >
          <StopCircleOutlinedIcon fontSize="small" />
        </TooltipIconButton>
      )}
    </Stack>
  )
}
