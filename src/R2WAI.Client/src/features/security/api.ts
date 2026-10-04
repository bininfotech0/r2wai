import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson } from '../../lib/api/fetchJson'
import type { AuthSecurityStatusDto, GlobalPolicyDto, UpsertPolicyInput } from './types'

export function listPolicies() {
  return fetchJson<GlobalPolicyDto[]>('/governance/policies')
}

export function getAuthStatus() {
  return fetchJson<AuthSecurityStatusDto>('/governance/auth-status')
}

export async function upsertPolicy(input: UpsertPolicyInput): Promise<GlobalPolicyDto> {
  const response = await authFetch(`/api/v1/governance/policies/${input.type}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to save policy')
  return (await response.json()) as GlobalPolicyDto
}
