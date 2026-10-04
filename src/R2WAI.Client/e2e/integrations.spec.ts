import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 9's verify criteria: integration CRUD against the real API. Test
// Connection makes a real outbound call via DynamicToolExecutor — its
// success/failure depends on network reachability from inside the API
// container, so this only asserts the request completes (no crash), not a
// particular result.
test.describe('Integrations', () => {
  test('create, test, toggle, and delete an integration', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/integrations')
    await expect(page.getByRole('heading', { name: 'Integrations' })).toBeVisible()

    const uniqueName = `E2E Integration ${Date.now()}`
    await page.getByRole('button', { name: 'New Integration' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Name').fill(uniqueName)
    await dialog.getByLabel('Base URL').fill('https://example.com')
    await dialog.getByRole('button', { name: 'Create' }).click()

    await expect(page.getByText('Integration created')).toBeVisible()
    await expect(page.getByText(uniqueName)).toBeVisible()

    const card = page.locator('.MuiCard-root').filter({ hasText: uniqueName })

    // Open it back up via the explicit Edit action (cards no longer open on a
    // whole-card click — Phase 5 replaced that with explicit Test/Edit/Advanced).
    await card.getByRole('button', { name: 'Edit' }).click()
    await expect(dialog.getByLabel('Name')).toHaveValue(uniqueName)
    await dialog.getByRole('button', { name: 'Test connection' }).click()
    await expect(dialog.getByRole('button', { name: 'Testing…' })).toHaveCount(0, { timeout: 15_000 })
    await dialog.getByRole('button', { name: 'Cancel' }).click()

    // Toggle inactive directly from the card.
    await card.getByRole('switch').click()
    await expect(card.getByText('Inactive')).toBeVisible()

    // Clean up.
    await card.getByRole('button', { name: 'Delete' }).click()
    const confirmDialog = page.getByRole('dialog')
    await confirmDialog.getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Integration deleted')).toBeVisible()
    await expect(page.getByText(uniqueName)).not.toBeVisible()
  })
})
