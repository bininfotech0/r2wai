import { useEffect, useMemo, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { createChatHubConnection } from '../signalr/chatHub'
import { authFetch } from '../auth/authClient'
import { reduceToolCallSteps } from './toolCallProgress'
import type { ChatMessageResult, MessageDto, ToolCallProgressEvent } from './types'

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

/**
 * Per-instance chat session — mirrors Blazor's ChatSessionService contract:
 * join a conversation's SignalR group, send messages over HTTP (which
 * persists them and triggers server-side generation), and render the reply
 * as it streams in via the group's "StreamChunk"/"StreamComplete" broadcasts.
 * Once sendMessage() resolves, its result.content is the authoritative final
 * text — callers should render that (not the accumulated streamingText) and
 * call resetStreaming().
 */
export function useChatSession() {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const joinedConversationRef = useRef<string | null>(null)
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [streamingText, setStreamingText] = useState('')
  const [isStreaming, setIsStreaming] = useState(false)
  const [toolCallEvents, setToolCallEvents] = useState<ToolCallProgressEvent[]>([])
  // "StreamError" (server: SendMessageCommand's catch block) — the sender's own sendMessage()
  // promise already rejects/resolves-with-errorMessage via its HTTP response, but any OTHER
  // watcher of this conversation's group (a second tab, a supervisor view — see
  // IStreamingNotificationService's own doc comment) only ever hears about a failed stream
  // through this broadcast; without it, chunks just stop arriving with no explanation.
  const [streamError, setStreamError] = useState<string | null>(null)

  useEffect(() => {
    const connection = createChatHubConnection()
    connectionRef.current = connection

    connection.on('StreamChunk', (chunk: string) => {
      setIsStreaming(true)
      setStreamingText((prev) => prev + chunk)
    })
    connection.on('StreamComplete', () => {
      setIsStreaming(false)
    })
    connection.on('StreamError', (payload: { message: string }) => {
      setIsStreaming(false)
      setStreamError(payload.message)
    })
    connection.on('ToolCallStarted', (payload: { toolName: string }) => {
      setToolCallEvents((prev) => [...prev, { toolName: payload.toolName, status: 'started' }])
    })
    connection.on('ToolCallCompleted', (payload: { toolName: string; success: boolean }) => {
      setToolCallEvents((prev) => [...prev, { toolName: payload.toolName, status: 'completed', success: payload.success }])
    })

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => {
      setStatus('connected')
      const conversationId = joinedConversationRef.current
      joinedConversationRef.current = null
      if (conversationId) {
        connection
          .invoke('JoinConversation', conversationId)
          .then(() => {
            joinedConversationRef.current = conversationId
          })
          .catch(() => {
            // best effort, matches Blazor's rejoin-on-reconnect try/catch
          })
      }
    })
    connection.onclose(() => setStatus('disconnected'))

    connection
      .start()
      .then(() => setStatus('connected'))
      .catch(() => setStatus('disconnected'))

    return () => {
      void connection.stop()
    }
  }, [])

  async function joinConversation(conversationId: string) {
    const connection = connectionRef.current
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return
    if (joinedConversationRef.current === conversationId) return
    try {
      await connection.invoke('JoinConversation', conversationId)
      joinedConversationRef.current = conversationId
    } catch {
      // best effort — matches Blazor's try/catch-and-ignore
    }
  }

  function resetStreaming() {
    setStreamingText('')
    setIsStreaming(false)
    setToolCallEvents([])
    setStreamError(null)
  }

  async function sendMessage(conversationId: string, content: string): Promise<ChatMessageResult> {
    setStreamingText('')
    setToolCallEvents([])
    setStreamError(null)
    try {
      const formData = new FormData()
      formData.append('content', content)
      const response = await authFetch(`/api/v1/chat/conversations/${conversationId}/messages`, {
        method: 'POST',
        body: formData,
      })

      if (!response.ok) {
        return { success: false, errorMessage: `Request failed (${response.status}).` }
      }

      const result = (await response.json()) as MessageDto
      return { success: true, id: result.id, content: result.content, contentBlocks: result.contentBlocks, createdAt: result.createdAt }
    } catch (err) {
      return { success: false, errorMessage: err instanceof Error ? err.message : String(err) }
    } finally {
      setIsStreaming(false)
    }
  }

  const toolCallSteps = useMemo(() => reduceToolCallSteps(toolCallEvents), [toolCallEvents])

  return { status, streamingText, isStreaming, toolCallSteps, streamError, joinConversation, sendMessage, resetStreaming }
}
