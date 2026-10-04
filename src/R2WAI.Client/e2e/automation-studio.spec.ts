import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Full CRUD + step-list authoring, per Phase 7's verify criteria. Manual
// creation keeps this fast — the AI-draft path shares the same draft/create
// endpoints Phase 6's AI-generate flow already proved live.
test('Automation Studio: create, add step, save draft, test, publish, delete', async ({ page }) => {
  test.setTimeout(120_000)

  await loginAsAdmin(page)
  await page.goto('/automations')
  await expect(page.getByRole('heading', { name: 'Automations' })).toBeVisible()

  // Create (manual path)
  await page.getByRole('button', { name: 'New Automation' }).click()
  await page.getByRole('button', { name: /configure manually/i }).click()
  const uniqueName = `E2E Automation ${Date.now()}`
  const createDialog = page.getByRole('dialog')
  await createDialog.getByLabel('Name').fill(uniqueName)
  await createDialog.getByRole('button', { name: 'Create' }).click()

  await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

  // Add a step via the Dynamic Step Editor
  await page.getByRole('button', { name: 'Add Step' }).click()
  const stepDrawer = page.getByRole('presentation').filter({ hasText: 'New Step' })
  await stepDrawer.getByLabel('Name').fill('Send Welcome Email')
  await stepDrawer.getByLabel('Action').click()
  await page.getByRole('option', { name: 'Email' }).click()
  await stepDrawer.getByLabel('To').fill('newhire@example.com')
  await stepDrawer.getByLabel('Subject').fill('Welcome!')
  await stepDrawer.getByRole('button', { name: 'Save' }).click()

  await expect(page.getByText('Send Welcome Email')).toBeVisible()

  // Save Draft
  await page.getByRole('button', { name: 'Save Draft' }).click()
  await expect(page.getByText('Draft saved')).toBeVisible()

  // Test (execute)
  await page.getByRole('button', { name: 'Test', exact: true }).click()
  await expect(page.getByText('Test run started')).toBeVisible()

  // Publish
  await page.getByRole('button', { name: 'Publish', exact: true }).click()
  await page.getByRole('button', { name: /^Publish \d/ }).click()
  await expect(page.getByText('Automation published')).toBeVisible()

  // Delete
  await page.getByRole('button', { name: 'Delete' }).click()
  await page.getByRole('button', { name: 'Delete' }).last().click()
  await expect(page.getByText('Automation deleted')).toBeVisible()
  await expect(page).toHaveURL(/\/automations$/)
})
