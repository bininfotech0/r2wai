import { useRef, useState } from 'react'
import { Box, Chip, Drawer, Paper, Stack, TextField, Typography, useMediaQuery, useTheme } from '@mui/material'
import { TooltipIconButton as IconButton } from './TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'
import SendIcon from '@mui/icons-material/Send'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import { MarkdownRenderer } from './MarkdownRenderer'
import { ResponseCard } from './ResponseCard'
import { Timeline } from './Timeline'
import { VoiceOrb } from './VoiceOrb'
import { useChatSession } from '../lib/chat/useChatSession'
import { authFetch } from '../lib/auth/authClient'
import { parseContentBlocks } from '../lib/chat/types'
import type { ConversationDto, ResponseCardData } from '../lib/chat/types'

interface CopilotPanelProps {
  open: boolean
  onClose: () => void
}

interface CopilotMessage {
  isUser: boolean
  content: string
  isError?: boolean
  cards?: ResponseCardData[]
}

/**
 * Persisted-conversation Copilot chat — ported from CopilotPanel.razor.
 * Lazily creates a "copilot"-module Conversation on first send, then reuses
 * it. Streaming renders via useChatSession's SignalR group broadcast; once
 * the send POST resolves, its result.content replaces the streamed buffer
 * as the authoritative final text.
 */
export function CopilotPanel({ open, onClose }: CopilotPanelProps) {
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))
  const { status, streamingText, isStreaming, toolCallSteps, joinConversation, sendMessage, resetStreaming } = useChatSession()
  const [messages, setMessages] = useState<CopilotMessage[]>([])
  const [input, setInput] = useState('')
  const [isSending, setIsSending] = useState(false)
  const conversationIdRef = useRef<string | null>(null)

  async function ensureConversation(): Promise<string> {
    if (conversationIdRef.current) return conversationIdRef.current
    const response = await authFetch('/api/v1/chat/conversations', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Copilot', module: 'copilot' }),
    })
    if (!response.ok) throw new Error(`Failed to start conversation (${response.status})`)
    const created = (await response.json()) as ConversationDto
    conversationIdRef.current = created.id
    await joinConversation(created.id)
    return created.id
  }

  async function handleSend() {
    const content = input.trim()
    if (!content || isSending || isStreaming) return

    setMessages((prev) => [...prev, { isUser: true, content }])
    setInput('')
    setIsSending(true)

    try {
      const conversationId = await ensureConversation()
      const result = await sendMessage(conversationId, content)
      if (result.success) {
        setMessages((prev) => [...prev, { isUser: false, content: result.content, cards: parseContentBlocks(result.contentBlocks) }])
      } else {
        setMessages((prev) => [
          ...prev,
          { isUser: false, isError: true, content: result.errorMessage ?? 'Something went wrong.' },
        ])
      }
    } catch (err) {
      setMessages((prev) => [
        ...prev,
        { isUser: false, isError: true, content: err instanceof Error ? err.message : 'Something went wrong.' },
      ])
    } finally {
      resetStreaming()
      setIsSending(false)
    }
  }

  const busy = isSending || isStreaming

  return (
    <Drawer anchor="right" open={open} onClose={onClose} slotProps={{ paper: { sx: { width: isMobile ? '100%' : 360 } } }}>
      <Stack sx={{ height: '100%' }}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ p: 2, alignItems: 'center', borderBottom: '1px solid', borderColor: 'divider' }}
        >
          <AutoAwesomeIcon color="primary" fontSize="small" />
          <Typography variant="subtitle1" sx={{ flexGrow: 1, fontWeight: 600 }}>
            AI Copilot
          </Typography>
          {status !== 'connected' && (
            <Chip
              size="small"
              label={status === 'reconnecting' ? 'Reconnecting…' : status === 'connecting' ? 'Connecting…' : 'Offline'}
              color={status === 'reconnecting' ? 'warning' : 'default'}
              variant="outlined"
            />
          )}
          <IconButton size="small" onClick={onClose} aria-label="Close copilot">
            <CloseIcon fontSize="small" />
          </IconButton>
        </Stack>

        <Box sx={{ flexGrow: 1, p: 2, overflowY: 'auto' }}>
          {messages.length === 0 && !isStreaming ? (
            <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 4 }}>
              Ask R2WAI Studio anything — navigation, status, or how to do something.
            </Typography>
          ) : (
            <Stack spacing={1.5}>
              {messages.map((msg, i) => (
                <Box key={i} sx={{ display: 'flex', justifyContent: msg.isUser ? 'flex-end' : 'flex-start' }}>
                  <Paper
                    variant={msg.isUser ? 'elevation' : 'outlined'}
                    sx={{
                      p: 1.25,
                      maxWidth: '85%',
                      bgcolor: msg.isUser ? 'primary.main' : msg.isError ? 'error.main' : 'background.paper',
                      color: msg.isUser || msg.isError ? 'primary.contrastText' : 'text.primary',
                    }}
                  >
                    {msg.isUser || msg.isError ? (
                      <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                        {msg.content}
                      </Typography>
                    ) : (
                      <MarkdownRenderer content={msg.content} />
                    )}
                    {!msg.isUser && !msg.isError && msg.cards?.map((card, ci) => <ResponseCard key={ci} card={card} />)}
                  </Paper>
                </Box>
              ))}
              {isStreaming && (
                <Box sx={{ display: 'flex', justifyContent: 'flex-start' }}>
                  <Paper variant="outlined" sx={{ p: 1.25, maxWidth: '85%' }}>
                    {toolCallSteps.length > 0 && (
                      <Box sx={{ mb: streamingText ? 1 : 0 }}>
                        <Timeline steps={toolCallSteps} dense />
                      </Box>
                    )}
                    {streamingText && <MarkdownRenderer content={streamingText} />}
                    {!streamingText && toolCallSteps.length === 0 && <MarkdownRenderer content="…" />}
                  </Paper>
                </Box>
              )}
            </Stack>
          )}
        </Box>

        <Box sx={{ p: 2, borderTop: '1px solid', borderColor: 'divider' }}>
          <Stack direction="row" spacing={1}>
            <TextField
              fullWidth
              size="small"
              placeholder="Ask something…"
              value={input}
              disabled={busy}
              onChange={(e) => setInput(e.target.value)}
              onKeyUp={(e) => {
                if (e.key === 'Enter' && !busy) void handleSend()
              }}
            />
            <VoiceOrb
              disabled={busy}
              onTranscript={(text) => setInput((prev) => (prev ? `${prev} ${text}` : text))}
              speakText={[...messages].reverse().find((m) => !m.isUser && !m.isError)?.content}
            />
            <IconButton color="primary" aria-label="Send message" disabled={busy || !input.trim()} onClick={() => void handleSend()}>
              <SendIcon />
            </IconButton>
          </Stack>
        </Box>
      </Stack>
    </Drawer>
  )
}
