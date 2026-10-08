import { test, expect } from './fixtures/cleanup'
import { loginAsAdmin } from './fixtures/auth'

// Phase 11: sidebar becomes an overlay below `md`, a fixed bottom nav takes over below `sm`, chat
// surfaces (CopilotPanel) go full-width below `sm`, and the Automation Builder canvas goes
// view-only below `sm` rather than attempting touch-drag support (brief §21).

test.describe('Responsive layout', () => {
  test('desktop (>=md): persistent sidebar, no bottom nav', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 })
    await loginAsAdmin(page)

    await expect(page.getByRole('link', { name: 'Home' })).toBeVisible()
    await expect(page.locator('.MuiBottomNavigation-root')).toHaveCount(0)

    await page.getByRole('button', { name: 'Collapse navigation' }).click()
    await expect(page.getByRole('button', { name: 'Expand navigation' })).toBeVisible()
    await page.reload()
    await expect(page.getByRole('button', { name: 'Expand navigation' })).toBeVisible()
  })

  test('respects the operating system reduced-motion preference', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await loginAsAdmin(page)

    const motionStyles = await page.evaluate(() => {
      const probe = document.createElement('div')
      probe.style.cssText = 'transition-duration: 5s; animation-duration: 5s; animation-iteration-count: infinite;'
      document.body.append(probe)
      const computed = getComputedStyle(probe)
      const styles = {
        transitionDuration: Number.parseFloat(computed.transitionDuration),
        animationDuration: Number.parseFloat(computed.animationDuration),
        animationIterationCount: computed.animationIterationCount,
      }
      probe.remove()
      return styles
    })
    expect(motionStyles.transitionDuration).toBeLessThan(0.001)
    expect(motionStyles.animationDuration).toBeLessThan(0.001)
    expect(motionStyles.animationIterationCount).toBe('1')
  })

  test('tablet (md range): sidebar starts closed as an overlay, toggled via the menu button', async ({ page }) => {
    await page.setViewportSize({ width: 800, height: 900 })
    await loginAsAdmin(page)

    const tabletDrawer = page.locator('.MuiDrawer-root.MuiDrawer-modal')
    await expect(tabletDrawer.getByRole('link', { name: 'Home' })).not.toBeVisible()
    await expect(page.locator('.MuiBottomNavigation-root')).toHaveCount(0)

    await page.getByRole('button', { name: 'Open navigation' }).click()
    await expect(tabletDrawer.getByRole('link', { name: 'Home' })).toBeVisible()

    // Overlay drawer closes on navigation (temporary variant).
    await page.getByRole('link', { name: 'Agents' }).click()
    await expect(page).toHaveURL(/\/assistants$/)
    await expect(tabletDrawer.getByRole('link', { name: 'Home' })).not.toBeVisible()
  })

  test('mobile (<sm): bottom nav replaces the sidebar for quick access', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 800 })
    await loginAsAdmin(page)

    const bottomNav = page.locator('.MuiBottomNavigation-root')
    await expect(bottomNav).toBeVisible()
    await expect(bottomNav.getByRole('button', { name: 'Home' })).toBeVisible()
    await expect(bottomNav.getByRole('button', { name: 'Inbox' })).toBeVisible()
    await expect(bottomNav.getByRole('button', { name: 'Profile' })).toBeVisible()

    await bottomNav.getByRole('button', { name: 'Inbox' }).click()
    await expect(page).toHaveURL(/\/inbox$/)

    // Sidebar is still reachable via the menu icon, as a temporary overlay.
    await page.getByRole('button', { name: 'Open navigation' }).click()
    await expect(page.getByRole('link', { name: 'Home' })).toBeVisible()
    await page.keyboard.press('Escape')
  })

  test('mobile (<sm): AI Copilot panel goes full-width', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 800 })
    await loginAsAdmin(page)

    await page.getByRole('button', { name: 'Account menu' }).click()
    await page.getByRole('menuitem', { name: 'Open AI Copilot' }).click()
    const copilotPaper = page.locator('.MuiDrawer-paper').filter({ hasText: 'AI Copilot' })
    await expect(copilotPaper).toBeVisible()
    const box = await copilotPaper.boundingBox()
    expect(box?.width).toBeGreaterThan(360)
    await page.keyboard.press('Escape')
  })

  test('mobile (<sm): Automation Builder canvas is view-only', async ({ page, cleanup }) => {
    await loginAsAdmin(page)

    // Seed a minimal automation on desktop first (builder needs a real workflow to open).
    await page.goto('/automations')
    await page.getByRole('button', { name: 'New Automation' }).click()
    await page.getByRole('button', { name: /configure manually/i }).click()
    const uniqueName = `E2E Responsive Test ${Date.now()}`
    cleanup.add('automation', uniqueName)
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()
    await page.getByRole('link', { name: /Open Builder/i }).click()
    await expect(page).toHaveURL(/\/builder$/)

    await page.setViewportSize({ width: 375, height: 800 })
    await expect(page.getByText('View-only on mobile')).toBeVisible()
    // Palette and properties panels are hidden — nothing to drag or edit.
    await expect(page.getByText('STEP', { exact: false })).not.toBeVisible()

    await page.setViewportSize({ width: 1280, height: 900 })

    // Clean up.
    await page.goto('/automations')
    await page.getByPlaceholder('Search automations…').fill(uniqueName)
    await page.getByRole('gridcell', { name: uniqueName }).click()
    await page.getByRole('button', { name: 'Delete' }).click()
    await page.getByRole('button', { name: 'Delete' }).last().click()
  })
})
