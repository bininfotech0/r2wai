import { test, expect } from '@playwright/test'
import { OTHER_TENANT_ADMIN_EMAIL, OTHER_TENANT_ADMIN_PASSWORD } from './fixtures/auth'

// Verifies R2WAI's actual tenant-isolation boundary — the one genuinely enforced boundary this
// codebase has (see the Workspace Boundary Audit) — at the real API level, not just via the UI.
// Every other seeded account belongs to the one default tenant; "othertenant-admin@r2wai.io" is a
// second, fully separate tenant seeded specifically so this can be tested (ApplicationDbContextSeed.cs).
// Hits the API directly with a manually-obtained token rather than driving the SPA, per the audit's
// own instruction: "do not rely on frontend filtering — verify authorization at the API/domain level."
test.describe('Cross-tenant isolation', () => {
  const DEFAULT_TENANT_ASSISTANT_ID = '00000000-0000-0000-0000-000000000501'

  test('a user in one tenant cannot read another tenant\'s resource by ID', async ({ request }) => {
    const loginResponse = await request.post('/api/v1/auth/login', {
      data: { email: OTHER_TENANT_ADMIN_EMAIL, password: OTHER_TENANT_ADMIN_PASSWORD },
    })
    expect(loginResponse.ok()).toBe(true)
    const { token } = (await loginResponse.json()) as { token: string }
    expect(token).toBeTruthy()

    // The EF Core global tenant query filter should make DefaultTenantId's "Cross-Tenant Isolation
    // Probe" assistant invisible to this request entirely — a 404 (resource doesn't exist from this
    // tenant's point of view), not a 403 (resource exists but access is denied). Either would prove
    // isolation holds; 404 is what the actual global-filter mechanism produces.
    const crossTenantResponse = await request.get(`/api/v1/assistants/${DEFAULT_TENANT_ASSISTANT_ID}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(crossTenantResponse.status()).toBe(404)

    // Sanity check: the same account can read its OWN tenant's data — proves the account itself is
    // valid and not just universally rejected (matching RoleMatrixSecurityTests' established pattern).
    const ownProfileResponse = await request.get('/api/v1/auth/me', {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(ownProfileResponse.ok()).toBe(true)
  })

  test('the resource is genuinely reachable by its own tenant, ruling out a universally-broken ID', async ({ request }) => {
    const loginResponse = await request.post('/api/v1/auth/login', {
      data: { email: 'admin@r2wai.io', password: 'R2wai_Admin!2026' },
    })
    const { token } = (await loginResponse.json()) as { token: string }

    const response = await request.get(`/api/v1/assistants/${DEFAULT_TENANT_ASSISTANT_ID}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    expect(response.ok()).toBe(true)
  })
})
