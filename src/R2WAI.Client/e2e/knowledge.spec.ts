import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 9's verify criteria: knowledge base + source/document CRUD against
// the real API/storage. The backend only gives "Url" sources special
// handling (fetches + indexes server-side) — Faq/Database/Integration index
// whatever Content text is pasted, same mechanism, so Faq is used here to
// avoid the test depending on outbound internet access from the container.
test.describe('Knowledge', () => {
  test('create a knowledge base, add a text source and a file, then delete', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/knowledge')
    await expect(page.getByRole('heading', { name: 'Knowledge' })).toBeVisible()

    const uniqueName = `E2E Knowledge Base ${Date.now()}`
    await page.getByRole('button', { name: 'New Knowledge Base' }).click()
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByLabel('Description').fill('Created by Playwright')
    await createDialog.getByRole('button', { name: 'Create' }).click()

    await expect(page).toHaveURL(/\/knowledge\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

    // Sources tab (Phase 4 restructure) — "Add Knowledge" lives here now, not on Overview.
    await page.getByRole('tab', { name: 'Sources' }).click()

    // Add a text (Faq) source.
    await page.getByRole('button', { name: 'Add Knowledge' }).click()
    await page.getByText('FAQ').click()
    await page.getByLabel('Questions and answers').fill('Q: What is R2WAI?\nA: An enterprise AI platform.')
    await page.getByRole('button', { name: 'Add' }).click()
    // Same reasoning as the file upload's own explicit timeout below: adding a source
    // synchronously chunks and embeds it through the local CPU-only Ollama model
    // (KnowledgeBaseService.AddSourceAsync), which the default 5s assertion timeout doesn't
    // reliably cover.
    await expect(page.getByText(/^Faq/)).toBeVisible({ timeout: 20_000 })

    // Add a real file upload.
    await page.getByRole('button', { name: 'Add Knowledge' }).click()
    await page.getByText('Files', { exact: true }).click()
    await page.setInputFiles('input[type="file"]', {
      name: 'e2e-notes.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('Playwright-uploaded knowledge document.'),
    })
    await page.getByRole('button', { name: /^Upload/ }).click()
    await expect(page.getByText('Uploaded')).toBeVisible({ timeout: 20_000 })
    await page.getByRole('button', { name: 'Close' }).click()

    // Documents tab — uploaded files are listed here, separate from the Sources list.
    await page.getByRole('tab', { name: 'Documents' }).click()
    await expect(page.getByRole('gridcell', { name: 'e2e-notes.txt' })).toBeVisible()

    // Clean up.
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    const confirmDialog = page.getByRole('dialog')
    await confirmDialog.getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Knowledge base deleted')).toBeVisible()
    await expect(page).toHaveURL(/\/knowledge$/)
  })
})
