import { useEffect, useRef, useState } from 'react'
import { IconButton, Stack, Tooltip } from '@mui/material'
import MicIcon from '@mui/icons-material/Mic'
import MicNoneOutlinedIcon from '@mui/icons-material/MicNoneOutlined'
import VolumeUpIcon from '@mui/icons-material/VolumeUp'
import VolumeOffOutlinedIcon from '@mui/icons-material/VolumeOffOutlined'
import { useVoice } from '../lib/voice/useVoice'

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
  const { sttSupported, ttsSupported, isListening, startListening, stopListening, speak, error } = useVoice(onTranscript)
  const [ttsEnabled, setTtsEnabled] = useState(false)
  const lastSpokenRef = useRef<string | undefined>(undefined)

  useEffect(() => {
    if (!ttsEnabled || !speakText || speakText === lastSpokenRef.current) return
    lastSpokenRef.current = speakText
    speak(speakText)
  }, [speakText, ttsEnabled, speak])

  if (!sttSupported && !ttsSupported) return null

  return (
    <Stack direction="row" spacing={0.5}>
      {sttSupported && (
        <Tooltip title={error ?? (isListening ? 'Listening… click to stop' : 'Speak your message')}>
          <span>
            <IconButton
              size="small"
              color={isListening ? 'error' : 'default'}
              disabled={disabled}
              aria-label={isListening ? 'Stop voice input' : 'Start voice input'}
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
            </IconButton>
          </span>
        </Tooltip>
      )}
      {ttsSupported && (
        <Tooltip title={ttsEnabled ? 'Reading replies aloud — click to mute' : 'Read replies aloud'}>
          <IconButton
            size="small"
            color={ttsEnabled ? 'primary' : 'default'}
            disabled={disabled}
            aria-label={ttsEnabled ? 'Mute spoken replies' : 'Read replies aloud'}
            aria-pressed={ttsEnabled}
            onClick={() => setTtsEnabled((prev) => !prev)}
          >
            {ttsEnabled ? <VolumeUpIcon fontSize="small" /> : <VolumeOffOutlinedIcon fontSize="small" />}
          </IconButton>
        </Tooltip>
      )}
    </Stack>
  )
}
