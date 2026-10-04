import { describe, it, expect } from 'vitest'
import { decodeJwt } from './jwt'

function base64UrlEncode(obj: unknown): string {
  const json = JSON.stringify(obj)
  const base64 = btoa(unescape(encodeURIComponent(json)))
  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function makeToken(payload: Record<string, unknown>): string {
  const header = base64UrlEncode({ alg: 'HS256', typ: 'JWT' })
  const body = base64UrlEncode(payload)
  return `${header}.${body}.fakesignature`
}

describe('decodeJwt', () => {
  it('returns null for a malformed token', () => {
    expect(decodeJwt('not-a-jwt')).toBeNull()
    expect(decodeJwt('only.two')).toBeNull()
  })

  it('returns null for an expired token', () => {
    const token = makeToken({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) - 3600 })
    expect(decodeJwt(token)).toBeNull()
  })

  it('decodes claims and drops standard JWT-registered keys', () => {
    const token = makeToken({
      sub: 'user-1',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role': ['Admin', 'User'],
      tenant_id: 'tenant-1',
      exp: Math.floor(Date.now() / 1000) + 3600,
      iat: Math.floor(Date.now() / 1000),
      iss: 'R2WAI',
      aud: 'R2WAI-API',
    })

    const decoded = decodeJwt(token)
    expect(decoded).not.toBeNull()
    expect(decoded?.claims.sub).toBe('user-1')
    expect(decoded?.claims.tenant_id).toBe('tenant-1')
    expect(decoded?.claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role']).toEqual([
      'Admin',
      'User',
    ])
    // registered/standard keys are not surfaced as app claims
    expect(decoded?.claims.exp).toBeUndefined()
    expect(decoded?.claims.iat).toBeUndefined()
    expect(decoded?.claims.iss).toBeUndefined()
    expect(decoded?.claims.aud).toBeUndefined()
  })
})
