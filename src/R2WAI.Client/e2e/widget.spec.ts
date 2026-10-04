import http from 'node:http'
import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Shared by both tests below: serves a bare page on localhost:4173 embedding the given chatbot,
// runs `run` against it, and always tears the server down afterward.
async function withDemoPage(
  browser: import('@playwright/test').Browser,
  chatbotId: string,
  run: (demoPage: import('@playwright/test').Page) => Promise<void>,
) {
  const html = `<!doctype html><html><body>
    <h1>Third-party page</h1>
    <script src="http://localhost:8080/widget/widget.js" data-chatbot-id="${chatbotId}" data-base-url="http://localhost:8080" data-title="Demo Bot" async></script>
  </body></html>`
  const server = http.createServer((_req, res) => {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' })
    res.end(html)
  })
  await new Promise<void>((resolve) => server.listen(4173, '127.0.0.1', resolve))

  try {
    const demoContext = await browser.newContext()
    const demoPage = await demoContext.newPage()
    await demoPage.goto('http://localhost:4173/')
    // Chromium suspends network I/O on backgrounded tabs — keep this tab foregrounded.
    await demoPage.bringToFront()
    expect(new URL(demoPage.url()).origin).toBe('http://localhost:4173')
    await run(demoPage)
    await demoContext.close()
  } finally {
    await new Promise<void>((resolve) => server.close(() => resolve()))
  }
}

// Phase 10's verify criteria (widget half): the widget loads and streams
// chat correctly embedded on a bare page OUTSIDE the SPA's own origin — not
// just outside its router/auth, genuinely a different origin (localhost:4173
// vs. the gateway's localhost:8080), so this actually exercises the
// cross-origin fetch/CORS path a real third-party embed would hit.
//
// Docker Compose runs the API with ASPNETCORE_ENVIRONMENT=Production, where
// CORS.AllowedOrigins comes only from the CORS__AllowedOrigins__N env
// indexers in docker-compose.yml (appsettings.Development.json's 4173/5173
// dev-origin list, from Phase 0, never applies in this environment) — so
// localhost:4173 is NOT allowed by default here. Running this spec requires
// docker compose up with WIDGET_TEST_ORIGIN=http://localhost:4173 (see
// docker-compose.yml's CORS__AllowedOrigins__1). A REAL third-party
// production domain needs the same explicit addition — the widget has no
// embed-token/CORS-bypass mechanism of its own (see ChatbotEmbedDialog's
// warning); that's a genuine deployment constraint, not something this test
// works around.
// .serial: both tests in this file bind the fixed port 4173 (it has to be fixed — it's the one
// origin docker-compose's WIDGET_TEST_ORIGIN allows through CORS), so they can't run concurrently.
test.describe.serial('Embeddable widget', () => {
  test('streams a real reply when embedded on a different origin', async ({ page, browser }) => {
    test.setTimeout(90_000)
    await loginAsAdmin(page)

    // Seed a chatbot to embed.
    await page.goto('/chatbots')
    const uniqueName = `E2E Widget Chatbot ${Date.now()}`
    await page.getByRole('button', { name: 'New Chatbot' }).click()
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page).toHaveURL(/\/chatbots\/([0-9a-f-]+)$/)
    const chatbotId = page.url().split('/chatbots/')[1]

    // A new chatbot starts Draft, and ChatbotsController's public-info/chat/stream/webhook
    // routes now 404 on anything but Active (the actual enforcement behind the Publish button —
    // previously cosmetic, see ChatbotsController.cs). Publish it so the widget below is really
    // exercising the "published" path, not a gap where Draft was reachable anyway.
    await page.getByRole('button', { name: 'Publish' }).click()
    await expect(page.getByText('Chatbot published')).toBeVisible()

    await withDemoPage(browser, chatbotId, async (demoPage) => {
      await demoPage.getByLabel('Open chat').click()
      await expect(demoPage.getByText('Demo Bot')).toBeVisible()
      await expect(demoPage.getByRole('dialog', { name: 'Demo Bot conversation' })).toBeVisible()

      // The native file picker stays out of the composer; visitors choose files through the
      // accessible attach action inside the widget's shadow root.
      const attachButton = demoPage.getByLabel('Attach a file')
      await expect(attachButton).toBeVisible()
      const nativePicker = await demoPage.locator('#r2wai-widget-host').evaluate((host) => {
        const picker = host.shadowRoot?.querySelector<HTMLInputElement>('input[type="file"]')
        return picker ? getComputedStyle(picker).display : null
      })
      expect(nativePicker).toBe('none')

      // A word-count constraint ("say hello in exactly 3 words") sends small
      // reasoning models like qwen3 into pathological over-thinking (~90s of
      // self-correction, observed directly against Ollama) — long enough to
      // blow the backend's own request timeout before any token streams
      // back. A plain, unconstrained prompt keeps the model's thinking phase
      // to a few seconds.
      await demoPage.getByPlaceholder('Type a message…').fill('Reply with just the word OK and nothing else.')
      await demoPage.getByRole('button', { name: 'Send' }).click()

      // Wait for a non-empty bot reply to land via the real SSE stream.
      await expect(async () => {
        const lastBubbleText = await demoPage.evaluate(() => {
          const host = document.getElementById('r2wai-widget-host')
          const bubbles = host?.shadowRoot?.querySelectorAll('.bubble.bot')
          const last = bubbles?.[bubbles.length - 1]
          return { text: last?.textContent ?? '', isError: last?.classList.contains('error') ?? false }
        })
        expect(lastBubbleText.isError).toBe(false)
        expect(lastBubbleText.text.trim().length).toBeGreaterThan(0)
      }).toPass({ timeout: 60_000 })
    })

    // Clean up the seeded chatbot.
    await page.goto(`/chatbots/${chatbotId}`)
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Chatbot deleted')).toBeVisible()
  })

  // Proves the origin allowlist (Chatbot.AllowedOrigins, ChatbotsController's
  // IsRequestOriginAllowed) actually blocks a real browser end to end — not just that the pure
  // parse/match logic is correct in isolation (see ChatbotOriginPolicyTests), and not just that
  // curl with a forged Origin header gets a 403 (a real browser is the one caller whose Origin
  // header can't be forged, which is the entire point of this control).
  test('a chatbot restricted to a different origin refuses to load on this one', async ({ page, browser }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/chatbots')
    const uniqueName = `E2E Restricted Widget Chatbot ${Date.now()}`
    await page.getByRole('button', { name: 'New Chatbot' }).click()
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page).toHaveURL(/\/chatbots\/([0-9a-f-]+)$/)
    const chatbotId = page.url().split('/chatbots/')[1]

    // Restrict to a domain that is NOT localhost:4173 (the demo page below), then publish.
    await page.getByLabel('Allowed embed origins').fill('https://only-this-domain.example')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await expect(page.getByText('Chatbot saved')).toBeVisible()
    await page.getByRole('button', { name: 'Publish' }).click()
    await expect(page.getByText('Chatbot published')).toBeVisible()

    await withDemoPage(browser, chatbotId, async (demoPage) => {
      await demoPage.getByLabel('Open chat').click()
      await expect(demoPage.getByText('Could not load this chatbot.')).toBeVisible()
    })

    // Clean up the seeded chatbot.
    await page.goto(`/chatbots/${chatbotId}`)
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Chatbot deleted')).toBeVisible()
  })
})
