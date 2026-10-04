import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 12's verify criteria: each Settings category loads independently and
// actually persists — including the merge-safe write to the shared
// TenantSettings blob (must not clobber the real, working content-moderation
// section that also lives in that same string).
test.describe('Settings', () => {
  test('each category tab loads and General/Automation/Data/Advanced save for real', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/settings')
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()

    // General — org details actually persist now (a real Phase 12 backend fix).
    await page.getByRole('tab', { name: 'General' }).click()
    const orgName = `E2E Org ${Date.now()}`
    await page.getByLabel('Organization name').fill(orgName)
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Organization details saved')).toBeVisible()
    await page.reload()
    await expect(page.getByLabel('Organization name')).toHaveValue(orgName)

    // AI — the tab links to the dedicated model management page.
    await page.getByRole('tab', { name: 'AI', exact: true }).click()
    await expect(page.getByRole('link', { name: 'Open AI models' })).toBeVisible()

    // Automation — feature flags.
    await page.getByRole('tab', { name: 'Features' }).click()
    await expect(page.getByText('Enabled Features')).toBeVisible()
    await page.getByRole('switch').first().click()
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Feature flags saved')).toBeVisible()

    // Data — limits.
    await page.getByRole('tab', { name: 'Data' }).click()
    const maxStorage = String(20_000 + (Date.now() % 50_000))
    await page.getByLabel('Max storage (MB)').fill(maxStorage)
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Limits saved')).toBeVisible()

    // Advanced — the other half of the same "limits" blob key; must not
    // have been clobbered by Data's save above.
    await page.getByRole('tab', { name: 'Advanced' }).click()
    await expect(page.getByLabel('Max users')).not.toHaveValue('0')
    await page.getByLabel('Max users').fill(String(1_000 + (Date.now() % 50_000)))
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Limits saved')).toBeVisible()

    await page.getByRole('tab', { name: 'Data' }).click()
    await expect(page.getByLabel('Max storage (MB)')).toHaveValue(maxStorage)

    // Developer — API key + webhook CRUD.
    await page.getByRole('tab', { name: 'Developer' }).click()
    const keyName = `E2E Key ${Date.now()}`
    await page.getByRole('button', { name: 'New Key' }).click()
    const keyDialog = page.getByRole('dialog')
    await keyDialog.getByLabel('Name').fill(keyName)
    await keyDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByRole('heading', { name: 'API Key Created' })).toBeVisible()
    await page.getByRole('button', { name: 'Done' }).click()
    const keyRow = page.getByText(keyName)
    await expect(keyRow).toBeVisible()

    const webhookName = `E2E Webhook ${Date.now()}`
    await page.getByRole('button', { name: 'New Webhook' }).click()
    const webhookDialog = page.getByRole('dialog')
    await webhookDialog.getByLabel('Name').fill(webhookName)
    await webhookDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('Webhook created')).toBeVisible()
    await expect(page.getByText(webhookName)).toBeVisible()

    // Clean up. Both deletes go through ConfirmDialog -- clicking the row's Delete icon only
    // opens it (deletion state), the mutation doesn't fire until its own confirm button is
    // clicked too.
    await page.getByRole('listitem').filter({ hasText: webhookName }).getByLabel('Delete').click()
    await page.getByRole('button', { name: 'Delete webhook' }).click()
    await expect(page.getByText('Webhook deleted')).toBeVisible()
    await page.getByRole('listitem').filter({ hasText: keyName }).getByLabel('Delete').click()
    await page.getByRole('button', { name: 'Revoke key' }).click()
    await expect(page.getByText('API key deleted')).toBeVisible()
  })
})
