import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

test.describe('Security & Policies', () => {
  test('stat cards render real data and a policy can be configured', async ({ page }) => {
    test.setTimeout(30_000)
    await loginAsAdmin(page)

    await page.goto('/security')
    await expect(page.getByRole('heading', { name: 'Security & Policies' })).toBeVisible()

    // Phase 10: restructured into named accordion sections (Authentication, RBAC, Policies, Tool
    // Security, Data Security, AI Governance, Secrets, Audit) — Authentication starts expanded,
    // the rest collapsed but their summary row (title + stat) is always visible.
    await expect(page.getByRole('button', { name: /^Authentication/ })).toBeVisible()
    await expect(page.getByRole('button', { name: /^RBAC/ })).toBeVisible()
    await expect(page.getByText('Tool Security')).toBeVisible()
    await expect(page.getByText('AI Governance')).toBeVisible()
    await expect(page.getByText('Data Security')).toBeVisible()
    await expect(page.getByRole('button', { name: /^Secrets/ })).toBeVisible()
    await expect(page.getByRole('button', { name: /^Audit/ })).toBeVisible()

    // RBAC section: expand it and confirm the real 3-role set with nav persona chips.
    await page.getByRole('button', { name: /^RBAC/ }).click()
    await expect(page.getByRole('button', { name: /^RBAC 3 system, \d+ custom roles/ })).toBeVisible()
    await expect(page.getByText('Not tied to nav')).toHaveCount(0) // all 3 seeded system roles map to a persona
    // exact: true — "Platform-wide super administrator" (the role's own description) otherwise
    // substring-matches "Super Admin" too ("administrator" starts with "admin").
    await expect(page.getByRole('listitem').filter({ hasText: 'SystemAdmin' }).getByText('Super Admin', { exact: true })).toBeVisible()

    // Secrets: honest "not configured" state, no fabricated vault UI.
    await page.getByRole('button', { name: /^Secrets/ }).click()
    await expect(page.getByText(/Credentials are encrypted in the database/)).toBeVisible()

    // Policies section is its own accordion now — expand it before interacting with a policy row.
    await page.getByRole('button', { name: /^Policies/ }).click()
    await page.getByRole('listitem').filter({ hasText: 'AiUsage' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog.getByRole('heading', { name: 'AiUsage Policy' })).toBeVisible()
    await dialog.getByLabel('Content').fill('E2E policy content')
    await dialog.getByRole('button', { name: 'Save' }).click()
    await expect(page.getByText('Policy saved')).toBeVisible()
    await expect(page.getByText('Active').first()).toBeVisible()
  })
})
