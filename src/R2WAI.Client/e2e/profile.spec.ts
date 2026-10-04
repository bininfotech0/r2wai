import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'
import { generateTotpCode } from './fixtures/totp'

test.describe('Profile', () => {
  test('view and edit profile, then a real MFA enroll/disable round trip', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/profile')
    await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()

    // Edit profile — real PUT /auth/profile round trip. Reads the seeded
    // admin's real last name first and restores it at the end, since this is
    // a shared account other specs' loginAsAdmin depends on (unlike the
    // create-then-delete temp entities most other specs use).
    await page.getByRole('button', { name: 'Edit profile' }).click()
    const dialog = page.getByRole('dialog')
    const originalLastName = await dialog.getByLabel('Last name').inputValue()
    await dialog.getByLabel('Last name').fill('E2E-Edited')
    await dialog.getByRole('button', { name: 'Save changes' }).click()
    await expect(page.getByText('Profile updated')).toBeVisible()
    await expect(page.getByText('System E2E-Edited').first()).toBeVisible()

    await page.getByRole('button', { name: 'Edit profile' }).click()
    await dialog.getByLabel('Last name').fill(originalLastName)
    await dialog.getByRole('button', { name: 'Save changes' }).click()
    await expect(page.getByText('Profile updated')).toBeVisible()

    // MFA — real TOTP secret from the backend, a real computed code enables
    // it, then a second real code disables it. No mocking either call.
    await page.getByRole('button', { name: 'Enable 2FA' }).click()
    const secretField = page.getByLabel('Secret')
    await expect(secretField).not.toHaveValue('')
    const secret = await secretField.inputValue()

    await page.getByLabel('6-digit code').fill(generateTotpCode(secret))
    await page.getByRole('button', { name: 'Confirm & enable' }).click()
    await expect(page.getByText('Two-factor authentication enabled')).toBeVisible()
    await expect(page.getByText('Enabled', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Disable 2FA' }).click()
    await page.getByLabel('6-digit code').fill(generateTotpCode(secret))
    await page.getByRole('button', { name: 'Confirm & disable' }).click()
    await expect(page.getByText('Two-factor authentication disabled')).toBeVisible()
  })
})
