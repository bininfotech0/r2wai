import * as signalR from '@microsoft/signalr'
import { tokenStorage } from '../auth/tokenStorage'

/**
 * Creates a NEW connection to /hubs/chat — deliberately a factory, not a
 * shared singleton. Mirrors Blazor's ChatSessionService: each component
 * (CopilotPanel, a future Conversations page) tracks its own conversation
 * and must not share hub-connection/group state with another instance.
 *
 * This connection carries the "persisted conversation" streaming contract:
 * the caller POSTs the message over HTTP (which persists it and triggers
 * server-side generation), and the server broadcasts "StreamChunk"/
 * "StreamComplete" to the conversation's SignalR group as the reply is
 * generated — see SignalRStreamingService.cs and SendMessageCommandHandler.
 * No citations on this path (see ChatDialog's separate SSE mechanism for
 * that — src/lib/chat/streamAssistantChat.ts).
 */
export function createChatHubConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl('/hubs/chat', {
      accessTokenFactory: () => tokenStorage.getToken() ?? '',
    })
    .withAutomaticReconnect()
    .build()
}
