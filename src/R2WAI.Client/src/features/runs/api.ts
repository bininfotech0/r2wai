import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson } from '../../lib/api/fetchJson'
import type { AuditLogDto, ConversationDto, MessageDto, PagedResult, RunsFiltersDto, RunsPageDto, WorkflowInstanceDto, WorkflowStepExecutionDto } from './types'

export interface RunsQuery {
  page: number
  pageSize: number
  type?: string
  status?: string
  applicationId?: string
  userId?: string
  assistantId?: string
  from?: string
  to?: string
  search?: string
}

export function listRuns(query: RunsQuery) {
  const params = new URLSearchParams({ page: String(query.page), pageSize: String(query.pageSize) })
  for (const key of ['type', 'status', 'applicationId', 'userId', 'assistantId', 'from', 'to', 'search'] as const) {
    const value = query[key]
    if (value) params.set(key, value)
  }
  return fetchJson<RunsPageDto>(`/runs?${params.toString()}`)
}

export function getRunsFilters() {
  return fetchJson<RunsFiltersDto>('/runs/filters')
}

export function listWorkflowInstances(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<PagedResult<WorkflowInstanceDto>>(`/workflows/instances?${params.toString()}`)
}

export function getWorkflowInstance(id: string) {
  return fetchJson<WorkflowInstanceDto>(`/workflows/instances/${id}`)
}

export function getWorkflowInstanceSteps(id: string) {
  return fetchJson<{ items: WorkflowStepExecutionDto[] }>(`/workflows/instances/${id}/steps`)
}

export async function retryFailedStep(instanceId: string): Promise<{ message: string }> {
  const response = await authFetch(`/api/v1/workflows/instances/${instanceId}/retry`, { method: 'POST' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to retry step')
  return (await response.json()) as { message: string }
}

export function listConversations(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<PagedResult<ConversationDto>>(`/chat/conversations?${params.toString()}`)
}

export function getConversation(id: string) {
  return fetchJson<ConversationDto>(`/chat/conversations/${id}`)
}

export function getMessages(id: string, pageSize = 50) {
  const params = new URLSearchParams({ page: '1', pageSize: String(pageSize) })
  return fetchJson<PagedResult<MessageDto>>(`/chat/conversations/${id}/messages?${params.toString()}`)
}

// No entityId filter exists server-side (GetAuditLogsQuery only supports
// entityType) — callers filter the returned page client-side for the exact
// entityId they want, same workaround this whole feature already applies
// elsewhere for missing backend filters.
export function listAuditLogsByEntityType(entityType: string, pageSize = 100) {
  const params = new URLSearchParams({ page: '1', pageSize: String(pageSize), entityType })
  return fetchJson<PagedResult<AuditLogDto>>(`/operations/audit-logs?${params.toString()}`)
}
