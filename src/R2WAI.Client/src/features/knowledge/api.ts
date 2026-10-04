import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  AddSourceInput,
  ComparisonResultDto,
  CreateKnowledgeBaseInput,
  DocumentDto,
  KnowledgeBaseDto,
  KnowledgeBaseSourceDto,
  PagedResult,
  UpdateKnowledgeBaseInput,
} from './types'

const postJson = sharedPostJson

export function listKnowledgeBases(page: number, pageSize: number, search: string, applicationId?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  if (applicationId) params.set('applicationId', applicationId)
  return fetchJson<PagedResult<KnowledgeBaseDto>>(`/knowledgebases?${params.toString()}`)
}

export function getKnowledgeBase(id: string) {
  return fetchJson<KnowledgeBaseDto>(`/knowledgebases/${id}`)
}

export function createKnowledgeBase(input: CreateKnowledgeBaseInput) {
  return postJson<KnowledgeBaseDto>('/knowledgebases', input)
}

export async function updateKnowledgeBase(id: string, input: UpdateKnowledgeBaseInput): Promise<KnowledgeBaseDto> {
  const response = await authFetch(`/api/v1/knowledgebases/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update knowledge base')
  return (await response.json()) as KnowledgeBaseDto
}

export async function deleteKnowledgeBase(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/knowledgebases/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete knowledge base')
}

export function addSource(knowledgeBaseId: string, input: AddSourceInput) {
  return postJson<KnowledgeBaseSourceDto>(`/knowledgebases/${knowledgeBaseId}/sources`, input)
}

export async function deleteSource(sourceId: string): Promise<void> {
  const response = await authFetch(`/api/v1/knowledgebases/sources/${sourceId}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to remove source')
}

export function reindexKnowledgeBase(id: string) {
  return postJson<{ knowledgeBaseId: string; totalDocuments: number; reindexed: number; status: string }>(
    `/knowledgebases/${id}/reindex`,
    {},
  )
}

export async function uploadDocument(file: File, knowledgeBaseId: string): Promise<DocumentDto> {
  const form = new FormData()
  form.append('file', file)
  form.append('knowledgeBaseId', knowledgeBaseId)
  const response = await authFetch('/api/v1/documents/upload', { method: 'POST', body: form })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to upload document')
  return (await response.json()) as DocumentDto
}

export function listDocuments(page: number, pageSize: number, knowledgeBaseId: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), knowledgeBaseId })
  return fetchJson<PagedResult<DocumentDto>>(`/documents?${params.toString()}`)
}

export function getDocumentContent(id: string) {
  return fetchJson<{ content: string }>(`/documents/${id}/content`)
}

export async function deleteDocument(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/documents/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete document')
}

export function compareDocuments(sourceDocumentId: string, targetDocumentId: string) {
  return postJson<ComparisonResultDto>('/documents/compare', { sourceDocumentId, targetDocumentId })
}
