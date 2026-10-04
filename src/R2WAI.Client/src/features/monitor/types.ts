// Mirrors R2WAI.Application.Features.Operations.DTOs.MetricsDto (camelCase).
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

export interface DailyTrendPoint {
  date: string
  conversations: number
  runs: number
}

export interface AiStatsDto {
  totalTokens: number
  totalConversations: number
  avgResponseTimeSec: number
  samplesUsed: number
  windowDays: number
}

export type ErrorLevel = 'Warning' | 'Error' | 'Critical'

export interface ErrorLogItem {
  level: ErrorLevel
  message: string
  timestamp: string
  source: string
  correlationId: string | null
  stackTrace: string | null
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

// /operations/reports items are computed anonymous objects, not a formal
// DTO — Stats' shape varies by report Type ("Compliance"|"Usage"|"Cost"|
// "Assistant"), so it's left loose rather than modeled per-variant.
export interface ReportSummary {
  id: string
  name: string
  type: string
  period: string
  generatedAt: string
  status: string
  stats: Record<string, unknown>
}

export interface UsageAnalyticsDto {
  period: string
  assistants: { total: number; active: number }
  conversations: { total: number; messages: number }
  workflows: { executions: number; completed: number; failed: number }
  approvals: { total: number; approved: number; pending: number }
  documents: { uploaded: number }
}

// Mirrors R2WAI.Application.Features.TestCases.DTOs.*
export interface TestCaseDto {
  id: string
  assistantId: string
  applicationId: string | null
  name: string
  question: string
  expectedResponseContains: string | null
  expectedCapabilityCalled: string | null
  isEnabled: boolean
  createdAt: string
  modifiedAt: string | null
}

export interface CreateTestCaseInput {
  assistantId: string
  name: string
  question: string
  expectedResponseContains?: string
  expectedCapabilityCalled?: string
}

export type UpdateTestCaseInput = Omit<CreateTestCaseInput, 'assistantId'>

export interface TestCaseResultDto {
  id: string
  testRunId: string
  testCaseId: string | null
  testCaseName: string
  question: string
  status: string
  actualResponse: string | null
  durationMs: number
  functionCallsJson: string | null
  errorMessage: string | null
  executedAt: string
}

export interface TestRunDto {
  id: string
  assistantId: string | null
  applicationId: string | null
  triggeredByUserId: string
  status: string
  startedAt: string
  completedAt: string | null
  passedCount: number
  failedCount: number
  warningCount: number
  results: TestCaseResultDto[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
