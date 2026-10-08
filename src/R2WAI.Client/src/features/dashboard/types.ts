// Mirrors R2WAI.Application.Features.Operations.DTOs.MetricsDto (camelCase over the wire).
export interface MetricsDto {
  totalWorkflows: number
  activeWorkflows: number
  totalDocuments: number
  totalKnowledgeBases: number
  totalAssistants: number
  totalConversations: number
  completedToday: number
  timestamp: string
  activeUsersByLogin: number
  workflowSuccessRatePercent: number | null
  applicationsTrendPercent: number | null
  assistantsTrendPercent: number | null
  conversationsTrendPercent: number | null
  activeUsersTrendPercent: number | null
  totalRequests: number
  successRate: number
  averageLatencyMs: number
  activeUsers: number
  apiErrors: number
  aiErrors: number
  workflowErrors: number
  aiRequests: number
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AssistantSummary {
  id: string
  name: string
  publishStatus: string
  createdAt: string
}

export interface WorkflowSummary {
  id: string
  name: string
  versionStatus: string
  isActive: boolean
  createdAt: string
  modifiedAt: string | null
}

export interface HealthStatus {
  status: string
  timestamp: string
}

export interface RecentWorkItem {
  id: string
  name: string
  type: 'Assistant' | 'Automation'
  status: string
  timestamp: string
}

// Mirrors R2WAI.Application.Features.Operations.DTOs.DailyTrendDto.
export interface DailyTrendPoint {
  date: string
  conversations: number
  runs: number
}

// Mirrors ...Operations.DTOs.AiStatsDto — a rolled-up window, not a per-day series.
export interface AiStatsDto {
  /** null when no message has a recorded token count (not measured yet, not zero usage). */
  totalTokens: number | null
  totalConversations: number
  /** null when no real user→reply timing sample exists in the window. */
  avgResponseTimeSec: number | null
  samplesUsed: number
  windowDays: number
}

// The subset of AssistantDto the dashboard needs to rank and count agents.
export interface AssistantUsageSummary {
  id: string
  name: string
  publishStatus: string
  usageCount: number
  createdAt: string
}
