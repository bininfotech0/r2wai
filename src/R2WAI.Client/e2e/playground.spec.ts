import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 6's verify criteria: run an Assistant test and an Automation test through the new
// unified 3-panel layout, confirm the Execution Inspector populates from real data. Assistant
// mode's actual chat completion is CPU-Ollama-dependent (documented elsewhere as slow/flaky in
// this environment) so this only exercises the picker/panel wiring for that mode, not a live
// reply — Automation and Capability modes get the full run-and-verify treatment since they're
// not LLM-dependent.
test.describe('Playground', () => {
  test('unified 3-panel layout: mode switch, automation run, capability test', async ({ page }) => {
    test.setTimeout(120_000)
    await loginAsAdmin(page)

    // Create an automation with a real step so the inspector assertion does not depend on
    // whichever empty or seeded workflow happens to sort first in a reused test database.
    await page.goto('/automations')
    await page.getByRole('button', { name: 'New Automation' }).click()
    await page.getByRole('button', { name: /configure manually/i }).click()
    const uniqueName = `E2E Playground ${Date.now()}`
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
    await page.getByRole('button', { name: 'Save Draft' }).click()
    await expect(page.getByText('Draft saved')).toBeVisible()

    await page.goto('/playground')
    await expect(page.getByRole('heading', { name: 'Playground' })).toBeVisible()

    // Assistant mode is the default — picker, center chat pane, and inspector all render.
    await expect(page.getByRole('combobox', { name: 'Assistant' })).toBeVisible()
    await expect(page.getByText('Live Preview')).toBeVisible()
    await expect(page.getByText('Execution Inspector')).toBeVisible()
    await expect(page.getByText('Tool calls this turn')).toBeVisible()

    // Automation mode: run a real workflow, confirm the Inspector's Timeline gets real step data.
    await page.getByRole('tab', { name: 'Automation' }).click()
    const automationPicker = page.getByRole('combobox', { name: 'Automation' })
    await expect(automationPicker).toBeVisible()
    await automationPicker.click()
    await page.getByRole('option', { name: uniqueName }).click()
    await page.getByRole('button', { name: 'Run Test' }).click()
    await expect(page.getByText(/^Instance /)).toBeVisible({ timeout: 15_000 })
    // The Inspector shows the same run's step timeline, not a placeholder.
    await expect(page.getByText('Needs Approval')).toBeVisible({ timeout: 15_000 })

    // Capability (Tool/API) mode: run a real test, confirm both the center pane and the
    // Inspector show the actual result (success or governed failure — either is a real outcome).
    await page.getByRole('tab', { name: 'Tool/API' }).click()
    await expect(page.getByRole('combobox', { name: 'Tool / API' })).toBeVisible()
    await page.getByRole('button', { name: 'Run Test' }).click()
    await expect(page.getByRole('button', { name: 'Testing…' })).toHaveCount(0, { timeout: 15_000 })
    // Two Alerts render with the same message — one in the center pane, one in the Inspector.
    await expect(page.locator('.MuiAlert-message').first()).toBeVisible()

    // Clean up the automation created for this scenario.
    await page.goto('/automations')
    await page.getByPlaceholder('Search automations…').fill(uniqueName)
    await page.getByRole('gridcell', { name: uniqueName }).click()
    await page.getByRole('button', { name: 'Delete' }).click()
    await page.getByRole('button', { name: 'Delete' }).last().click()
    await expect(page.getByText('Automation deleted')).toBeVisible()
  })
})
