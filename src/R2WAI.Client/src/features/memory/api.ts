import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson } from '../../lib/api/fetchJson'
import type { MemoryOverview, MemorySettings, SaveMemoryInput, UserMemoryDto } from './types'

export const MEMORY_QUERY_KEY = ['memory'] as const

export function getMemoryOverview() {
  return fetchJson<MemoryOverview>('/memory')
}

export function createMemory(input: SaveMemoryInput) {
  return postJson<UserMemoryDto>('/memory', input)
}

export function updateMemory(id: string, input: SaveMemoryInput) {
  return fetchJson<UserMemoryDto>(`/memory/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
}

export function approveMemory(id: string) {
  return postJson<UserMemoryDto>(`/memory/${id}/approve`, {})
}

export async function deleteMemory(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/memory/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete memory')
}

/** partition: 'all', 'personal', 'pending' or an agent's id. */
export function clearMemory(partition: string) {
  return fetchJson<{ removed: number }>(`/memory?partition=${encodeURIComponent(partition)}`, { method: 'DELETE' })
}

export function updateMemorySettings(settings: MemorySettings) {
  return fetchJson<MemorySettings>('/memory/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  })
}

export async function exportMemory(): Promise<Blob> {
  const response = await authFetch('/api/v1/memory/export')
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to export memory')
  return response.blob()
}
