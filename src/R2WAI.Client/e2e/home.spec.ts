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

  // Connections is one sidebar destination; Integrations, MCP Servers and Tools & APIs are tabs on it.
  // Tools & APIs stays reachable only by the SuperAdmin persona the seeded admin account holds
  // (CapabilitiesController is still SystemAdmin-only — nav placement only, not an authorization change).
  test('sets a per-route document title, and groups connection destinations as tabs', async ({ page }) => {
    await loginAsAdmin(page)
    await expect(page).toHaveTitle('R2WAI Studio')

    const sidebar = page.getByRole('group', { name: 'Connections navigation' })
    await expect(sidebar.getByRole('link', { name: 'Connections' })).toBeVisible()
    for (const label of ['Integrations', 'MCP Servers', 'Tools & APIs']) {
      await expect(sidebar.getByRole('link', { name: label })).toHaveCount(0)
    }
    await expect(page.getByRole('group', { name: 'Settings navigation' }).getByRole('link', { name: 'AI Models' })).toBeVisible()
    const publishFollowsConnections = await page.locator('a[href="/workspaces"]').first().evaluate((connections) => {
      const publish = document.querySelector('a[href="/chatbots"]')
      return publish !== null && Boolean(connections.compareDocumentPosition(publish) & Node.DOCUMENT_POSITION_FOLLOWING)
    })
    expect(publishFollowsConnections).toBe(true)
    await expect(page.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page')

    await sidebar.getByRole('link', { name: 'Connections' }).click()
    await expect(page).toHaveURL(/\/workspaces$/)
    const tabs = page.getByRole('tablist', { name: 'Connected Systems sections' })
    for (const label of ['Connected Systems', 'Integrations', 'MCP Servers', 'Tools & APIs']) {
      await expect(tabs.getByRole('tab', { name: label })).toBeVisible()
    }

    await tabs.getByRole('tab', { name: 'Tools & APIs' }).click()
    await expect(page).toHaveURL(/\/tools$/)
    await expect(tabs.getByRole('tab', { name: 'Tools & APIs' })).toHaveAttribute('aria-selected', 'true')
    // The group's sidebar item stays highlighted on every one of its tabs.
    await expect(sidebar.getByRole('link', { name: 'Connections' })).toHaveAttribute('aria-current', 'page')
    await expect(page).toHaveTitle('Tools & APIs · R2WAI Studio')
  })
})
