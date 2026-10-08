import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 12's verify criteria: each Settings category loads independently and
// actually persists — including the merge-safe write to the shared
// TenantSettings blob (must not clobber the real, working content-moderation
// section that also lives in that same string).
test.describe('Settings', () => {
  test('each category tab loads and General/Features/Limits save for real', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/settings')
    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()
    // AI models and API keys/webhooks have their own pages; Settings keeps only what lives nowhere else.
    await expect(page.getByRole('tab')).toHaveText(['General', 'Features', 'Limits'])

    // General — org details actually persist now (a real Phase 12 backend fix). The original
    // name is put back afterwards; this used to leave the shared demo org renamed "E2E Org …".
    await page.getByRole('tab', { name: 'General' }).click()
    const orgField = page.getByLabel('Organization name')
    await expect(orgField).not.toHaveValue('')
    const originalOrgName = await orgField.inputValue()
    const orgName = `E2E Org ${Date.now()}`
    await orgField.fill(orgName)
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Organization details saved')).toBeVisible()
    await page.reload()
    await expect(orgField).toHaveValue(orgName)
    await orgField.fill(originalOrgName)
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Organization details saved')).toBeVisible()
    await page.reload()
    await expect(orgField).toHaveValue(originalOrgName)

    // Features — feature flags.
    await page.getByRole('tab', { name: 'Features' }).click()
    await expect(page.getByRole('heading', { name: 'Enabled Features' })).toBeVisible()
    await page.getByRole('switch').first().click()
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Feature flags saved')).toBeVisible()

    // Limits — storage and user caps share one "limits" blob key and one save; both must persist.
    await page.getByRole('tab', { name: 'Limits' }).click()
    const maxStorage = String(20_000 + (Date.now() % 50_000))
    const maxUsers = String(1_000 + (Date.now() % 50_000))
    await page.getByLabel('Max storage (MB)').fill(maxStorage)
    await page.getByLabel('Max users').fill(maxUsers)
    await page.getByRole('button', { name: 'Save changes', exact: true }).click()
    await expect(page.getByText('Limits saved')).toBeVisible()
    await page.reload()
    await page.getByRole('tab', { name: 'Limits' }).click()
    await expect(page.getByLabel('Max storage (MB)')).toHaveValue(maxStorage)
    await expect(page.getByLabel('Max users')).toHaveValue(maxUsers)
  })

  test('API keys and webhooks are managed on the API & SDK page', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)
    await page.goto('/developer')
    await page.getByRole('tab', { name: 'Keys & webhooks' }).click()

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
