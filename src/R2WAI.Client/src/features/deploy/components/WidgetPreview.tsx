import { useState } from 'react'
import { Box, Chip, IconButton, Paper, Stack, TextField, Typography } from '@mui/material'
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded'
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import MicNoneRoundedIcon from '@mui/icons-material/MicNoneRounded'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import VolumeUpRoundedIcon from '@mui/icons-material/VolumeUpRounded'
import { WIDGET_DEFAULT_COLOR, type WidgetAppearanceConfig } from '../types'

interface WidgetPreviewProps {
  appearance: WidgetAppearanceConfig
  /** Persisted server-side copy the widget fetches from /public-info at load. */
  welcomeMessage: string | null
  suggestedQuestions: string[]
  voiceEnabled: boolean
  viewport: 'desktop' | 'mobile'
}

/**
 * A faithful in-page replica of the shipped widget (src/R2WAI.Widget/src/widget.ts),
 * driven by the configuration currently set in the panel beside it.
 *
 * It reproduces the bundle's real geometry, colours and copy so what the customer
 * previews is what lands on their site — including that the title falls back to "Chat"
 * and the accent falls back to the bundle's own #6d28d9 when the attribute is absent.
 *
 * Replies are simulated locally and the panel says so. The genuine
 * `POST /chatbots/{id}/chat` and `/chat/stream` endpoints are `[AllowAnonymous]` and
 * origin-checked against the chatbot's AllowedOrigins, which will not include the Studio
 * origin — so a preview that really called them would be rejected. Testing the live
 * agent end to end is a separate action on the Playground page.
 */
