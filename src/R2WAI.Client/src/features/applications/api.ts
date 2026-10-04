import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  ApplicationAction,
  ApplicationApiDto,
  ApplicationConfigurationDto,
  ApplicationDiscoveryResultDto,
  ApplicationDto,
  ApplicationVersionDto,
  CreateApplicationApiInput,
  CreateApplicationInput,
  PagedResult,
  UpdateApplicationApiInput,
  UpdateApplicationInput,
} from './types'

const postJson = sharedPostJson

async function putJson<T>(path: string, body: unknown): Promise<T> {
  const response = await authFetch(`/api/v1${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) throw new ApiRequestError(response.status, `PUT ${path} failed`)
  return (await response.json()) as T
}

export function listApplications(page: number, pageSize: number, search: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<ApplicationDto>>(`/applications?${params.toString()}`)
}

export function getApplication(id: string) {
  return fetchJson<ApplicationDto>(`/applications/${id}`)
}

export function createApplication(input: CreateApplicationInput) {
  return postJson<ApplicationDto>('/applications', input)
}

export function updateApplication(id: string, input: UpdateApplicationInput) {
  return putJson<ApplicationDto>(`/applications/${id}`, { id, ...input })
}

export async function deleteApplication(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/applications/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete application')
}

export function changeApplicationStatus(id: string, action: ApplicationAction) {
  return postJson<ApplicationDto>(`/applications/${id}/status`, { action })
}

export function discoverApplication(id: string, source: { openApiUrl?: string; openApiFileContent?: string }) {
  return postJson<ApplicationDiscoveryResultDto>(`/applications/${id}/discover`, source)
}

export function listApplicationApis(id: string) {
  return fetchJson<ApplicationApiDto[]>(`/applications/${id}/apis`)
}

export function createApplicationApi(id: string, input: CreateApplicationApiInput) {
  return postJson<ApplicationApiDto>(`/applications/${id}/apis`, input)
}

export function updateApplicationApi(id: string, apiId: string, input: UpdateApplicationApiInput) {
  return putJson<ApplicationApiDto>(`/applications/${id}/apis/${apiId}`, input)
}

export async function deleteApplicationApi(id: string, apiId: string): Promise<void> {
  const response = await authFetch(`/api/v1/applications/${id}/apis/${apiId}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete API')
}

export function getApplicationConfiguration(id: string) {
  return fetchJson<ApplicationConfigurationDto>(`/applications/${id}/configuration`)
}

export function updateApplicationConfiguration(id: string, input: ApplicationConfigurationDto) {
  return putJson<ApplicationConfigurationDto>(`/applications/${id}/configuration`, input)
}

export function listApplicationVersions(id: string) {
  return fetchJson<ApplicationVersionDto[]>(`/applications/${id}/versions`)
}

export function createApplicationVersion(id: string, note: string, publish: boolean) {
  return postJson<ApplicationVersionDto>(`/applications/${id}/versions`, { note: note || undefined, publish })
}

export function rollbackApplicationVersion(id: string, versionId: string) {
  return postJson<ApplicationVersionDto>(`/applications/${id}/versions/${versionId}/rollback`, {})
}
