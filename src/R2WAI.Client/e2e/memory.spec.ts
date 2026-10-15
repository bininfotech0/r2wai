import { test, expect } from '@playwright/test'
import { loginAsAdmin, loginAsOtherTenantAdmin } from './fixtures/auth'

// Real round trips against /api/v1/memory: add, PII refusal, pin, pause, settings, isolation,
// delete. The seeded admin is shared with other specs, so everything this creates is uniquely
// named and removed again, and the one setting it flips is restored.
test.describe('Memory', () => {
  test('add, pin, pause and delete a memory; settings persist; PII is refused', async ({ page, browser }) => {
    test.setTimeout(90_000)
    const stamp = Date.now().toString(36)
    const content = `E2E ${stamp} prefers answers as short tables.`

    await loginAsAdmin(page)
    await page.goto('/memory')
    await expect(page.getByRole('heading', { name: 'Memory', exact: true })).toBeVisible()

    // PII is refused with the server's own reason, and nothing is saved.
    await page.getByRole('button', { name: 'Add memory' }).first().click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('What should agents remember?').fill('Call me on 9876543210')
    await dialog.getByRole('button', { name: 'Add memory' }).click()
    await expect(dialog.getByText("Memories can't contain Aadhaar or PAN numbers, phone numbers or email addresses.")).toBeVisible()
    await dialog.getByRole('button', { name: 'Cancel' }).click()

    // Add
    await page.getByRole('button', { name: 'Add memory' }).first().click()
    await dialog.getByLabel('What should agents remember?').fill(content)
    await dialog.getByLabel('Name (optional)').fill(`E2E Format ${stamp}`)
    await dialog.getByRole('button', { name: 'Add memory' }).click()
    await expect(page.getByText('Memory added')).toBeVisible()
    const row = page.getByRole('listitem', { name: content })
    await expect(row).toBeVisible()
    await expect(row.getByText(`e2e_format_${stamp}`)).toBeVisible()
    await expect(row.getByText(/Added by you/)).toBeVisible()

    // Another tenant's admin never sees it.
    const otherContext = await browser.newContext()
    const otherPage = await otherContext.newPage()
    await loginAsOtherTenantAdmin(otherPage)
    await otherPage.goto('/memory')
    await expect(otherPage.getByRole('heading', { name: 'Memory', exact: true })).toBeVisible()
    await expect(otherPage.getByText(content)).toHaveCount(0)
    await otherContext.close()

    // Pin, then pause
    await row.getByRole('button', { name: 'Pin memory' }).click()
    await expect(row.getByRole('button', { name: 'Unpin memory' })).toHaveAttribute('aria-pressed', 'true')
    await row.getByRole('button', { name: `Actions for ${content}` }).click()
    await page.getByRole('menuitem', { name: 'Pause' }).click()
    await expect(row.getByText('Paused', { exact: true })).toBeVisible()

    // A setting survives a reload, then is restored.
    const settingsSaved = () => page.waitForResponse((r) => r.url().endsWith('/api/v1/memory/settings') && r.ok())
    const learn = page.getByLabel('Learn from my chats')
    const wasOn = await learn.isChecked()
    let saved = settingsSaved()
    await learn.click()
    await saved
    await expect(learn).toBeChecked({ checked: !wasOn })
    await page.reload()
    await expect(page.getByLabel('Learn from my chats')).toBeChecked({ checked: !wasOn })
    saved = settingsSaved()
    await page.getByLabel('Learn from my chats').click()
    await saved

    // Delete
    await page.getByRole('listitem', { name: content }).getByRole('button', { name: `Actions for ${content}` }).click()
    await page.getByRole('menuitem', { name: 'Delete' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Memory deleted')).toBeVisible()
    await expect(page.getByRole('listitem', { name: content })).toHaveCount(0)
  })
})
