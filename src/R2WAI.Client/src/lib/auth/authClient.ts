import { tokenStorage } from './tokenStorage'
import { AuthError } from './types'
import type {
  ApiErrorBody,
  EntraIdRequest,
  LoginRequest,
  LoginResponse,
  RefreshResponse,
  UserInfo,
} from './types'

async function jsonRequest(path: string, init?: RequestInit): Promise<Response> {
  return fetch(`/api/v1${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  })
}

export async function loginRequest(request: LoginRequest): Promise<LoginResponse> {
  const response = await jsonRequest('/auth/login', {
    method: 'POST',
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    let body: ApiErrorBody = {}
    try {
      body = (await response.json()) as ApiErrorBody
    } catch {
      // non-JSON error body — fall through with generic message
    }
    throw new AuthError(
      body.error ?? 'Invalid email or password',
      body.mfaRequired ?? false,
      body.mfaSetupRequired ?? false,
      body.passwordChangeRequired ?? false,
      body.setupToken,
    )
  }

  return (await response.json()) as LoginResponse
}

// The three follow-up calls a mfaSetupRequired/passwordChangeRequired AuthError's setupToken is
// good for — deliberately NOT authFetch/tokenStorage, since this token is scoped server-side
// (MfaSetupScopeMiddleware) to exactly one of these calls and must never be persisted as if it
// were a real session.
export interface MfaSetupTokenResult {
  secret: string
  setupUri: string
}

export async function setupMfaWithSetupToken(setupToken: string): Promise<MfaSetupTokenResult> {
  const response = await jsonRequest('/auth/mfa/setup', {
    method: 'POST',
    headers: { Authorization: `Bearer ${setupToken}` },
  })
  if (!response.ok) throw new AuthError('Failed to start MFA setup.')
  return (await response.json()) as MfaSetupTokenResult
}

export async function enableMfaWithSetupToken(setupToken: string, secret: string, code: string): Promise<void> {
  const response = await jsonRequest('/auth/mfa/enable', {
    method: 'POST',
    headers: { Authorization: `Bearer ${setupToken}` },
    body: JSON.stringify({ secret, code }),
  })
  if (!response.ok) {
    let body: ApiErrorBody = {}
    try {
      body = (await response.json()) as ApiErrorBody
    } catch {
      // ignore
    }
    throw new AuthError(body.error ?? 'Invalid code — please try again.')
  }
}

export async function changePasswordWithSetupToken(
  setupToken: string,
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  const response = await jsonRequest('/auth/change-password', {
    method: 'POST',
    headers: { Authorization: `Bearer ${setupToken}` },
    body: JSON.stringify({ currentPassword, newPassword }),
  })
  if (!response.ok) {
    let body: ApiErrorBody = {}
    try {
      body = (await response.json()) as ApiErrorBody
    } catch {
      // ignore
    }
    throw new AuthError(body.error ?? 'Failed to set a new password.')
  }
}

export async function entraIdLoginRequest(request: EntraIdRequest): Promise<LoginResponse> {
  const response = await jsonRequest('/auth/entra-id', {
    method: 'POST',
    body: JSON.stringify(request),
  })
  if (!response.ok) {
    let body: ApiErrorBody = {}
    try {
      body = (await response.json()) as ApiErrorBody
    } catch {
      // ignore
    }
    // Same structured error shape as loginRequest — this endpoint enforces the identical
    // per-account MFA/enrollment/password-policy floors the password path does.
    throw new AuthError(
      body.error ?? 'Microsoft account not provisioned in this workspace.',
      body.mfaRequired ?? false,
      body.mfaSetupRequired ?? false,
      body.passwordChangeRequired ?? false,
      body.setupToken,
    )
  }
  return (await response.json()) as LoginResponse
}

export type MeResult =
  | { outcome: 'valid'; user: UserInfo }
  | { outcome: 'rejected' }
  | { outcome: 'trust' } // network/server error — trust the token for now, matches
  //  JwtAuthenticationStateProvider.IsTokenValidOnServerAsync: only an explicit 401/403
  //  means the token is bad, so a transient API outage doesn't log every user out.

export async function meRequest(token: string): Promise<MeResult> {
  try {
    const response = await jsonRequest('/auth/me', {
      headers: { Authorization: `Bearer ${token}` },
    })
    if (!response.ok) {
      if (response.status === 401 || response.status === 403) return { outcome: 'rejected' }
      return { outcome: 'trust' }
    }
    return { outcome: 'valid', user: (await response.json()) as UserInfo }
  } catch {
    return { outcome: 'trust' }
  }
}

export async function forgotPasswordRequest(email: string): Promise<void> {
  const response = await jsonRequest('/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify({ email }),
  })
  if (!response.ok) {
    throw new AuthError('Unable to send reset instructions. Please try again.')
  }
}

export async function resetPasswordRequest(
  email: string,
  token: string,
  newPassword: string,
): Promise<void> {
  const response = await jsonRequest('/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify({ email, token, newPassword }),
  })
  if (!response.ok) {
    let body: ApiErrorBody = {}
    try {
      body = (await response.json()) as ApiErrorBody
    } catch {
      // ignore
    }
    throw new AuthError(body.error ?? 'Unable to reset password. The link may have expired.')
  }
}

export async function logoutRequest(token: string): Promise<void> {
  try {
    await jsonRequest('/auth/logout', {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
    })
  } catch {
    // best-effort — local session is cleared regardless by the caller
  }
}

// --- Refresh-on-401, mirrors AuthenticatedHttpClient's SemaphoreSlim(1,1):
// concurrent 401s share one in-flight refresh instead of each firing their own.
let refreshInFlight: Promise<boolean> | null = null

async function refreshTokens(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight

  refreshInFlight = (async () => {
    const accessToken = tokenStorage.getToken()
    const refreshToken = tokenStorage.getRefreshToken()
    if (!accessToken || !refreshToken) return false

    try {
      const response = await jsonRequest('/auth/refresh', {
        method: 'POST',
        body: JSON.stringify({ accessToken, refreshToken }),
      })
      if (!response.ok) return false

      const result = (await response.json()) as RefreshResponse
      if (!result.token || !result.refreshToken) return false

      tokenStorage.setToken(result.token)
      tokenStorage.setRefreshToken(result.refreshToken)
      return true
    } catch {
      return false
    }
  })()

  try {
    return await refreshInFlight
  } finally {
    refreshInFlight = null
  }
}

type SessionExpiredListener = () => void
let sessionExpiredListeners: SessionExpiredListener[] = []

export function onSessionExpired(listener: SessionExpiredListener): () => void {
  sessionExpiredListeners.push(listener)
  return () => {
    sessionExpiredListeners = sessionExpiredListeners.filter((l) => l !== listener)
  }
}

function notifySessionExpired(): void {
  tokenStorage.clearAll()
  sessionExpiredListeners.forEach((listener) => listener())
}

/**
 * Drop-in fetch replacement: attaches the bearer token, and on a 401 tries
 * exactly one refresh-and-retry before giving up and clearing the session —
 * mirrors AuthenticatedHttpClient.HandleUnauthorizedAsync.
 */
export async function authFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  const withAuth = (): RequestInit => {
    const headers = new Headers(init.headers)
    const token = tokenStorage.getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    return { ...init, headers }
  }

  const response = await fetch(input, withAuth())
  if (response.status !== 401) return response

  const refreshed = await refreshTokens()
  if (refreshed) {
    const retry = await fetch(input, withAuth())
    if (retry.status !== 401) return retry
  }

  notifySessionExpired()
  return response
}
