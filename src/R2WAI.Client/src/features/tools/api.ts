import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson } from '../../lib/api/fetchJson'
import type { CapabilityDto, CapabilityFormInput, PagedResult, TestConnectionResult, ToolVersionDto } from './types'

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await authFetch(`/api/v1${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    // Test failures return a structured { message } body (the real reason a connection test
    // or governance check failed) — surface that instead of a generic "POST failed" string.
    let message = `POST ${path} failed`
    try {
      const errBody = await response.clone().json()
      if (errBody && typeof errBody === 'object' && typeof (errBody.message ?? errBody.error) === 'string') {
        message = errBody.message ?? errBody.error
      }
    } catch {
      // Non-JSON error body — keep the generic message.
    }
    throw new ApiRequestError(response.status, message)
  }
  return (await response.json()) as T
}

export function listCapabilities(page: number, pageSize: number, search: string, applicationId?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  if (applicationId) params.set('applicationId', applicationId)
  return fetchJson<PagedResult<CapabilityDto>>(`/capabilities?${params.toString()}`)
}

export function getCapability(id: string) {
  return fetchJson<CapabilityDto>(`/capabilities/${id}`)
}

export function createCapability(input: CapabilityFormInput) {
  return postJson<CapabilityDto>('/capabilities', input)
}

export async function updateCapability(id: string, input: CapabilityFormInput): Promise<CapabilityDto> {
  const response = await authFetch(`/api/v1/capabilities/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id, ...input }),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update tool')
  return (await response.json()) as CapabilityDto
}

export function setCapabilityStatus(id: string, isActive: boolean) {
  return postJson<CapabilityDto>(`/capabilities/${id}/status`, { isActive })
}

export async function deleteCapability(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/capabilities/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete tool')
}

// Usage tab: real call/execution history via AiFunctionAuditFilter, which
// writes an audit row (Execute action, ToolDefinition entity, a status in
// Metadata) on every SK function invocation this tool governs — success,
// failure, and each denial reason. Filtered by entityId (a Phase 12 addition
// to GetAuditLogsQuery — previously only entityType was filterable).
export interface ToolUsageLogItem {
  id: string
  action: string
  userName: string | null
  metadata: string | null
  timestamp: string
}
export function listCapabilityUsage(capabilityId: string, pageSize = 50) {
  const params = new URLSearchParams({ page: '1', pageSize: String(pageSize), entityType: 'ToolDefinition', entityId: capabilityId })
  return fetchJson<PagedResult<ToolUsageLogItem>>(`/operations/audit-logs?${params.toString()}`)
}

// Capabilities and Integrations are the same ToolDefinition row under two different DTOs — this
// reuses IntegrationsController's /test action directly (DynamicToolExecutor → HttpTool, the exact
// path an AI agent's own tool call takes) rather than duplicating it under /capabilities.
export function testCapability(id: string) {
  return postJson<TestConnectionResult>(`/integrations/${id}/test`, {})
}

export function listCapabilityVersions(id: string) {
  return fetchJson<ToolVersionDto[]>(`/capabilities/${id}/versions`)
}

export function createCapabilityVersion(id: string, note: string, publish: boolean) {
  return postJson<ToolVersionDto>(`/capabilities/${id}/versions`, { note: note || undefined, publish })
}

export function rollbackCapabilityVersion(id: string, versionId: string) {
  return postJson<ToolVersionDto>(`/capabilities/${id}/versions/${versionId}/rollback`, {})
}
