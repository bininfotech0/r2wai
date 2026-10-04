import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError } from '../../lib/api/fetchJson'
import type { UserInfo } from '../../lib/auth/types'

export interface UpdateProfileInput {
  firstName: string
  lastName: string
  mobileNumber: string | null
  email: string | null
}

export async function updateProfile(input: UpdateProfileInput): Promise<UserInfo> {
  const response = await authFetch('/api/v1/auth/profile', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update profile')
  return (await response.json()) as UserInfo
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const response = await authFetch('/api/v1/auth/change-password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ currentPassword, newPassword }),
  })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiRequestError(response.status, body?.error ?? 'Failed to change password')
  }
}

export async function uploadAvatar(file: File): Promise<{ avatarUrl: string }> {
  const form = new FormData()
  form.append('file', file)
  const response = await authFetch('/api/v1/auth/profile/avatar', { method: 'POST', body: form })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiRequestError(response.status, body?.error ?? 'Failed to upload avatar')
  }
  return (await response.json()) as { avatarUrl: string }
}

export interface MfaStatus {
  mfaEnabled: boolean
}

export async function getMfaStatus(): Promise<MfaStatus> {
  const response = await authFetch('/api/v1/auth/mfa/status')
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to load MFA status')
  return (await response.json()) as MfaStatus
}

export interface MfaSetupResult {
  secret: string
  setupUri: string
}

export async function setupMfa(): Promise<MfaSetupResult> {
  const response = await authFetch('/api/v1/auth/mfa/setup', { method: 'POST' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to start MFA setup')
  return (await response.json()) as MfaSetupResult
}

export async function enableMfa(secret: string, code: string): Promise<void> {
  const response = await authFetch('/api/v1/auth/mfa/enable', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ secret, code }),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Invalid code — please try again')
}

export async function disableMfa(code: string): Promise<void> {
  const response = await authFetch('/api/v1/auth/mfa/disable', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code }),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Invalid code — please try again')
}
