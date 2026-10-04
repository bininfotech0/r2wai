import { describe, expect, it } from 'vitest'
import { ApiRequestError } from './fetchJson'
import { createAppQueryClient } from './queryClient'

describe('createAppQueryClient', () => {
  it('uses a short shared freshness window and disables mutation retries', () => {
    const client = createAppQueryClient()
    const defaults = client.getDefaultOptions()

    expect(defaults.queries?.staleTime).toBe(30_000)
    expect(defaults.mutations?.retry).toBe(false)
    client.clear()
  })

  it('retries a transient failure once, but not client errors or repeated failures', () => {
    const retry = createAppQueryClient().getDefaultOptions().queries?.retry
    expect(typeof retry).toBe('function')
    if (typeof retry !== 'function') return

    expect(retry(0, new TypeError('offline'))).toBe(true)
    expect(retry(1, new TypeError('offline'))).toBe(false)
    expect(retry(0, new ApiRequestError(422, 'Validation error'))).toBe(false)
    expect(retry(0, new ApiRequestError(503, 'Unavailable'))).toBe(true)
  })
})
