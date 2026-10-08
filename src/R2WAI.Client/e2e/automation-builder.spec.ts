import { test, expect } from './fixtures/cleanup'
import { loginAsAdmin } from './fixtures/auth'

// Phase 8's verify criteria: graph round-trips save->reload identically, and
// the builder is only reachable via the "Open Builder" link. Drag-and-drop
// from the palette isn't simulated here — graphConversion.test.ts already
// proves round-trip fidelity as pure logic; this test proves the REAL
// backend round-trip (save -> reload -> same data), which unit tests can't.
test('Advanced Automation Builder: canvas reflects real steps, edits round-trip through the API', async ({ page, cleanup }) => {
  test.setTimeout(60_000)

  await loginAsAdmin(page)

  // Seed a 2-step automation via the already-proven step-list editor (Phase 7).
  await page.goto('/automations')
  await page.getByRole('button', { name: 'New Automation' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Builder Test ${Date.now()}`
  cleanup.add('automation', uniqueName)
  const createDialog = page.getByRole('dialog')
  await createDialog.getByLabel('Name').fill(uniqueName)
  await createDialog.getByRole('button', { name: 'Create' }).click()
  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  await page.getByRole('button', { name: 'Add Step' }).click()
  let stepDrawer = page.getByRole('presentation').filter({ hasText: 'New Step' })
  await stepDrawer.getByLabel('Name').fill('Notify Manager')
  await stepDrawer.getByLabel('Action').click()
  await page.getByRole('option', { name: 'Email' }).click()
  await stepDrawer.getByLabel('Subject').fill('Original Subject')
  await stepDrawer.getByRole('button', { name: 'Save' }).click()
  await page.getByRole('button', { name: 'Save Draft' }).click()
  await expect(page.getByText('Draft saved')).toBeVisible()

  // Open the Advanced Builder via the explicit link (never forced).
  await page.getByRole('link', { name: /Open Builder/i }).click()
  await expect(page).toHaveURL(/\/builder$/)

  // The seeded step renders as a real canvas node with its step type.
  const canvasNode = page.locator('.react-flow__node').filter({ hasText: 'Notify Manager' })
  await expect(canvasNode).toBeVisible()
  await expect(canvasNode.getByText('Email')).toBeVisible()

  // Edit its config through the properties panel and save.
  await canvasNode.click()
  const propertiesPanel = page.locator('text=Type: Email').locator('..')
  await propertiesPanel.getByLabel('Subject').fill('Updated Subject')
  await page.getByRole('button', { name: 'Save', exact: true }).click()
  await expect(page.getByText('Automation saved')).toBeVisible()

  // Reload from scratch — this only passes if the edit actually persisted
  // through updateWorkflow -> the real API -> getWorkflow on the next load.
  await page.reload()
  const reloadedNode = page.locator('.react-flow__node').filter({ hasText: 'Notify Manager' })
  await expect(reloadedNode).toBeVisible()
  await reloadedNode.click()
  const reloadedPanel = page.locator('text=Type: Email').locator('..')
  await expect(reloadedPanel.getByLabel('Subject')).toHaveValue('Updated Subject')

  // Clean up — search first so the target row isn't scrolled out of the
  // MUI DataGrid's virtualized viewport (only visible rows exist in the DOM).
  await page.goto('/automations')
  await page.getByPlaceholder('Search automations…').fill(uniqueName)
  await page.getByRole('gridcell', { name: uniqueName }).click()
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Delete' }).last().click()
})
