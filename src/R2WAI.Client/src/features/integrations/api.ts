import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  CreateIntegrationInput,
  IntegrationCatalogEntryDto,
  IntegrationDto,
  OpenApiAnalyzeResult,
  OpenApiCommitResult,
  OpenApiOperationCandidate,
  PagedResult,
  TestConnectionResult,
  UpdateIntegrationInput,
} from './types'

const postJson = sharedPostJson

export function listIntegrations(page: number, pageSize: number, search: string, category?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  if (category) params.set('category', category)
  return fetchJson<PagedResult<IntegrationDto>>(`/integrations?${params.toString()}`)
}

export function getIntegration(id: string) {
  return fetchJson<IntegrationDto>(`/integrations/${id}`)
}

export function getIntegrationCatalog() {
  return fetchJson<IntegrationCatalogEntryDto[]>('/integrations/catalog')
}

// POST/PUT return a raw Guid (not a DTO) — the id of the created/updated row.
export function createIntegration(input: CreateIntegrationInput) {
  return postJson<string>('/integrations', input)
}

export async function updateIntegration(id: string, input: UpdateIntegrationInput): Promise<string> {
  const response = await authFetch(`/api/v1/integrations/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update integration')
  return (await response.json()) as string
}

export async function deleteIntegration(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/integrations/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete integration')
}

export function toggleIntegration(id: string) {
  return postJson<{ id: string; isActive: boolean }>(`/integrations/${id}/toggle`, {})
}

export function testIntegration(id: string) {
  return postJson<TestConnectionResult>(`/integrations/${id}/test`, {})
}

// Kept tolerant of 404/501 for safety during rollout, even though the backend
// endpoint is now live (Track B Phase 3a) — a degraded/misconfigured deploy
// should still explain itself rather than crash.
export async function analyzeOpenApiSpec(source: { url?: string; fileContent?: string }): Promise<OpenApiAnalyzeResult> {
  const response = await authFetch('/api/v1/integrations/openapi-import/analyze', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(source),
  })
  if (response.status === 404 || response.status === 501) {
    throw new ApiRequestError(response.status, 'not-available')
  }
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to analyze OpenAPI spec')
  return (await response.json()) as OpenApiAnalyzeResult
}

export function commitOpenApiImport(baseUrl: string, operations: OpenApiOperationCandidate[]) {
  return postJson<OpenApiCommitResult>('/integrations/openapi-import/commit', { baseUrl, operations })
}
