import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// DoD pass (Phase 12) flagged this as a real, missing gap — AutomationDetailPage/
// AutomationBuilderPage tracked `isDirty` only to gate the Save button, never to warn on
// navigating away. Fixed via a shared useUnsavedChangesGuard (useBlocker + beforeunload).
test('Automation Detail: unsaved changes are guarded on in-app navigation', async ({ page }) => {
  test.setTimeout(60_000)
  await loginAsAdmin(page)

  await page.goto('/automations')
  await page.getByRole('button', { name: 'New Automation' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Unsaved ${Date.now()}`
  const createDialog = page.getByRole('dialog')
  await createDialog.getByLabel('Name').fill(uniqueName)
  await createDialog.getByRole('button', { name: 'Create' }).click()
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  // Dirty the page without saving.
  await page.getByLabel('Trigger').selectOption('Schedule')
  await expect(page.getByRole('button', { name: 'Save Draft' })).toBeEnabled()

  // Two DIFFERENT real elements that both navigate to /automations — deliberately not reusing
  // the same locator across both clicks, since re-clicking the identical element right after a
  // window.confirm() cycle (which blocks the main thread synchronously) is flaky in Playwright
  // regardless of `force`. The page's own back link (role "link" since component={RouterLink}
  // keeps the native <a> role) is scoped to <main> — { name: 'Automations' } alone would also
  // substring-match the sidebar's (Legacy section) nav link as well as the page's own link.
  const backLink = page.getByRole('main').getByRole('link', { name: 'Automations', exact: true })
  const sidebarLink = page.locator('.MuiDrawer-paper').getByRole('link', { name: 'Automations', exact: true })

  let acceptNext = false
  page.on('dialog', (dialog) => {
    expect(dialog.type()).toBe('confirm')
    expect(dialog.message()).toContain('unsaved changes')
    void (acceptNext ? dialog.accept() : dialog.dismiss())
  })

  // Cancel the navigation-away prompt — should stay on the same page, still dirty.
  await backLink.click()
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Save Draft' })).toBeEnabled()

  // Accept this time, via the sidebar's equivalent link — real navigation should proceed.
  acceptNext = true
  await sidebarLink.click()
  await expect(page).toHaveURL(/\/automations$/)

  // Clean up.
  await page.getByPlaceholder('Search automations…').fill(uniqueName)
  await page.getByRole('gridcell', { name: uniqueName }).click()
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Delete' }).last().click()
})