export function WidgetPreview({ appearance, welcomeMessage, suggestedQuestions, voiceEnabled, viewport }: WidgetPreviewProps) {
  const [open, setOpen] = useState(true)
  const [messages, setMessages] = useState<string[]>(() => (welcomeMessage ? [welcomeMessage] : []))
  const [draft, setDraft] = useState('')

  const color = appearance.color.trim() || WIDGET_DEFAULT_COLOR
  const title = appearance.title.trim() || 'Chat'
  const isRight = appearance.position === 'bottom-right'

  function send(text: string) {
    const trimmed = text.trim()
    if (!trimmed) return
    setMessages((prev) => [...prev, trimmed, 'This is a simulated reply. The preview does not call the live chat endpoint.'])
    setDraft('')
  }

  const shellWidth = viewport === 'mobile' ? 280 : 420
  // Keep the preview panel proportional to the production widget while fitting the simulated viewport.
  const panelWidth = Math.min(400, shellWidth - 24)

  return (
    <Paper
      variant="outlined"
      sx={{
        position: 'relative',
        height: 560,
        overflow: 'hidden',
        bgcolor: viewport === 'desktop' ? '#f1f5f9' : '#e2e8f0',
        backgroundImage:
          'linear-gradient(rgba(15,23,42,0.04) 1px, transparent 1px), linear-gradient(90deg, rgba(15,23,42,0.04) 1px, transparent 1px)',
        backgroundSize: '24px 24px',
      }}
    >
      <Stack
        sx={{ height: '100%', pointerEvents: 'none', opacity: 0.55, alignItems: 'center', justifyContent: 'center' }}
      >
        <Chip size="small" variant="outlined" label="Your website" />
      </Stack>

      {open && (
        <Box
          role="dialog"
          aria-label={`${title} widget preview`}
          sx={{
            position: 'absolute',
            bottom: 88,
            ...(isRight ? { right: 24 } : { left: 24 }),
            width: panelWidth,
            height: 460,
            maxHeight: '82%',
            bgcolor: '#fff',
            border: '1px solid rgba(15,23,42,.08)',
            borderRadius: '22px',
            boxShadow: '0 24px 70px rgba(15,23,42,.20), 0 4px 14px rgba(15,23,42,.08)',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            color: '#1a1a1a',
          }}
        >
          <Stack
            direction="row"
            sx={{
              alignItems: 'center',
              gap: 1.5,
              px: 2.25,
              py: 1.75,
              bgcolor: color,
              color: '#fff',
            }}
          >
            <Box sx={{ width: 38, height: 38, display: 'grid', placeItems: 'center', flex: '0 0 38px', borderRadius: '13px', bgcolor: 'rgba(255,255,255,.17)', border: '1px solid rgba(255,255,255,.24)' }}>
              <AutoAwesomeRoundedIcon fontSize="small" />
            </Box>
            <Box sx={{ minWidth: 0, flex: 1 }}>
              <Typography noWrap sx={{ fontSize: 15, lineHeight: 1.25, fontWeight: 650 }}>{title}</Typography>
              <Typography sx={{ mt: 0.4, color: 'rgba(255,255,255,.82)', fontSize: 11.5, lineHeight: 1 }}>AI assistant</Typography>
            </Box>
            <IconButton
              onClick={() => setOpen(false)}
              aria-label="Close chat"
              size="small"
              sx={{ width: 34, height: 34, flex: '0 0 34px', border: '1px solid rgba(255,255,255,.2)', borderRadius: '11px', color: '#fff', bgcolor: 'rgba(255,255,255,.12)', '&:hover': { bgcolor: 'rgba(255,255,255,.22)' } }}
            >
              <CloseRoundedIcon fontSize="small" />
            </IconButton>
          </Stack>

          <Box
            sx={{
              flex: 1,
              overflowY: 'auto',
              p: 2,
              display: 'flex',
              flexDirection: 'column',
              gap: 1.25,
            }}
          >
            {messages.length === 0 && (
              <Typography variant="caption" sx={{ color: '#6b7280' }}>
                No welcome message set.
              </Typography>
            )}
            {messages.map((m, i) => (
              <Box
                key={i}
                sx={{
                  maxWidth: '85%',
                  px: 1.75,
                  py: 1.35,
                  border: '1px solid',
                  borderColor: i % 2 === 0 ? '#eceef2' : 'transparent',
                  borderRadius: '17px',
                  fontSize: 13.5,
                  lineHeight: 1.52,
                  whiteSpace: 'pre-wrap',
                  overflowWrap: 'anywhere',
                  alignSelf: i % 2 === 0 ? 'flex-start' : 'flex-end',
                  borderBottomLeftRadius: i % 2 === 0 ? '6px' : '17px',
                  borderBottomRightRadius: i % 2 === 0 ? '17px' : '6px',
                  bgcolor: i % 2 === 0 ? '#f4f5f8' : color,
                  color: i % 2 === 0 ? '#1a1a1a' : '#fff',
                }}
              >
                {m}
              </Box>
            ))}
            {suggestedQuestions.length > 0 && messages.length <= 1 && (
              <Stack spacing={0.875} sx={{ alignSelf: 'flex-start', maxWidth: '94%', mt: 0.5 }}>
                <Typography sx={{ ml: 0.4, color: '#8b93a1', fontSize: 9.5, fontWeight: 700, letterSpacing: '.08em', textTransform: 'uppercase' }}>
                  Suggested questions
                </Typography>
                {suggestedQuestions.map((q) => (
                  <Box
                    component="button"
                    type="button"
                    key={q}
                    onClick={() => send(q)}
                    sx={{
                      bgcolor: '#fff',
                      color,
                      border: '1px solid #e3e6ec',
                      borderRadius: '14px',
                      px: 1.6,
                      py: 1,
                      fontSize: 12,
                      textAlign: 'left',
                      cursor: 'pointer',
                      fontFamily: 'inherit',
                      boxShadow: '0 1px 2px rgba(15,23,42,.025)',
                      transition: 'transform 130ms ease, border-color 130ms ease, background-color 130ms ease',
                      '&:hover': { bgcolor: `color-mix(in srgb, ${color} 6%, white)`, color, borderColor: `color-mix(in srgb, ${color} 35%, #e3e6ec)`, transform: 'translateY(-1px)' },
                    }}
                  >
                    {q}
                  </Box>
                ))}
              </Stack>
            )}
          </Box>

          <Box sx={{ px: 1.75, pt: 1.25, pb: 1, borderTop: '1px solid #eef0f4' }}>
            <Stack sx={{ border: '1px solid #dfe3ea', borderRadius: '17px', p: '9px 9px 8px 13px', boxShadow: '0 2px 8px rgba(15,23,42,.035)' }}>
            <TextField
              variant="standard"
              multiline
              minRows={1}
              maxRows={3}
              value={draft}
              placeholder="Type a message…"
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault()
                  send(draft)
                }
              }}
              sx={{ flex: 1, '& .MuiInputBase-root': { fontSize: 13.5, lineHeight: 1.5 }, '& .MuiInputBase-input': { py: '2px', color: '#202431' }, '& .MuiInputBase-input::placeholder': { color: '#98a0ae', opacity: 1 } }}
            />
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mt: 0.5 }}>
              <Stack direction="row" spacing={0.5}>
                <IconButton size="small" disabled aria-label="Attach a file (preview only)" sx={{ width: 34, height: 34, color: '#667085', borderRadius: '11px' }}><AttachFileRoundedIcon fontSize="small" /></IconButton>
                {voiceEnabled && <>
                  <IconButton size="small" disabled aria-label="Speak your message (preview only)" sx={{ width: 34, height: 34, color: '#667085', borderRadius: '11px' }}><MicNoneRoundedIcon fontSize="small" /></IconButton>
                  <IconButton size="small" disabled aria-label="Read replies aloud (preview only)" sx={{ width: 34, height: 34, color: '#667085', borderRadius: '11px' }}><VolumeUpRoundedIcon fontSize="small" /></IconButton>
                </>}
              </Stack>
              <Box
                component="button"
                type="button"
                onClick={() => send(draft)}
                disabled={!draft.trim()}
                aria-label="Send"
                sx={{ minWidth: 38, height: 38, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 0.75, px: 1.5, color: '#fff', bgcolor: color, border: 0, borderRadius: '12px', fontFamily: 'inherit', fontSize: 12, fontWeight: 650, cursor: draft.trim() ? 'pointer' : 'default', opacity: draft.trim() ? 1 : 0.48, boxShadow: `0 3px 8px color-mix(in srgb, ${color} 25%, transparent)`, '&:hover': { bgcolor: color, filter: 'brightness(1.04)' } }}
              >
                Send <SendRoundedIcon sx={{ fontSize: 16 }} />
              </Box>
            </Stack>
            </Stack>
            <Typography sx={{ pt: 1, color: '#a0a6b2', textAlign: 'center', fontSize: 10.5, lineHeight: 1.3 }}>
              Enter to send · Shift + Enter for a new line
            </Typography>
          </Box>
        </Box>
      )}

      <Box
        component="button"
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-label="Open chat"
        sx={{
          position: 'absolute',
          bottom: 24,
          ...(isRight ? { right: 24 } : { left: 24 }),
          width: 56,
          height: 56,
          borderRadius: '50%',
          bgcolor: color,
          color: '#fff',
          border: 'none',
          cursor: 'pointer',
          boxShadow: '0 4px 14px rgba(0,0,0,0.25)',
          fontSize: 22,
          zIndex: 2,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        💬
      </Box>
    </Paper>
  )
}
