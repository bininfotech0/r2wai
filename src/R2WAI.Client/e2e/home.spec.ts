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

    await page.getByRole('button', { name: '+ Agent' }).click()
    await expect(page.getByRole('heading', { name: 'Create AI Assistant' })).toBeVisible()
    await page.keyboard.press('Escape')

    await page.getByPlaceholder('Describe what you want R2WAI to create…').fill('E2E home prompt text')
    await page.getByRole('button', { name: 'Create', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'What would you like to create?' })).toBeVisible()

    await page.getByRole('dialog').getByText('AI Assistant', { exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Create AI Assistant' })).toBeVisible()
    await expect(page.getByRole('dialog').getByText('E2E home prompt text')).toBeVisible()
  })

  test('templates open the AI draft with their description, and the numbers moved to Monitor', async ({ page }) => {
    await loginAsAdmin(page)
    await expect(page.getByRole('heading', { name: 'What would you like to build?' })).toBeVisible()

    await page.getByRole('button', { name: 'Start from the Citizen services template' }).click()
    await expect(page.getByRole('heading', { name: 'Create AI Assistant' })).toBeVisible()
    await expect(page.getByText(/citizen services agent that explains government scheme eligibility/)).toBeVisible()
    await page.keyboard.press('Escape')

    await page.getByRole('link', { name: /Usage and performance/ }).click()
    await expect(page).toHaveURL(/\/monitor$/)
    await expect(page.getByText('Tokens used')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Agent performance' })).toBeVisible()
  })

  // Copilot Studio-style shell: an icon-over-label rail, a More menu for Workspaces/Settings/secondary
  // pages, and the workspace switcher in the top bar. Tools & APIs stays reachable only by the
  // SuperAdmin persona the seeded admin account holds (CapabilitiesController is SystemAdmin-only).
  test('rail, More menu and workspace switcher reach every destination', async ({ page }) => {
    await loginAsAdmin(page)
    await expect(page).toHaveTitle('R2WAI Studio')

    const rail = page.getByRole('navigation', { name: 'Main navigation' })
    await expect(rail.getByRole('link')).toHaveCount(7) // evaluateAll below does not auto-wait
    const railLabels = await rail.getByRole('link').evaluateAll((links) => links.map((l) => l.getAttribute('aria-label')))
    expect(railLabels).toEqual(['Home', 'Agents', 'Knowledge', 'Automations', 'Connections', 'Publish', 'Activity'])
    await expect(rail.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page')

    // More holds Workspaces, Settings and secondary pages.
    await rail.getByRole('button', { name: 'More' }).click()
    await expect(page.getByRole('menuitem', { name: 'AI Models' })).toBeVisible()
    await page.getByRole('menuitem', { name: 'Workspaces' }).click()
    await expect(page).toHaveURL(/\/workspaces$/)
    await expect(page.getByRole('heading', { name: 'Workspaces' })).toBeVisible()

    // The switcher shows the active workspace and links to management.
    const switcher = page.getByRole('button', { name: /^Workspace: .+\. Change workspace$/ })
    await expect(switcher).toBeVisible()
    await switcher.click()
    await expect(page.getByRole('menuitem', { name: 'Manage workspaces' })).toBeVisible()
    await page.keyboard.press('Escape')

    // Connections tabs; the Connections rail item stays lit on every tab.
    await rail.getByRole('link', { name: 'Connections' }).click()
    await expect(page).toHaveURL(/\/connections$/)
    const tabs = page.getByRole('tablist', { name: 'Workspaces sections' })
    for (const label of ['Workspaces', 'Connections', 'Integrations', 'MCP Servers', 'Tools & APIs']) {
      await expect(tabs.getByRole('tab', { name: label })).toBeVisible()
    }
    await tabs.getByRole('tab', { name: 'Tools & APIs' }).click()
    await expect(page).toHaveURL(/\/tools$/)
    await expect(rail.getByRole('link', { name: 'Connections' })).toHaveAttribute('aria-current', 'page')
    await expect(page).toHaveTitle('Tools & APIs · R2WAI Studio')
  })
})
