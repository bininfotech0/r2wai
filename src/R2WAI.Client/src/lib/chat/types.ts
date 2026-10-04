// Mirrors R2WAI.Application.Features.Chat.DTOs.* (camelCase, string enums —
// the API registers JsonStringEnumConverter globally, see Program.cs).

export type MessageRole = 'User' | 'Assistant' | 'System'
export type MessageStatus = 'Sending' | 'Sent' | 'Completed' | 'Failed'

export interface MessageAttachmentDto {
  id: string
  fileName: string
  contentType: string
  fileSize: number
}

export interface MessageDto {
  id: string
  role: MessageRole
  content: string
  contentBlocks: string | null
  status: MessageStatus
  attachments: MessageAttachmentDto[]
  createdAt: string
}

export interface ConversationDto {
  id: string
  title: string
  module: string | null
  messageCount: number
  lastMessageAt: string | null
  createdAt: string
}

// Mirrors R2WAI.Application.Features.Assistants.Commands.CitationDto.
export interface Citation {
  sourceName: string
  content: string
  score: number
  index: number
}

export type ChatMessageResult =
  | { success: true; id: string; content: string; contentBlocks: string | null; createdAt: string }
  | { success: false; cancelled?: boolean; errorMessage?: string }

// In-chat "Checking {Capability Name}..." progress (redesign plan Phase 2). Carries a display
// name only — never a resolver class, endpoint, or arguments — mirroring the backend's
// ToolCallProgressEvent broadcast (SignalR "ToolCallStarted"/"ToolCallCompleted" events, or SSE
// "toolCallStarted"/"toolCallCompleted" named events, depending on the transport).
export interface ToolCallProgressEvent {
  toolName: string
  status: 'started' | 'completed'
  success?: boolean
}

// Structured chat response card (redesign plan Phase 3) — mirrors R2WAI.Application.Common.Models
// .ResponseCardDto. Only "status" and "table" have a real producer today (WorkflowPlugin); "summary"
// and "kpi" are defined so ResponseCard.tsx is ready for the next tool that needs them.
export type ResponseCardType = 'status' | 'table' | 'kpi' | 'summary'

export interface ResponseCardField {
  label: string
  value: string
}

export interface ResponseCardData {
  type: ResponseCardType
  title: string
  fields?: ResponseCardField[]
  columns?: string[]
  rows?: string[][]
}

/** Parses a MessageDto.contentBlocks JSON string into cards — never throws on malformed input. */
export function parseContentBlocks(contentBlocks: string | null | undefined): ResponseCardData[] {
  if (!contentBlocks) return []
  try {
    const parsed = JSON.parse(contentBlocks) as unknown
    return Array.isArray(parsed) ? (parsed as ResponseCardData[]) : []
  } catch {
    return []
  }
}
