import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type { BusinessCapabilityDto, BusinessCapabilityFormInput, PagedResult } from './types'

const postJson = sharedPostJson

export function listBusinessCapabilities(assistantId: string) {
  return fetchJson<PagedResult<BusinessCapabilityDto>>(`/business-capabilities?assistantId=${assistantId}&pageSize=100`)
}

export function getBusinessCapability(id: string) {
  return fetchJson<BusinessCapabilityDto>(`/business-capabilities/${id}`)
}

export function createBusinessCapability(input: BusinessCapabilityFormInput) {
  return postJson<BusinessCapabilityDto>('/business-capabilities', input)
}

export async function updateBusinessCapability(id: string, input: BusinessCapabilityFormInput): Promise<BusinessCapabilityDto> {
  const response = await authFetch(`/api/v1/business-capabilities/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id, ...input }),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update capability')
  return (await response.json()) as BusinessCapabilityDto
}

export function setBusinessCapabilityStatus(id: string, status: string) {
  return postJson<BusinessCapabilityDto>(`/business-capabilities/${id}/status`, { status })
}

export async function deleteBusinessCapability(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/business-capabilities/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete capability')
}
