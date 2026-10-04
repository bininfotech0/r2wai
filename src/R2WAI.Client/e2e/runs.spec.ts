import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 11's verify criteria (Runs half): a real automation run shows up in
// the unified Runs list and the Run Inspector renders its real step timeline
// and Technical Details. The Assistant-run half of the merge (Conversations)
// is exercised implicitly by every assistant chat test in this suite
// (Phase 5/6 already proved that path live) — not re-verified here to keep
// this spec focused on what's actually new: the unified list and inspector.
test('Runs: an executed automation appears in the unified list with a real step timeline', async ({ page }) => {
  test.setTimeout(60_000)
  await loginAsAdmin(page)

  await page.goto('/automations')
  await page.getByRole('button', { name: 'New Automation' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Run ${Date.now()}`
  const createDialog = page.getByRole('dialog')
  await createDialog.getByLabel('Name').fill(uniqueName)
  await createDialog.getByRole('button', { name: 'Create' }).click()
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  await page.getByRole('button', { name: 'Add Step' }).click()
  const stepDrawer = page.getByRole('presentation').filter({ hasText: 'New Step' })
  await stepDrawer.getByLabel('Name').fill('Log Something')
  await stepDrawer.getByLabel('Action').click()
  await page.getByRole('option', { name: 'Action', exact: true }).click()
  await stepDrawer.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByText('Log Something')).toBeVisible()

  await page.getByRole('button', { name: 'Save Draft' }).click()
  await expect(page.getByText('Draft saved')).toBeVisible()

  await page.getByRole('button', { name: 'Test', exact: true }).click()
  await expect(page.getByText('Test run started')).toBeVisible()

  // Give the Elsa engine a moment to actually run the (near-instant) step.
  await page.waitForTimeout(3000)

  await page.goto('/runs')
  await expect(page.getByRole('heading', { name: 'Executions' })).toBeVisible()
  await expect(page.getByRole('gridcell', { name: uniqueName })).toBeVisible()

  // Phase 7: the new columns render real values for this row — Application is honestly "—"
  // (this automation isn't linked to a Connected System), Duration is a real elapsed time
  // since the test run already completed, User is the real initiating admin's name.
  const row = page.locator('.MuiDataGrid-row').filter({ hasText: uniqueName })
  await expect(row).toContainText('—') // Application column, unlinked automation
  await expect(row).toContainText(/\d+(s|m)/) // Duration column, real elapsed time
  await expect(row).toContainText('System Administrator') // User column, real initiator

  // FilterBar (Phase 7): filters actually narrow the list.
  const statusFilter = page.getByRole('combobox', { name: 'Status' })
  await statusFilter.click()
  await page.getByRole('option', { name: 'Completed' }).click()
  await expect(page.getByRole('gridcell', { name: uniqueName })).toBeVisible()
  await statusFilter.click()
  await page.getByRole('option', { name: 'Any status' }).click()

  await page.getByRole('gridcell', { name: uniqueName }).click()
  const drawer = page.getByRole('dialog').filter({ hasText: 'Timeline' })
  await expect(drawer.getByText('Log Something')).toBeVisible()

  await drawer.getByText('Technical details').click()
  await expect(drawer.getByText('Semantic Kernel')).toBeVisible()
  await expect(drawer.getByText('Elsa')).toBeVisible()

  // Clean up.
  await page.goto(`/automations`)
  await page.getByRole('gridcell', { name: uniqueName }).click()
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Delete' }).last().click()
  await expect(page.getByText('Automation deleted')).toBeVisible()
})
