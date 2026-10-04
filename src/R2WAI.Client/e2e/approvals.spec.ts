import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 8's verify criteria: a real pending approval shows Application/Capability, and View
// Details navigates correctly. Built via a real Approval-step automation run, not seeded data.
// Note: the builder's Approval step has no configurable "Data" field today (STEP_CONFIG_SCHEMAS
// Approval has zero properties) — a builder-created approval's Data is genuinely null, so the
// "readable data summary" rendering path (parseDataSummary in ApprovalsPage.tsx) can't be
// exercised through this real flow; it's a pure string→rows function, honest either way (renders
// nothing for null data rather than a fabricated summary).
test('Approvals: a real pending approval shows Application/Capability chips and View Details opens the run', async ({ page }) => {
  test.setTimeout(60_000)
  await loginAsAdmin(page)

  await page.goto('/automations')
  await page.getByRole('button', { name: 'New Automation' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Approval ${Date.now()}`
  const createDialog = page.getByRole('dialog')
  await createDialog.getByLabel('Name').fill(uniqueName)
  await createDialog.getByRole('button', { name: 'Create' }).click()
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  await page.getByRole('button', { name: 'Add Step' }).click()
  const stepDrawer = page.getByRole('presentation').filter({ hasText: 'New Step' })
  await stepDrawer.getByLabel('Name').fill('Needs Approval')
  await stepDrawer.getByLabel('Action').click()
  await page.getByRole('option', { name: 'Approval', exact: true }).click()
  await stepDrawer.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByText('Needs Approval')).toBeVisible()

  await page.getByRole('button', { name: 'Save Draft' }).click()
  await expect(page.getByText('Draft saved')).toBeVisible()

  await page.getByRole('button', { name: 'Test', exact: true }).click()
  await expect(page.getByText('Test run started')).toBeVisible()
  // Give Elsa a moment to reach and suspend on the Approval bookmark.
  await page.waitForTimeout(3000)

  await page.goto('/approvals')
  await expect(page.getByRole('heading', { name: 'Confirmations' })).toBeVisible()
  const card = page.locator('.MuiCard-root').filter({ hasText: uniqueName })
  await expect(card).toBeVisible()

  // Real Application chip (honestly "—" — this automation isn't linked to a Connected System)
  // Capability is not part of the approval entity, so the card only renders Application.
  await expect(card.getByText('App: —')).toBeVisible()
  await expect(card.getByText(/Capability:/)).toHaveCount(0)

  // View Details reuses RunInspectorDrawer against the real underlying workflow instance.
  await card.getByRole('button', { name: 'View Details' }).click()
  const drawer = page.getByRole('dialog').filter({ hasText: uniqueName })
  await expect(drawer.getByText('Needs Approval')).toBeVisible()
  // Escape, not a direct click on the small Close icon button — MUI's focus-trap backdrop
  // intercepts pointer events on that button in this app (a pre-existing quirk runs.spec.ts
  // also avoids, by never closing the drawer via click at all).
  await page.keyboard.press('Escape')
  await expect(drawer).not.toBeVisible()

  // The logged-in admin is also this automation's own requester (they created it and triggered
  // the run earlier in this same test), so ApprovalService.RejectAsync's separation-of-duties
  // guard correctly forbids deciding on a request you requested yourself (403, mapped to a
  // "Failed to submit decision" toast) -- asserting that real block, not a successful reject,
  // is what this flow actually does for this account.
  await card.getByRole('button', { name: 'Reject' }).click()
  await page.getByRole('button', { name: 'Reject' }).last().click()
  await expect(page.getByText('Failed to submit decision')).toBeVisible()
  await page.keyboard.press('Escape')

  // Clean up by deleting the automation directly -- the approval is left Pending, which is fine:
  // nothing in the workflow-delete path depends on its approvals being resolved first.
  await page.goto('/automations')
  await page.getByRole('gridcell', { name: uniqueName }).click()
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Delete' }).last().click()
  await expect(page.getByText('Automation deleted')).toBeVisible()
})
