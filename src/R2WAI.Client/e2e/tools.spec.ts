import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

test.describe('Tools & APIs', () => {
  test('create a tool, browse its detail tabs, edit, then delete', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/tools')
    await expect(page.getByRole('heading', { name: 'Tools & APIs' })).toBeVisible()

    const name = `E2E Tool ${Date.now()}`
    await page.getByRole('button', { name: 'New Tool' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Name').fill(name)
    await dialog.getByLabel('Endpoint path').fill('/e2e/test')
    await dialog.getByRole('button', { name: 'Create' }).click()

    await expect(page).toHaveURL(/\/tools\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { name }).first()).toBeVisible()

    // Tabs.
    await page.getByRole('tab', { name: 'Schema' }).click()
    await expect(page.getByText(/no input\/output JSON-schema/)).toBeVisible()

    await page.getByRole('tab', { name: 'Security' }).click()
    await expect(page.getByText('Risk level:')).toBeVisible()

    // Test tab now actually sends a real request through the same call path an AI agent uses
    // (the old "no backend endpoint" disclaimer was true when this test was written, but the
    // feature has since been implemented for real) — the endpoint isn't reachable from here, so
    // assert a result surfaces at all rather than which one.
    await page.getByRole('tab', { name: 'Test', exact: true }).click()
    await expect(page.getByText(/Sends a real request to the configured endpoint/)).toBeVisible()
    await page.getByRole('button', { name: 'Run Test' }).click()
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 15_000 })

    // Even the tool's own creation is a real audited event (Create ·
    // System) — proves the Usage tab reads genuine audit-log data, not a
    // stub.
    await page.getByRole('tab', { name: 'Usage' }).click()
    await expect(page.getByText(/Create · System/)).toBeVisible()

    // Versioning is now real too (same pattern as Test above) — a fresh tool has none yet, and
    // Create Version actually persists a real snapshot.
    await page.getByRole('tab', { name: 'Versions' }).click()
    await expect(page.getByText('No versions yet', { exact: true })).toBeVisible()
    await page.getByRole('button', { name: 'Create Version' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('Version 1')).toBeVisible()

    // Edit.
    await page.getByRole('tab', { name: 'Overview' }).click()
    await page.getByRole('button', { name: 'Edit' }).click()
    const editDialog = page.getByRole('dialog')
    await editDialog.getByLabel('Description').fill('Updated by Playwright')
    await editDialog.getByRole('button', { name: 'Save changes' }).click()
    await expect(page.getByText('Tool updated')).toBeVisible()
    await expect(page.getByText('Updated by Playwright').first()).toBeVisible()

    // Clean up.
    await page.getByRole('button', { name: 'Delete' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Tool deleted')).toBeVisible()
    await expect(page).toHaveURL(/\/tools$/)
  })
})
