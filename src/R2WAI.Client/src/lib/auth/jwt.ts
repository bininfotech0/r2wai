/**
 * Client-side JWT payload decode — no signature verification, matching
 * Blazor's JwtAuthenticationStateProvider (the browser has no signing key,
 * only the API does). A token is only ever trusted for UI purposes (nav
 * visibility, route guards) after a successful call to /api/v1/auth/me or
 * because it was just issued by /auth/login|refresh|entra-id — every real
 * data call is independently re-validated by the API's own JWT bearer auth
 * regardless of what the UI decodes here.
 */

const NON_CLAIM_KEYS = new Set(['exp', 'iat', 'nbf', 'iss', 'aud', 'jti'])

export interface DecodedToken {
  claims: Record<string, string | string[]>
  expiresAtSeconds: number | null
}

function base64UrlDecode(segment: string): string {
  const padded = segment.padEnd(segment.length + ((4 - (segment.length % 4)) % 4), '=')
  const base64 = padded.replace(/-/g, '+').replace(/_/g, '/')
  return decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  )
}

/**
 * Returns null for a malformed OR expired token — same "unusable" outcome
 * the caller treats identically either way, mirroring ParseClaimsFromJwt.
 */
export function decodeJwt(token: string): DecodedToken | null {
  const parts = token.split('.')
  if (parts.length !== 3) return null

  try {
    const payload = JSON.parse(base64UrlDecode(parts[1])) as Record<string, unknown>

    let expiresAtSeconds: number | null = null
    if (typeof payload.exp === 'number') {
      expiresAtSeconds = payload.exp
      if (payload.exp * 1000 < Date.now()) return null
    }

    // JwtService writes every claim as its .NET long-form URI (ClaimTypes.Role,
    // ClaimTypes.Email, ...) or a literal like "tenant_id" — never short JWT-standard
    // names — so copying every payload entry as-is (rather than a hardcoded allowlist)
    // means any claim the backend adds is picked up without another frontend change.
    const claims: Record<string, string | string[]> = {}
    for (const [key, value] of Object.entries(payload)) {
      if (NON_CLAIM_KEYS.has(key)) continue
      claims[key] = Array.isArray(value) ? value.map(String) : String(value)
    }

    return { claims, expiresAtSeconds }
  } catch {
    return null
  }
}
