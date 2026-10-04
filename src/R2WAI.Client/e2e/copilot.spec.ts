import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Verifies the OTHER real chat mechanism (see useChatSession.ts): HTTP POST
// persists + triggers generation, SignalR group-broadcast ("StreamChunk"/
// "StreamComplete") renders it live. Distinct from ChatDialog's SSE path,
// which streamAssistantChat.test.ts plus a live curl check already covered.
// Local CPU-only Ollama inference is slow (~1-2 min/reply observed) — this
// test's timeout accommodates that, not a regression in the app itself.
test('Copilot panel sends a message and receives a real streamed reply', async ({ page }) => {
  test.setTimeout(360_000)

  await loginAsAdmin(page)
  await page.getByRole('button', { name: 'Toggle AI Copilot' }).click()

  const input = page.getByPlaceholder('Ask something…')
  await expect(input).toBeVisible()
  await input.fill('Say hello in exactly 5 words.')
  await input.press('Enter')

  const drawer = page.locator('.MuiDrawer-paper')
  await expect(drawer.getByText('Say hello in exactly 5 words.')).toBeVisible()

  // Real AI-generated reply bubble — no mocking, hits the live Ollama-backed API.
  const bubbles = drawer.locator('.MuiPaper-root')
  // Cold CPU-only local-model inference can exceed four minutes on the isolated E2E runner.
  // Keep the assertion bounded, but leave enough time for the API's real response to arrive.
  await expect(bubbles).toHaveCount(2, { timeout: 330_000 })
  await expect(bubbles.nth(1)).not.toHaveText('')
})
