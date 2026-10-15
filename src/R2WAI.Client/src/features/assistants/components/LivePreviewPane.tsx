import type { ReactNode } from 'react'
import { useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { Box, Button, Chip, Paper, Stack, TextField, Typography } from '@mui/material'
import SmartToyOutlinedIcon from '@mui/icons-material/SmartToyOutlined'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import SendIcon from '@mui/icons-material/Send'
import StopIcon from '@mui/icons-material/Stop'
import ReplayIcon from '@mui/icons-material/Replay'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineRounded'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import { MarkdownRenderer } from '../../../components/MarkdownRenderer'
import { ResponseCard } from '../../../components/ResponseCard'
import { Timeline } from '../../../components/Timeline'
import { VoiceOrb } from '../../../components/VoiceOrb'
import { ChatTypingIndicator } from '../../../components/ChatTypingIndicator'
import { ChatResponseActions } from '../../../components/ChatResponseActions'
import { chatWithAssistant, streamAssistantChat } from '../../../lib/chat/streamAssistantChat'
import { reduceToolCallSteps } from '../../../lib/chat/toolCallProgress'
import type { Citation, ResponseCardData, ToolCallProgressEvent } from '../../../lib/chat/types'

interface PreviewMessage {
  isUser: boolean
  content: string
  interrupted?: boolean
  citations?: Citation[]
  cards?: ResponseCardData[]
}

interface LivePreviewPaneProps {
  assistantId: string
  /**
   * Fires whenever this turn's live tool-call events change — lets a host page
   * (Playground's Execution Inspector) mirror real-time progress without this
   * pane exposing its internal state shape. Optional; existing callers are unaffected.
   */
  onToolCallEvents?: (events: ToolCallProgressEvent[]) => void
  /** Header text; Assistant Studio calls it "Test your agent", as Copilot Studio does. */
  title?: string
  subtitle?: string
  /** Rendered at the end of the header row, e.g. a close button. */
  headerAction?: ReactNode
}

/**
 * Persistent live chat preview shared by Assistant Studio's right pane
 * (Phase 6) and the unified Playground's Assistant tab (Phase 6/11). Reuses
 * the same SSE mechanism as ChatDialog — deliberately NOT extracted from
 * ChatDialog itself, since ChatDialog is a modal with its own chrome/title
 * and this is an inline pane; both call the same lib/chat functions.
 *
 * Conversation state is local to this component instance — callers that let
 * the target assistant change (e.g. Playground's assistant picker) should
 * mount it with `key={assistantId}` so switching assistants remounts a fresh
 * conversation instead of carrying the old one over.
 */
export function LivePreviewPane({ assistantId, onToolCallEvents, title = 'Live preview', subtitle = 'Assistant conversation', headerAction }: LivePreviewPaneProps) {
  const [messages, setMessages] = useState<PreviewMessage[]>([])
  const [input, setInput] = useState('')
  const [streaming, setStreaming] = useState(false)
  const [streamBuffer, setStreamBuffer] = useState('')
  const [toolCallEvents, setToolCallEvents] = useState<ToolCallProgressEvent[]>([])
  const [retryMessage, setRetryMessage] = useState<string | null>(null)
  const conversationIdRef = useRef<string | undefined>(undefined)
  const activeControllerRef = useRef<AbortController | null>(null)
  const requestIdRef = useRef(0)
  const toolCallSteps = useMemo(() => reduceToolCallSteps(toolCallEvents), [toolCallEvents])

  useEffect(() => {
    onToolCallEvents?.(toolCallEvents)
  }, [toolCallEvents, onToolCallEvents])

  useEffect(() => () => {
    requestIdRef.current += 1
    activeControllerRef.current?.abort()
  }, [])

  async function sendMessage(userMsg: string) {
    if (!userMsg || streaming) return

    setMessages((prev) => [...prev, { isUser: true, content: userMsg }])
    setRetryMessage(null)
    setStreaming(true)
    setStreamBuffer('')
    setToolCallEvents([])

    const requestId = ++requestIdRef.current
    const controller = new AbortController()
    activeControllerRef.current = controller
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
          },
          onToolCall: (event) => setToolCallEvents((prev) => [...prev, event]),
          onContentBlocks: (cards) => {
            cardsSnapshot = cards
          },
          onDone: resolve,
          onError: (message) => reject(new Error(message)),
        }, controller.signal).catch(reject)
      })
      if (requestId !== requestIdRef.current) return
      setMessages((prev) => [...prev, { isUser: false, content: bufferSnapshot, citations: citationsSnapshot, cards: cardsSnapshot }])
    } catch (error) {
      if (requestId !== requestIdRef.current) return
      if (controller.signal.aborted || (error instanceof DOMException && error.name === 'AbortError')) {
        setMessages((prev) => [...prev, {
          isUser: false,
          content: bufferSnapshot || 'Response stopped.',
          citations: citationsSnapshot,
          cards: cardsSnapshot,
          interrupted: true,
        }])
        setRetryMessage(userMsg)
        return
      }
      if (bufferSnapshot) {
        setMessages((prev) => [...prev, { isUser: false, content: bufferSnapshot, citations: citationsSnapshot, cards: cardsSnapshot }])
        setRetryMessage(userMsg)
      } else {
        try {
          const result = await chatWithAssistant(assistantId, userMsg, conversationIdRef.current, controller.signal)
          if (requestId !== requestIdRef.current) return
          conversationIdRef.current = result.conversationId
          setMessages((prev) => [...prev, { isUser: false, content: result.reply, citations: result.citations }])
        } catch {
          if (requestId !== requestIdRef.current) return
          if (controller.signal.aborted) {
            setMessages((prev) => [...prev, { isUser: false, content: 'Response stopped.', interrupted: true }])
            setRetryMessage(userMsg)
            return
          }
          setMessages((prev) => [
            ...prev,
            { isUser: false, content: 'Sorry, something went wrong. Please try again.' },
          ])
          setRetryMessage(userMsg)
        }
      }
    } finally {
      if (requestId === requestIdRef.current) {
        activeControllerRef.current = null
        setStreaming(false)
        setStreamBuffer('')
        setToolCallEvents([])
      }
    }
  }

  function handleSend() {
    const userMsg = input.trim()
    if (!userMsg || streaming) return
    setInput('')
    void sendMessage(userMsg)
  }

  function handleRetry() {
    if (!retryMessage || streaming) return
    const messageToRetry = retryMessage
    setMessages((prev) => {
      const next = [...prev]
      if (next.at(-1)?.isUser === false) next.pop()
      if (next.at(-1)?.isUser && next.at(-1)?.content === messageToRetry) next.pop()
      return next
    })
    void sendMessage(messageToRetry)
  }

  function handleStop() {
    activeControllerRef.current?.abort()
  }

  function handleClear() {
    requestIdRef.current += 1
    activeControllerRef.current?.abort()
    activeControllerRef.current = null
    conversationIdRef.current = undefined
    setMessages([])
    setInput('')
    setRetryMessage(null)
    setStreaming(false)
    setStreamBuffer('')
    setToolCallEvents([])
  }

  function handleComposerKeyDown(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Enter' && !event.shiftKey && !streaming) {
      event.preventDefault()
      handleSend()
    }
  }

  return (
    <Stack sx={{ height: '100%' }}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ p: 1.75, alignItems: 'center', borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'background.default' }}
      >
        <Box sx={{ display: 'grid', placeItems: 'center', width: 34, height: 34, borderRadius: 1.5, bgcolor: 'action.selected', color: 'primary.main' }}>
          <SmartToyOutlinedIcon fontSize="small" />
        </Box>
        <Box sx={{ flexGrow: 1 }}>
          <Typography variant="subtitle2" component="h2" sx={{ fontWeight: 700 }}>{title}</Typography>
          <Typography variant="caption" color="text.secondary">{subtitle}</Typography>
        </Box>
        {streaming && <Chip label="Responding" size="small" color="info" variant="outlined" />}
        {headerAction}
      </Stack>

      <Box sx={{ flexGrow: 1, p: 2, overflowY: 'auto' }}>
        {messages.length === 0 && !streaming ? (
          <Box sx={{ minHeight: 260, height: '100%', display: 'grid', placeItems: 'center', textAlign: 'center', px: 2 }}>
            <Box sx={{ maxWidth: 360 }}>
              <Box sx={{ display: 'grid', placeItems: 'center', mx: 'auto', mb: 1.5, width: 54, height: 54, borderRadius: 2.5, bgcolor: 'action.selected', color: 'primary.main' }}>
                <SmartToyOutlinedIcon sx={{ fontSize: 28 }} />
              </Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 0.5 }}>Ready when you are</Typography>
              <Typography variant="body2" color="text.secondary">
                Send a message below to test the assistant. Replies and tool activity will appear here as they happen.
              </Typography>
            </Box>
          </Box>
        ) : (
          <Stack spacing={1.5}>
            {messages.map((msg, i) => (
              <Box key={i} className="r2wai-chat-message" sx={{ display: 'flex', justifyContent: msg.isUser ? 'flex-end' : 'flex-start' }}>
                <Paper
                  variant={msg.isUser ? 'elevation' : 'outlined'}
                  sx={{
                    p: 1.25,
                    maxWidth: '85%',
                    bgcolor: msg.isUser ? 'primary.main' : 'background.paper',
                    color: msg.isUser ? 'primary.contrastText' : 'text.primary',
                  }}
                >
                  {msg.isUser ? (
                    <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                      {msg.content}
                    </Typography>
                  ) : (
                    <>
                      <MarkdownRenderer content={msg.content} />
                      {msg.interrupted && (
                        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.75 }}>
                          Response stopped
                        </Typography>
                      )}
                    </>
                  )}
                  {!msg.isUser && msg.cards?.map((card, ci) => <ResponseCard key={ci} card={card} />)}
                  {!msg.isUser && <ChatResponseActions content={msg.content} />}
                  {!msg.isUser && msg.citations && msg.citations.length > 0 && (
                    <Box sx={{ mt: 1, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mb: 0.5 }}>
                        <MenuBookOutlined fontSize="inherit" color="info" />
                        <Typography variant="caption" color="info.main" sx={{ fontWeight: 700 }}>
                          Sources
                        </Typography>
                      </Stack>
                      {msg.citations.map((c) => (
                        <Typography key={c.index} variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                          [{c.index}] {c.sourceName}
                        </Typography>
                      ))}
                    </Box>
                  )}
                </Paper>
              </Box>
            ))}
            {streaming && (
              <Box className="r2wai-chat-message" sx={{ display: 'flex', justifyContent: 'flex-start' }}>
                <Paper variant="outlined" sx={{ p: 1.25, maxWidth: '85%' }}>
                  {toolCallSteps.length > 0 && (
                    <Box sx={{ mb: streamBuffer ? 1 : 0 }}>
                      <Timeline steps={toolCallSteps} dense />
                    </Box>
                  )}
                  {streamBuffer && <MarkdownRenderer content={streamBuffer} />}
                  {!streamBuffer && toolCallSteps.length === 0 && <ChatTypingIndicator />}
                </Paper>
              </Box>
            )}
          </Stack>
        )}
      </Box>

      <Box sx={{ p: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
        {(retryMessage || messages.length > 0) && (
          <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end', mb: 1 }}>
            {retryMessage && !streaming && (
              <Button size="small" startIcon={<ReplayIcon />} onClick={handleRetry}>
                Retry last message
              </Button>
            )}
            {messages.length > 0 && (
              <Button size="small" color="inherit" startIcon={<DeleteOutlineIcon />} onClick={handleClear}>
                Clear conversation
              </Button>
            )}
          </Stack>
        )}
        <Stack direction="row" spacing={1}>
          <TextField
            fullWidth
            size="small"
            placeholder="Ask something…"
            slotProps={{ htmlInput: { 'aria-label': 'Message' } }}
            value={input}
            disabled={streaming}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleComposerKeyDown}
          />
          <VoiceOrb
            disabled={streaming}
            onTranscript={(text) => setInput((prev) => (prev ? `${prev} ${text}` : text))}
            speakText={[...messages].reverse().find((m) => !m.isUser)?.content}
          />
          {streaming ? (
            <Button variant="outlined" color="error" startIcon={<StopIcon />} onClick={handleStop}>
              Stop
            </Button>
          ) : (
            <IconButton color="primary" aria-label="Send message" disabled={!input.trim()} onClick={handleSend}>
              <SendIcon />
            </IconButton>
          )}
        </Stack>
      </Box>
    </Stack>
  )
}
