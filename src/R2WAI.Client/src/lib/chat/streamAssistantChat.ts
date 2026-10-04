import { authFetch } from '../auth/authClient'
import type { Citation, ResponseCardData, ToolCallProgressEvent } from './types'
import { parseContentBlocks } from './types'

export interface StreamAssistantChatCallbacks {
  onChunk: (content: string) => void
  onCitations: (citations: Citation[]) => void
  onDone: () => void
  onError: (message: string) => void
  /** In-chat tool-call progress (Phase 2) — optional, ignored by callers that don't render it. */
  onToolCall?: (event: ToolCallProgressEvent) => void
  /** Structured response cards (Phase 3) — optional, ignored by callers that don't render them. */
  onContentBlocks?: (cards: ResponseCardData[]) => void
}

/**
 * SSE-based ephemeral assistant test-chat — POST /api/v1/assistants/{id}/chat/stream
 * (text/event-stream, named events: chunk/citations/done/error). This is a
 * distinct mechanism from useChatSession's SignalR-group-broadcast contract:
 * it doesn't persist to a Conversation and it DOES carry citations from the
 * assistant's knowledge base, which the persisted-conversation path doesn't.
 * Ported from ChatDialog.razor's StreamResponseAsync — same event contract,
 * same fallback-on-failure story (see chatWithAssistant below).
 */
export async function streamAssistantChat(
  assistantId: string,
  message: string,
  conversationId: string | undefined,
  callbacks: StreamAssistantChatCallbacks,
  signal?: AbortSignal,
): Promise<void> {
  const response = await authFetch(`/api/v1/assistants/${assistantId}/chat/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, conversationId }),
    signal,
  })

  if (!response.ok || !response.body) {
    callbacks.onError(`Request failed (${response.status})`)
    return
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let currentEventType: string | null = null

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })

    let newlineIndex = buffer.indexOf('\n')
    while (newlineIndex !== -1) {
      const line = buffer.slice(0, newlineIndex).replace(/\r$/, '')
      buffer = buffer.slice(newlineIndex + 1)

      if (line.startsWith('event: ')) {
        currentEventType = line.slice(7).trim()
        newlineIndex = buffer.indexOf('\n')
        continue
      }
      if (!line.startsWith('data: ')) {
        newlineIndex = buffer.indexOf('\n')
        continue
      }

      const data = line.slice(6)
      if (data === '[DONE]') return

      const eventType = currentEventType ?? 'chunk'
      currentEventType = null

      try {
        if (eventType === 'chunk') {
          const parsed = JSON.parse(data) as { content?: string }
          if (parsed.content) callbacks.onChunk(parsed.content)
        } else if (eventType === 'citations') {
          const parsed = JSON.parse(data) as { citations?: Citation[] }
          if (parsed.citations) callbacks.onCitations(parsed.citations)
        } else if (eventType === 'toolCallStarted') {
          const parsed = JSON.parse(data) as { toolName?: string }
          if (parsed.toolName) callbacks.onToolCall?.({ toolName: parsed.toolName, status: 'started' })
        } else if (eventType === 'toolCallCompleted') {
          const parsed = JSON.parse(data) as { toolName?: string; success?: boolean }
          if (parsed.toolName) callbacks.onToolCall?.({ toolName: parsed.toolName, status: 'completed', success: parsed.success })
        } else if (eventType === 'contentBlocks') {
          const parsed = JSON.parse(data) as { contentBlocks?: string }
          if (parsed.contentBlocks) callbacks.onContentBlocks?.(parseContentBlocks(parsed.contentBlocks))
        } else if (eventType === 'done') {
          callbacks.onDone()
          return
        } else if (eventType === 'error') {
          const parsed = JSON.parse(data) as { message?: string }
          callbacks.onError(parsed.message ?? 'Stream error')
          return
        }
      } catch {
        // malformed frame — skip, matches ChatDialog.razor's catch(JsonException){}
      }

      newlineIndex = buffer.indexOf('\n')
    }
  }

  callbacks.onDone()
}

export interface ChatWithAssistantResult {
  reply: string
  conversationId: string
  tokensUsed: number
  citations: Citation[]
}

/** Non-streaming fallback — POST /api/v1/assistants/{id}/chat. */
export async function chatWithAssistant(
  assistantId: string,
  message: string,
  conversationId?: string,
): Promise<ChatWithAssistantResult> {
  const response = await authFetch(`/api/v1/assistants/${assistantId}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, conversationId }),
  })
  if (!response.ok) throw new Error(`Request failed (${response.status})`)
  return (await response.json()) as ChatWithAssistantResult
}
