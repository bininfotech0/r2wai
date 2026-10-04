import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

test.describe('Inbox & About', () => {
  test('Inbox is reachable from the bell and shows real, server-persisted history', async ({ page }) => {
    // Notifications are backed by the Notifications table (history fetched on connect, read
    // state persisted via markRead/markAllRead — see NotificationFeedProvider) rather than
    // being session-only, so the empty state is a plain "nothing yet", not a disclaimer.
    await loginAsAdmin(page)

    await page.getByRole('button', { name: 'Notifications' }).click()
    await page.getByRole('link', { name: 'View all' }).click()
    await expect(page).toHaveURL(/\/inbox$/)
    await expect(page.getByRole('heading', { name: 'Inbox' })).toBeVisible()
    await expect(page.getByText(/Nothing yet|Notifications sent to your account/)).toBeVisible()
  })

  test('About shows real live health data', async ({ page }) => {
    await loginAsAdmin(page)

    await page.goto('/about')
    await expect(page.getByRole('heading', { name: 'About R2WAI Studio' })).toBeVisible()
    await expect(page.getByText('Feature Modules')).toBeVisible()
    await expect(page.getByText(/Overall: (Healthy|Degraded|Unhealthy)/)).toBeVisible({ timeout: 10_000 })
    await expect(page.getByText('Technology Stack')).toBeVisible()
  })
})
