import { authFetch } from '../auth/authClient'

export class ApiRequestError extends Error {
  status: number
  correlationId?: string
  errors?: Record<string, string[]>

  constructor(status: number, message: string, details?: { correlationId?: string; errors?: Record<string, string[]> }) {
    super(message)
    this.name = 'ApiRequestError'
    this.status = status
    this.correlationId = details?.correlationId
    this.errors = details?.errors
  }
}

/**
 * Distinguishes the failure kinds a query's `error` can actually be, for a UX-Quality-appropriate
 * message: a real HTTP response the caller isn't allowed to see (403 — distinct from 401, which is
 * already handled app-wide by authFetch's session-expiry redirect), a genuine network failure
 * (fetch() rejected before any HTTP response — offline, DNS, CORS), or any other server-side error.
 */
export function describeApiError(error: unknown, fallbackTitle = 'Unable to load this data.'): { title: string; description?: string } {
  if (error instanceof ApiRequestError) {
    switch (error.status) {
      case 401:
        return { title: 'Your session has expired', description: 'Sign in again to continue.' }
      case 403:
        return { title: "You don't have permission to view this", description: 'Contact your administrator if you believe this is a mistake.' }
      case 404:
        return { title: 'This item could not be found', description: 'It may have been deleted or moved.' }
      case 409:
        return { title: 'This change conflicts with the current data', description: error.message }
      case 422:
        return { title: 'Some fields need attention', description: error.message }
      case 429:
        return { title: 'Too many requests', description: 'Please wait a moment, then try again.' }
      case 500:
      case 502:
      case 503:
      case 504:
        return { title: 'The service could not complete this request', description: 'Try again shortly. If the problem continues, contact your administrator.' }
      default:
        return { title: fallbackTitle }
    }
  }
  if (error instanceof TypeError) {
    return { title: 'Network error', description: 'Check your connection and try again.' }
  }
  return { title: fallbackTitle }
}

/** GET a JSON endpoint under /api/v1 through the refresh-on-401 authFetch. */
export async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(`/api/v1${path}`, init)
  if (!response.ok) {
    // Many endpoints return a structured { message } / { error } body on failure
    // (e.g. AdminController.TestModelConnection's "Connection failed: ..." reason) —
    // surface that instead of a generic status code so callers/UIs can show the
    // real cause. Falls back to the generic message if the body isn't JSON or has
    // neither field.
    let message = `HTTP ${response.status} for ${path}`
    let correlationId: string | undefined
    let errors: Record<string, string[]> | undefined
    try {
      const body = await response.clone().json()
      if (body && typeof body === 'object') {
        const problem = body as Record<string, unknown>
        const candidate = problem.detail ?? problem.message ?? problem.error ?? problem.title
        if (typeof candidate === 'string' && candidate.trim()) message = candidate
        const extensions = problem.extensions && typeof problem.extensions === 'object'
          ? problem.extensions as Record<string, unknown>
          : undefined
        const correlation = problem.correlationId ?? problem.traceId ?? problem.requestId
          ?? extensions?.correlationId ?? extensions?.traceId
        if (typeof correlation === 'string') correlationId = correlation
        if (problem.errors && typeof problem.errors === 'object' && !Array.isArray(problem.errors)) {
          errors = Object.fromEntries(Object.entries(problem.errors).map(([key, value]) => [
            key,
            (Array.isArray(value) ? value : [value]).filter((item): item is string => typeof item === 'string'),
          ]))
        }
      }
    } catch {
      // Non-JSON error body — keep the generic message.
    }
    throw new ApiRequestError(response.status, message, { correlationId, errors })
  }
  return (await response.json()) as T
}

/** POST JSON through the shared auth and problem-response handling. */
export function postJson<T>(path: string, body: unknown): Promise<T> {
  return fetchJson<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}
