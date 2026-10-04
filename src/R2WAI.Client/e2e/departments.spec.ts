import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Departments is Phase 4's reference CRUD area — proves DataTable, FormDialog,
// ConfirmDeleteDialog, and cache invalidation end to end. Retire this spec
// only once its replacement in a later consolidated Manage/Governance area
// (if any) is independently verified done — see the migration plan.
test.describe('Departments CRUD', () => {
  test('create, edit, and delete a department', async ({ page }) => {
    await loginAsAdmin(page)
    await page.goto('/departments')
    await expect(page.getByRole('heading', { name: 'Departments' })).toBeVisible()

    // Field lookups are scoped to the open dialog — the DataGrid behind it
    // has its own "Name column menu" etc. buttons that also match a bare
    // getByLabel('Name') query.
    const dialog = page.getByRole('dialog')
    const uniqueName = `E2E Test Department ${Date.now()}`

    // Create
    await page.getByRole('button', { name: 'New Department' }).click()
    await dialog.getByLabel('Name').fill(uniqueName)
    await dialog.getByLabel('Code').fill(`E2E${Date.now()}`)
    await dialog.getByLabel('Description').fill('Created by Playwright')
    await dialog.getByRole('button', { name: 'Create' }).click()

    await expect(page.getByText('Department created')).toBeVisible()
    // Search first — a fixed/unscoped name would rely on the new row landing on the DataGrid's
    // default first page, which many prior E2E runs' accumulated real (not cleaned-up) data can
    // push it past.
    await page.getByPlaceholder('Search departments…').fill(uniqueName)
    await expect(page.getByRole('gridcell', { name: uniqueName })).toBeVisible()

    // Edit
    const row = page.getByRole('row', { name: new RegExp(uniqueName) })
    await row.getByLabel('Edit').click()
    const updatedName = `${uniqueName} (Updated)`
    await dialog.getByLabel('Name').fill(updatedName)
    await dialog.getByRole('button', { name: 'Save changes' }).click()

    await expect(page.getByText('Department updated')).toBeVisible()
    await page.getByPlaceholder('Search departments…').fill(updatedName)
    await expect(page.getByRole('gridcell', { name: updatedName })).toBeVisible()

    // Delete
    const updatedRow = page.getByRole('row', { name: new RegExp(updatedName.replace('(', '\\(').replace(')', '\\)')) })
    await updatedRow.getByLabel('Delete').click()
    await dialog.getByRole('button', { name: 'Delete' }).click()

    await expect(page.getByText('Department deleted')).toBeVisible()
    await expect(page.getByRole('gridcell', { name: updatedName })).not.toBeVisible()
  })
})
