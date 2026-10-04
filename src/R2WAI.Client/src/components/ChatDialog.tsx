import { useMemo, useRef, useState } from 'react'
import {
  Avatar,
  Box,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Paper,
  Stack,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import { TooltipIconButton as IconButton } from './TooltipIconButton'
import SmartToyIcon from '@mui/icons-material/SmartToy'
import SendIcon from '@mui/icons-material/Send'
import CloseIcon from '@mui/icons-material/Close'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import { MarkdownRenderer } from './MarkdownRenderer'
import { ResponseCard } from './ResponseCard'
import { Timeline } from './Timeline'
import { VoiceOrb } from './VoiceOrb'
import { chatWithAssistant, streamAssistantChat } from '../lib/chat/streamAssistantChat'
import { reduceToolCallSteps } from '../lib/chat/toolCallProgress'
import { useSnackbar } from '../lib/notifications/useSnackbar'
import type { Citation, ResponseCardData, ToolCallProgressEvent } from '../lib/chat/types'

interface ChatDialogMessage {
  isUser: boolean
  content: string
  citations?: Citation[]
  cards?: ResponseCardData[]
}

interface ChatDialogProps {
  open: boolean
  onClose: () => void
  assistantId: string
  assistantName: string
}

/**
 * Ephemeral assistant test-chat — ported from ChatDialog.razor. Uses the SSE
 * streamAssistantChat mechanism (citations included), falling back to the
 * plain non-streaming endpoint if the stream errors before any content
 * arrived; a partial stream that already produced content is kept and
 * committed as-is rather than discarded, matching the Blazor original.
 */
export function ChatDialog({ open, onClose, assistantId, assistantName }: ChatDialogProps) {
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))
  const { notify } = useSnackbar()
  const [messages, setMessages] = useState<ChatDialogMessage[]>([])
  const [input, setInput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [sending, setSending] = useState(false)
  const [streamBuffer, setStreamBuffer] = useState('')
  const [streamCitations, setStreamCitations] = useState<Citation[]>([])
  const [toolCallEvents, setToolCallEvents] = useState<ToolCallProgressEvent[]>([])
  const conversationIdRef = useRef<string | undefined>(undefined)
  const toolCallSteps = useMemo(() => reduceToolCallSteps(toolCallEvents), [toolCallEvents])

  const busy = streaming || sending

  async function handleSend() {
    const userMsg = input.trim()
    if (!userMsg || busy) return

    setMessages((prev) => [...prev, { isUser: true, content: userMsg }])
    setInput('')
    setStreaming(true)
    setStreamBuffer('')
    setStreamCitations([])
    setToolCallEvents([])

    let bufferSnapshot = ''
    let citationsSnapshot: Citation[] = []
    let cardsSnapshot: ResponseCardData[] = []

    try {
      await new Promise<void>((resolve, reject) => {
        void streamAssistantChat(assistantId, userMsg, conversationIdRef.current, {
          onChunk: (chunk) => {
            bufferSnapshot += chunk
            setStreamBuffer(bufferSnapshot)
          },
          onCitations: (citations) => {
            citationsSnapshot = citations
            setStreamCitations(citations)
          },
          onToolCall: (event) => setToolCallEvents((prev) => [...prev, event]),
          onContentBlocks: (cards) => {
            cardsSnapshot = cards
          },
          onDone: resolve,
          onError: (message) => reject(new Error(message)),
        }).catch(reject)
      })

      setMessages((prev) => [...prev, { isUser: false, content: bufferSnapshot, citations: citationsSnapshot, cards: cardsSnapshot }])
    } catch (err) {
      if (bufferSnapshot) {
        setMessages((prev) => [...prev, { isUser: false, content: bufferSnapshot, citations: citationsSnapshot, cards: cardsSnapshot }])
      } else {
        setSending(true)
        try {
          const result = await chatWithAssistant(assistantId, userMsg, conversationIdRef.current)
          conversationIdRef.current = result.conversationId
          setMessages((prev) => [...prev, { isUser: false, content: result.reply, citations: result.citations }])
        } catch {
          setMessages((prev) => [
            ...prev,
            { isUser: false, content: 'Sorry, something went wrong. Please try again.' },
          ])
        }
      }
      notify(`Streaming failed, used fallback: ${err instanceof Error ? err.message : String(err)}`, 'warning')
    } finally {
      setStreaming(false)
      setSending(false)
      setStreamBuffer('')
      setStreamCitations([])
      setToolCallEvents([])
    }
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth fullScreen={isMobile}>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Avatar sx={{ width: 32, height: 32, bgcolor: 'primary.main' }}>
          <SmartToyIcon fontSize="small" />
        </Avatar>
        <Typography variant="h6" sx={{ flexGrow: 1, fontWeight: 700 }}>
          Chat with {assistantName}
        </Typography>
        {streaming && <Chip label="streaming" size="small" color="primary" variant="outlined" />}
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <Box sx={{ maxHeight: '55vh', minHeight: 240, overflowY: 'auto', mb: 2 }}>
          <Stack spacing={1.5}>
            {messages.map((msg, i) => (
              <Box key={i} sx={{ display: 'flex', justifyContent: msg.isUser ? 'flex-end' : 'flex-start' }}>
                <Paper
                  variant={msg.isUser ? 'elevation' : 'outlined'}
                  sx={{
                    p: 1.5,
                    maxWidth: '80%',
                    bgcolor: msg.isUser ? 'primary.main' : 'background.paper',
                    color: msg.isUser ? 'primary.contrastText' : 'text.primary',
                  }}
                >
                  <Typography variant="caption" sx={{ opacity: 0.7 }}>
                    {msg.isUser ? 'You' : 'Assistant'}
                  </Typography>
                  {msg.isUser ? (
                    <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                      {msg.content}
                    </Typography>
                  ) : (
                    <MarkdownRenderer content={msg.content} />
                  )}
                  {!msg.isUser && msg.cards?.map((card, ci) => <ResponseCard key={ci} card={card} />)}
                  {!msg.isUser && msg.citations && msg.citations.length > 0 && (
                    <Box sx={{ mt: 1, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mb: 0.5 }}>
                        <MenuBookOutlined fontSize="inherit" color="info" />
                        <Typography variant="caption" color="info.main" sx={{ fontWeight: 700 }}>
                          Sources
                        </Typography>
                      </Stack>
                      {msg.citations.map((citation) => (
                        <Paper
                          key={citation.index}
                          variant="outlined"
                          sx={{ p: 1, mb: 0.5, borderLeft: '3px solid', borderLeftColor: 'info.main' }}
                        >
                          <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
                            <Typography variant="caption" sx={{ fontWeight: 600 }}>
                              [{citation.index}] {citation.sourceName}
                            </Typography>
                            <Chip label={`${Math.round(citation.score * 100)}% match`} size="small" />
                          </Stack>
                        </Paper>
                      ))}
                    </Box>
                  )}
                </Paper>
              </Box>
            ))}
            {streaming && (
              <Box sx={{ display: 'flex', justifyContent: 'flex-start' }}>
                <Paper variant="outlined" sx={{ p: 1.5, maxWidth: '80%' }}>
                  <Typography variant="caption" color="text.secondary">
                    Assistant
                  </Typography>
                  {toolCallSteps.length > 0 && (
                    <Box sx={{ mb: streamBuffer ? 1 : 0 }}>
                      <Timeline steps={toolCallSteps} dense />
                    </Box>
                  )}
                  {streamBuffer && <MarkdownRenderer content={streamBuffer} />}
                  {!streamBuffer && toolCallSteps.length === 0 && <MarkdownRenderer content="…" />}
                  {streamCitations.length > 0 && (
                    <Box sx={{ mt: 1, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                        <MenuBookOutlined fontSize="inherit" color="info" />
                        <Typography variant="caption" color="info.main" sx={{ fontWeight: 700 }}>
                          Sources
                        </Typography>
                      </Stack>
                    </Box>
                  )}
                </Paper>
              </Box>
            )}
          </Stack>
        </Box>
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
            speakText={[...messages].reverse().find((m) => !m.isUser)?.content}
          />
          <IconButton color="primary" aria-label="Send message" disabled={busy || !input.trim()} onClick={() => void handleSend()}>
            <SendIcon />
          </IconButton>
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
