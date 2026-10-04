import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  AssistantDto,
  AssistantsPagedResult,
  CreateAssistantInput,
  GeneratedAssistantConfig,
  UpdateAssistantInput,
} from './types'
import type { AssistantSortKey, AssistantStatusFilter } from './assistantView'

export function listAssistants(
  page: number,
  pageSize: number,
  search: string,
  publishStatus?: AssistantStatusFilter,
  sortBy?: AssistantSortKey,
) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  if (publishStatus && publishStatus !== 'All') params.set('publishStatus', publishStatus)
  if (sortBy) params.set('sortBy', sortBy)
  return fetchJson<AssistantsPagedResult>(`/assistants?${params.toString()}`)
}

export function getAssistant(id: string) {
  return fetchJson<AssistantDto>(`/assistants/${id}`)
}

export interface AssistantPublishCheck {
  label: string
  ready: boolean
  detail?: string | null
}

export function getAssistantPublishReadiness(id: string) {
  return fetchJson<{ checks: AssistantPublishCheck[] }>(`/assistants/${id}/readiness`)
}

const postJson = sharedPostJson

export function createAssistant(input: CreateAssistantInput) {
  return postJson<AssistantDto>('/assistants', input)
}

export async function updateAssistant(id: string, input: UpdateAssistantInput): Promise<AssistantDto> {
  const response = await authFetch(`/api/v1/assistants/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update assistant')
  return (await response.json()) as AssistantDto
}

export async function deleteAssistant(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/assistants/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete assistant')
}

export function generateAssistantConfig(description: string) {
  return postJson<GeneratedAssistantConfig>('/assistants/generate-config', { description })
}

export async function publishAssistant(id: string) {
  return postJson<{ id: string; isActive: boolean; status: string; publishedVersion: number }>(
    `/assistants/${id}/publish`,
    {},
  )
}

export async function unpublishAssistant(id: string) {
  return postJson<{ id: string; isActive: boolean; status: string }>(`/assistants/${id}/unpublish`, {})
}

export function cloneAssistant(id: string) {
  return postJson<AssistantDto>(`/assistants/${id}/clone`, {})
}
