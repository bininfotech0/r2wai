// Mirrors R2WAI.Application.Features.Workflows.DTOs.WorkflowInstanceDto (camelCase).
export type WorkflowInstanceStatus = 'Pending' | 'Running' | 'Suspended' | 'Completed' | 'Failed' | 'Cancelled'

export interface WorkflowInstanceDto {
  id: string
  workflowId: string
  workflowName: string
  // Backfilled server-side from the owning Workflow's ApplicationId — null when that
  // workflow isn't linked to a Connected System.
  applicationId: string | null
  applicationName: string | null
  initiatedByUserName: string | null
  status: WorkflowInstanceStatus
  currentStep: string | null
  data: string | null
  startedAt: string | null
  completedAt: string | null
  createdAt: string
}

export type WorkflowStepExecutionStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Skipped'

// GET /workflows/instances/{id}/steps projects an anonymous shape from
// WorkflowStepExecution — not the full entity (Variables is captured
// server-side but not returned).
export interface WorkflowStepExecutionDto {
  id: string
  stepIndex: number
  stepName: string
  stepType: string
  status: WorkflowStepExecutionStatus
  startedAt: string | null
  completedAt: string | null
  output: string | null
  error: string | null
}

// Mirrors R2WAI.Application.Features.Chat.DTOs.ConversationDto (camelCase).
// No token/model/duration/tool-call fields exist at this level — the
// Message entity has TokensUsed/ModelUsed columns, but the assistant-chat
// write path never populates them (ChatWithAssistantCommandHandler passes
// no modelUsed/tokensUsed to conversation.AddMessage), so an Assistant-type
// run's Technical Details will show those as unavailable, not fabricated.
export interface ConversationDto {
  id: string
  title: string | null
  module: string | null
  // Backfilled server-side from Conversation.ReferenceId, only resolved when module ===
  // "assistant" (the one convention the assistant-chat handler actually follows — ReferenceId
  // isn't a typed FK, so this is real but convention-based, not a guaranteed relationship).
  assistantName: string | null
  userName: string | null
  messageCount: number
  lastMessageAt: string | null
  createdAt: string
}

export type MessageRole = 'User' | 'Assistant' | 'System'

export interface MessageDto {
  id: string
  role: MessageRole
  content: string
  contentBlocks: string | null
  status: string | null
  attachments: string | null
  createdAt: string
}

// Mirrors R2WAI.Application.Features.Admin.DTOs.AuditLogDto (camelCase).
export interface AuditLogDto {
  id: string
  userId: string | null
  userName: string | null
  applicationId: string | null
  correlationId: string | null
  action: string
  entityType: string | null
  entityId: string | null
  oldValues: string | null
  newValues: string | null
  ipAddress: string | null
  timestamp: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// The unified GET /runs projection represents workflow executions and assistant conversations.
// Assistant rows are synthesized from conversation data; fields without a durable execution
// ledger (for example detailed tool steps or token usage) may be unavailable. TestRuns remain a
// separate QA regression feature surfaced from Monitor.
export type RunType = 'Automation' | 'Assistant'

export interface RunItem {
  id: string
  type: RunType
  name: string
  status: string
  startedAt: string
  completedAt: string | null
  applicationId: string | null
  applicationName: string | null
  linkedId: string | null
  linkedName: string | null
  userId: string | null
  userName: string | null
  correlationId: string | null
}

export interface RunFilterOption {
  id: string
  label: string
}

export interface RunsPageDto {
  items: RunItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface RunsFiltersDto {
  applications: RunFilterOption[]
  users: RunFilterOption[]
  assistants: RunFilterOption[]
}
