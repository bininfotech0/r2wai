import { test as base, type APIRequestContext } from '@playwright/test'
import { ADMIN_EMAIL, ADMIN_PASSWORD } from './auth'

// Where each kind of record a spec creates can be listed (?search=) and deleted (/{id}).
const ENDPOINTS = {
  automation: '/api/v1/workflows',
  assistant: '/api/v1/assistants',
  model: '/api/v1/admin/models',
} as const

export type CleanupKind = keyof typeof ENDPOINTS

export interface Cleanup {
  /** Delete the record with exactly this name after the test, whether it passed or failed. */
  add(kind: CleanupKind, name: string): void
}

async function adminToken(request: APIRequestContext): Promise<string> {
  const response = await request.post('/api/v1/auth/login', { data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD } })
  return ((await response.json()) as { token: string }).token
}

/**
 * Specs used to delete what they created as their last UI step, so any earlier failure (often a
 * slow live-model call) left "E2E …" records piling up in the shared demo database. Registering
 * them here moves the delete into fixture teardown, which runs regardless of the outcome.
 * Matching is by exact name, so a concurrent run's records are never touched.
 */
export const test = base.extend<{ cleanup: Cleanup }>({
  cleanup: async ({ request }, use) => {
    const pending: { kind: CleanupKind; name: string }[] = []
    await use({ add: (kind, name) => pending.push({ kind, name }) })
    if (pending.length === 0) return

    const headers = { Authorization: `Bearer ${await adminToken(request)}` }
    for (const { kind, name } of pending) {
      const list = await request.get(`${ENDPOINTS[kind]}?page=1&pageSize=50&search=${encodeURIComponent(name)}`, { headers })
      if (!list.ok()) continue
      const { items = [] } = (await list.json()) as { items?: { id: string; name: string }[] }
      for (const item of items.filter((i) => i.name === name)) {
        await request.delete(`${ENDPOINTS[kind]}/${item.id}`, { headers })
      }
    }
  },
})

export { expect } from '@playwright/test'
