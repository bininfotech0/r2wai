import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ApiRequestError, describeApiError, fetchJson, postJson } from './fetchJson'

const { authFetchMock } = vi.hoisted(() => ({ authFetchMock: vi.fn() }))
vi.mock('../auth/authClient', () => ({ authFetch: authFetchMock }))

beforeEach(() => authFetchMock.mockReset())

describe('describeApiError', () => {
  it('gives a distinct permission-denied message for a 403 ApiRequestError', () => {
    const result = describeApiError(new ApiRequestError(403, 'Forbidden'))
    expect(result.title).toBe("You don't have permission to view this")
    expect(result.description).toBeTruthy()
  })

  it('gives a distinct network-error message for a raw fetch failure (TypeError)', () => {
    const result = describeApiError(new TypeError('Failed to fetch'))
    expect(result.title).toBe('Network error')
    expect(result.description).toBeTruthy()
  })

  it('uses a safe service-unavailable message for a 500 response', () => {
    const result = describeApiError(new ApiRequestError(500, 'Internal Server Error'))
    expect(result.title).toBe('The service could not complete this request')
  })

  it.each([
    [404, 'This item could not be found'],
    [409, 'This change conflicts with the current data'],
    [422, 'Some fields need attention'],
    [429, 'Too many requests'],
  ])('maps HTTP %i to distinct user guidance', (status, title) => {
    expect(describeApiError(new ApiRequestError(status, 'Backend detail')).title).toBe(title)
  })

  it('accepts a caller-supplied fallback title for unrecognized statuses', () => {
    const result = describeApiError(new ApiRequestError(418, 'Unexpected status'), "Couldn't load platform metrics")
    expect(result.title).toBe("Couldn't load platform metrics")
  })

  it('does not use the fallback title for a 403 — permission-denied always wins', () => {
    const result = describeApiError(new ApiRequestError(403, 'Forbidden'), 'Custom fallback')
    expect(result.title).toBe("You don't have permission to view this")
  })
})

describe('fetchJson error responses', () => {
  it('preserves ProblemDetails detail, correlation ID, and field errors', async () => {
    authFetchMock.mockResolvedValue(new Response(JSON.stringify({
      title: 'Validation failed',
      detail: 'The request has invalid fields.',
      traceId: 'corr-123',
      errors: { Name: ['Name is required'] },
    }), { status: 422, headers: { 'Content-Type': 'application/problem+json' } }))

    await expect(fetchJson('/assistants')).rejects.toMatchObject({
      name: 'ApiRequestError',
      status: 422,
      message: 'The request has invalid fields.',
      correlationId: 'corr-123',
      errors: { Name: ['Name is required'] },
    })
  })

  it('uses the shared authenticated JSON path for POST requests', async () => {
    authFetchMock.mockResolvedValue(new Response(JSON.stringify({ id: 'agent-1' }), { status: 201 }))

    await expect(postJson('/assistants', { name: 'Agent' })).resolves.toEqual({ id: 'agent-1' })
    expect(authFetchMock).toHaveBeenCalledWith('/api/v1/assistants', expect.objectContaining({
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'Agent' }),
    }))
  })
})
