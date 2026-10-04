import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  CreateWorkflowInput,
  PagedResult,
  UpdateWorkflowInput,
  WorkflowDraft,
  WorkflowDto,
  WorkflowTemplate,
} from './types'

export function listWorkflows(page: number, pageSize: number, search: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<WorkflowDto>>(`/workflows?${params.toString()}`)
}

export function getWorkflow(id: string) {
  return fetchJson<WorkflowDto>(`/workflows/${id}`)
}

const postJson = sharedPostJson

export function createWorkflow(input: CreateWorkflowInput) {
  return postJson<WorkflowDto>('/workflows', input)
}

export async function updateWorkflow(id: string, input: UpdateWorkflowInput): Promise<WorkflowDto> {
  const response = await authFetch(`/api/v1/workflows/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update automation')
  return (await response.json()) as WorkflowDto
}

export async function deleteWorkflow(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/workflows/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete automation')
}

export function draftWorkflow(description: string) {
  return postJson<WorkflowDraft>('/workflows/draft', { description })
}

export function getWorkflowTemplates() {
  return fetchJson<{ items: WorkflowTemplate[] }>('/workflows/templates')
}

export function publishWorkflow(id: string) {
  return postJson<{ id: string; version: number; versionStatus: string }>(`/workflows/${id}/publish`, {})
}

export function unpublishWorkflow(id: string) {
  return postJson<{ id: string; version: number; versionStatus: string }>(`/workflows/${id}/unpublish`, {})
}

export function executeWorkflow(id: string, data?: string) {
  return postJson<{ instanceId: string; status: string; warning?: string }>(`/workflows/${id}/execute`, { data })
}
