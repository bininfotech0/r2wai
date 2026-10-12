import { test, expect } from './fixtures/cleanup'
import { loginAsAdmin } from './fixtures/auth'

// Full CRUD + live preview streaming, per Phase 6's verify criteria. Manual
// creation (not AI-generate) keeps this test fast; the AI-generate path
// shares the same create/update endpoints already exercised elsewhere, and
// the live-preview assertion below is what actually needs a real AI call.
test('Assistant Studio: create, edit, live preview, publish, delete', async ({ page, cleanup }) => {
  test.setTimeout(240_000)

  await loginAsAdmin(page)
  await page.goto('/assistants')
  await expect(page.getByRole('heading', { name: 'Agents' })).toBeVisible()

  // Create (manual path)
  await page.getByRole('button', { name: 'New agent' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Assistant ${Date.now()}`
  // The test renames it partway through, so either name may be the one left behind.
  cleanup.add('assistant', uniqueName)
  cleanup.add('assistant', `${uniqueName} (Updated)`)
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Name').fill(uniqueName)
  await dialog.getByRole('button', { name: 'Create' }).click()

  // Lands on the Studio editor for the new assistant.
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  // Overview: section cards edit in place; one sticky bar saves them together.
  const details = page.getByRole('region', { name: 'Details' })
  await details.getByRole('button', { name: 'Edit' }).click()
  await details.getByLabel('Name').fill(`${uniqueName} (Updated)`)
  const instructionsCard = page.getByRole('region', { name: 'Instructions' })
  await instructionsCard.getByRole('button', { name: 'Edit' }).click()
  await instructionsCard.getByLabel('Instructions').fill('You are a helpful assistant for E2E testing. Reply concisely.')
  // Publishing is blocked until the edits are saved.
  await expect(page.getByRole('button', { name: 'Publish', exact: true })).toBeDisabled()
  const unsaved = page.getByRole('region', { name: 'Unsaved changes' })
  await unsaved.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByText('Saved', { exact: true })).toBeVisible()
  await expect(unsaved).toHaveCount(0)
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
  // Anchor on the input itself, then take its nearest container that also holds the pane's
  // "Live preview" heading — robust to the pane's wrapper depth changing.
  const previewInput = page.getByPlaceholder('Ask something…')
  const preview = page.getByRole('region', { name: 'Test your agent' })
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
  await expect(page.getByText('Agent published')).toBeVisible()

  // Delete lives in Settings → Advanced, as in Copilot Studio.
  await page.getByRole('button', { name: 'Settings' }).click()
  await page.getByRole('tab', { name: 'Advanced' }).click()
  await page.getByRole('button', { name: 'Delete agent' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete', exact: true }).click()
  await expect(page.getByText('Agent deleted')).toBeVisible()
  await expect(page).toHaveURL(/\/assistants$/)
  // exact:true avoids a strict-mode clash with the delete confirmation
  // dialog's own "Delete "...Updated)"? ..." text, which can still be
  // mid-unmount when this assertion first polls.
  // toHaveCount(0) retries; a negated toBeVisible fails outright on two matches, and the agent page
  // (name in the header and the Details card) can still be on screen while the list route loads.
  await expect(page.getByRole('heading', { name: 'Agents' })).toBeVisible()
  await expect(page.getByText(`${uniqueName} (Updated)`, { exact: true })).toHaveCount(0)
})
