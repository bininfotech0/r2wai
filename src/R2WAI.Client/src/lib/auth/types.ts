/**
 * Mirrors AuthController's request/response records exactly
 * (src/R2WAI.Api/Controllers/AuthController.cs). ASP.NET Core's default
 * System.Text.Json naming policy is camelCase, so these fields match what
 * actually comes over the wire.
 */

export interface LoginRequest {
  email: string
  password: string
  mfaCode?: string
}

export interface UserInfo {
  id: string
  email: string | null
  firstName: string
  lastName: string
  displayName: string
  avatarUrl: string | null
  role: string
  roles: string[]
  tenantId: string
  isActive: boolean
  lastLoginAt: string | null
  createdAt: string
  updatedAt: string | null
  mobileNumber: string | null
  hasAadhaar: boolean
}

export interface LoginResponse {
  token: string
  refreshToken: string
  expiresAt: string
  user: UserInfo
}

export interface RefreshRequest {
  accessToken: string
  refreshToken: string
}

export interface RefreshResponse {
  token: string
  refreshToken: string
  expiresAt: string
}

export interface EntraIdRequest {
  idToken: string
  // Only sent on the retry after the backend returns mfaRequired for an account with local TOTP
  // MFA enabled — a valid Entra id_token proves identity to Entra, not to R2WAI's own second
  // factor, so this endpoint enforces it the same way /auth/login does.
  mfaCode?: string
}

export interface ApiErrorBody {
  error?: string
  mfaRequired?: boolean
  // Set (with setupToken) instead of a normal 200 when a tenant's "Auth" GlobalPolicy blocks
  // full sign-in until the user resolves something — MFA enrollment or an expired password. The
  // token is scoped server-side (MfaSetupScopeMiddleware) to only the relevant follow-up call.
  mfaSetupRequired?: boolean
  passwordChangeRequired?: boolean
  setupToken?: string
  retryAfter?: number
}

export class AuthError extends Error {
  mfaRequired: boolean
  mfaSetupRequired: boolean
  passwordChangeRequired: boolean
  setupToken?: string

  constructor(
    message: string,
    mfaRequired = false,
    mfaSetupRequired = false,
    passwordChangeRequired = false,
    setupToken?: string,
  ) {
    super(message)
    this.name = 'AuthError'
    this.mfaRequired = mfaRequired
    this.mfaSetupRequired = mfaSetupRequired
    this.passwordChangeRequired = passwordChangeRequired
    this.setupToken = setupToken
  }
}
