import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 10's verify criteria (main SPA half): chatbot CRUD, editable fields,
// channel connect/disconnect, and the embed snippet dialog, all against the
// real API.
test.describe('Chatbots', () => {
  test('create, edit, connect a channel, embed, and delete a chatbot', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/chatbots')
    await expect(page.getByRole('heading', { name: 'Publish' })).toBeVisible()

    const uniqueName = `E2E Chatbot ${Date.now()}`
    await page.getByRole('button', { name: 'New Chatbot' }).click()
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByRole('button', { name: 'Create' }).click()

    await expect(page).toHaveURL(/\/chatbots\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

    // Edit the update-only fields.
    await page.getByLabel('Welcome message').fill('Hi! How can I help?')
    await page.getByLabel('Description').fill('Created by Playwright')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await expect(page.getByText('Chatbot saved')).toBeVisible()

    // Connect a channel.
    await page.getByLabel('Channel', { exact: true }).click();
    (await page.getByRole('option').first()).click()
    await page.getByRole('button', { name: 'Connect' }).click()
    await expect(page.getByText(/channel configuration saved/)).toBeVisible()

    // Embed snippet.
    await page.getByRole('button', { name: 'Embed' }).click()
    const embedDialog = page.getByRole('dialog')
    await expect(embedDialog.getByText(/data-chatbot-id=/)).toBeVisible()
    await embedDialog.getByRole('button', { name: 'Close' }).click()

    // Clean up.
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    const confirmDialog = page.getByRole('dialog')
    await confirmDialog.getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Chatbot deleted')).toBeVisible()
    await expect(page).toHaveURL(/\/chatbots$/)
  })
})
