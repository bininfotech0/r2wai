import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

test.describe('Command palette & shortcuts', () => {
  test('Ctrl+K opens the palette, an action command runs, and a nav command navigates', async ({ page }) => {
    await loginAsAdmin(page)
    await expect(page.getByRole('button', { name: /Search assistants/ })).toBeVisible()

    await page.keyboard.press('Control+K')
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()

    // Action command — toggling theme is directly observable via localStorage.
    await page.getByText('Switch to dark mode').click()
    await expect.poll(() => page.evaluate(() => localStorage.getItem('r2wai_dark_mode'))).toBe('true')

    // Nav command.
    await page.keyboard.press('Control+K')
    await page.getByPlaceholder(/Search assistants/).fill('Settings')
    await page.getByRole('option', { name: 'Settings', exact: true }).click()
    await expect(page).toHaveURL(/\/settings$/)
  })

  test('? opens the keyboard shortcuts help', async ({ page }) => {
    await loginAsAdmin(page)
    await expect(page.getByRole('button', { name: /Search assistants/ })).toBeVisible()

    await page.locator('body').press('?')
    await expect(page.getByRole('heading', { name: 'Keyboard shortcuts' })).toBeVisible()
    await expect(page.getByText('Open command palette (search & actions)')).toBeVisible()
  })
})
