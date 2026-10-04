import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Full CRUD + live preview streaming, per Phase 6's verify criteria. Manual
// creation (not AI-generate) keeps this test fast; the AI-generate path
// shares the same create/update endpoints already exercised elsewhere, and
// the live-preview assertion below is what actually needs a real AI call.
test('Assistant Studio: create, edit, live preview, publish, delete', async ({ page }) => {
  test.setTimeout(240_000)

  await loginAsAdmin(page)
  await page.goto('/assistants')
  await expect(page.getByRole('heading', { name: 'Agents' })).toBeVisible()

  // Create (manual path)
  await page.getByRole('button', { name: 'New Assistant' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Assistant ${Date.now()}`
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Name').fill(uniqueName)
  await dialog.getByRole('button', { name: 'Create' }).click()

  // Lands on the Studio editor for the new assistant.
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  // Overview edit + save
  const nameField = page.getByLabel('Name')
  await nameField.fill(`${uniqueName} (Updated)`)
  const instructions = page.getByLabel('Instructions')
  await instructions.fill('You are a helpful assistant for E2E testing. Reply concisely.')
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByText('Saved')).toBeVisible()
  await expect(page.getByRole('heading', { name: `${uniqueName} (Updated)` })).toBeVisible()

  // Live preview — real AI call through the Studio's right pane. Wait for
  // the "streaming" chip to disappear (generation complete) before checking
  // content — the streaming placeholder shows "…" immediately, which is
  // non-empty text and would pass a bare not-toHaveText('') assertion
  // without proving any real content was ever generated.
  // A word-count constraint ("say hello in exactly 3 words") sends small
  // reasoning models like qwen3 into pathological over-thinking (~90s of
  // self-correction, confirmed directly against Ollama in Phase 10/11) —
  // long enough to blow past even this test's 200s wait on a loaded shared
  // Ollama instance. A plain, unconstrained prompt keeps the model's
  // thinking phase to a few seconds.
  const preview = page.getByText('Live Preview').locator('..').locator('..')
  const previewInput = preview.getByPlaceholder('Ask something…')
  await previewInput.fill('Reply with just the word OK and nothing else.')
  await previewInput.press('Enter')
  await expect(preview.getByText('Reply with just the word OK and nothing else.')).toBeVisible()
  await expect(preview.getByText('streaming')).toBeVisible()
  await expect(preview.getByText('streaming')).not.toBeVisible({ timeout: 200_000 })
  const replyBubble = preview.locator('.MuiPaper-root').last()
  await expect(replyBubble).not.toHaveText('')
  await expect(replyBubble).not.toHaveText('…')

  // Publish
  await page.getByRole('button', { name: 'Publish', exact: true }).click()
  await page.getByRole('button', { name: /^Publish \d/ }).click()
  await expect(page.getByText('Assistant published')).toBeVisible()

  // Delete
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Delete' }).last().click()
  await expect(page.getByText('Assistant deleted')).toBeVisible()
  await expect(page).toHaveURL(/\/assistants$/)
  // exact:true avoids a strict-mode clash with the delete confirmation
  // dialog's own "Delete "...Updated)"? ..." text, which can still be
  // mid-unmount when this assertion first polls.
  await expect(page.getByText(`${uniqueName} (Updated)`, { exact: true })).not.toBeVisible()
})
