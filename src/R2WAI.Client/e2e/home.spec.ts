import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// The Home banner's create box and quick-create chips were built in Phase 2
// but shipped disabled — nothing wired them to the real creation flows that
// landed in Phase 6/7. Fixed later: they now open the real dialogs, and the
// typed description carries through the Universal Create picker into
// whichever type the user picks.
test.describe('Home', () => {
  test('quick-create opens the real Assistant dialog, and Create carries the typed prompt through the picker', async ({ page }) => {
    test.setTimeout(30_000)
    await loginAsAdmin(page)

    await page.getByRole('button', { name: '+ Assistant' }).click()
    await expect(page.getByRole('heading', { name: 'Create AI Assistant' })).toBeVisible()
    await page.keyboard.press('Escape')

    await page.getByPlaceholder('Describe what you want R2WAI to create…').fill('E2E home prompt text')
    await page.getByRole('button', { name: 'Create', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'What would you like to create?' })).toBeVisible()

    await page.getByRole('dialog').getByText('AI Assistant', { exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Create AI Assistant' })).toBeVisible()
    await expect(page.getByText('E2E home prompt text')).toBeVisible()
  })

  // Tools & APIs stays reachable only by the SuperAdmin persona the seeded admin account holds
  // (CapabilitiesController is still SystemAdmin-only — nav placement only, not an authorization change).
  test('sets a per-route document title, and groups connection destinations together', async ({ page }) => {
    await loginAsAdmin(page)
    await expect(page).toHaveTitle('R2WAI Studio')

    // The primary nav uses the product labels directly. Integrations and Tools & APIs remain
    // secondary links beneath Connections and come before Publish.
    const connectionsGroup = page.getByRole('group', { name: 'Connections navigation' })
    await expect(connectionsGroup).toBeVisible()
    for (const label of ['Connections', 'Integrations', 'MCP Servers', 'Tools & APIs', 'AI Models']) {
      await expect(page.getByRole('link', { name: label })).toBeVisible()
    }
    const publishFollowsConnections = await page.locator('a[href="/workspaces"]').evaluate((connections) => {
      const integrations = document.querySelector('a[href="/integrations"]')
      const tools = document.querySelector('a[href="/tools"]')
      const publish = document.querySelector('a[href="/chatbots"]')
      return integrations !== null && tools !== null && publish !== null
        && Boolean(connections.compareDocumentPosition(integrations) & Node.DOCUMENT_POSITION_FOLLOWING)
        && Boolean(tools.compareDocumentPosition(publish) & Node.DOCUMENT_POSITION_FOLLOWING)
    })
    expect(publishFollowsConnections).toBe(true)
    await expect(page.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page')

    await page.getByRole('link', { name: 'Tools & APIs' }).click()
    await expect(page).toHaveURL(/\/tools$/)
    await expect(page.getByRole('link', { name: 'Tools & APIs' })).toHaveAttribute('aria-current', 'page')
    await expect(page).toHaveTitle('Tools & APIs · R2WAI Studio')
  })
})
