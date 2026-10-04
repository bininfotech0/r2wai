import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

test.describe('Monitor', () => {
  test('all tabs render real data, and a test case can be created, run, and deleted', async ({ page }) => {
    test.setTimeout(120_000)
    await loginAsAdmin(page)

    // Seed an assistant to attach a test case to.
    await page.goto('/assistants')
    await page.getByRole('button', { name: 'New Assistant' }).click()
    await page.getByRole('button', { name: /configure manually/i }).click()
    const assistantName = `E2E Monitor Assistant ${Date.now()}`
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(assistantName)
    await createDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page).toHaveURL(/\/assistants\/([0-9a-f-]+)$/)
    const assistantId = page.url().split('/assistants/')[1]
    await expect(page.getByRole('heading', { name: assistantName })).toBeVisible()

    await page.goto('/monitor')
    await expect(page.getByRole('heading', { name: 'Monitor' })).toBeVisible()

    // Overview — real stat cards, including Phase 9's new backlog metric and health panel
    // (aggregated from Integrations/Knowledge/Models' own existing status fields).
    await expect(page.getByText('Executions Today')).toBeVisible()
    await expect(page.getByText('AI Requests')).toBeVisible()
    await expect(page.getByText('Confirmation Backlog')).toBeVisible()
    // .last(): the sidebar's own "Platform Healthy" status text also lives in a MuiPaper-root
    // and substring-matches "Platform Health" — this panel's Paper is the other/later match.
    const healthPanel = page.locator('.MuiPaper-root').filter({ hasText: 'Platform Health' }).last()
    await expect(healthPanel).toBeVisible()
    await expect(healthPanel.getByText('Integrations', { exact: true })).toBeVisible()
    // exact: true -- HealthRow's own empty-state caption ("No knowledge bases yet") substring-
    // matches the unqualified label text and makes the locator ambiguous (strict-mode violation).
    await expect(healthPanel.getByText('Knowledge Bases', { exact: true })).toBeVisible()
    await expect(healthPanel.getByText('AI Models', { exact: true })).toBeVisible()

    // Errors.
    await page.getByRole('tab', { name: 'Errors' }).click()
    await expect(page.getByText(/read-only, no persisted error table/)).toBeVisible()

    // AI Operations.
    await page.getByRole('tab', { name: 'AI Operations' }).click()
    await expect(page.getByText('Recent AI Activity')).toBeVisible()

    // Audit Logs.
    await page.getByRole('tab', { name: 'Audit Logs' }).click()
    await expect(page.getByLabel('Entity type')).toBeVisible()

    // Reports — Phase 9 moved this off the primary tab row into the "QA & Testing" menu,
    // since it's QA regression tooling, not ops monitoring like Errors/Audit.
    await page.getByLabel('QA and Testing menu').click()
    await page.getByRole('menuitem', { name: 'Reports' }).click()
    await expect(page.getByRole('button', { name: /Generate Cost Report/ })).toBeVisible()

    // Usage Analytics — still a primary tab. "Assistants" is a StatCard label (plain caption
    // text, like its "Active Assistants" sibling), not a heading -- only the two chart panels
    // below the stat-card row ("Workflow Executions"/"Confirmations") render as real headings.
    await page.getByRole('tab', { name: 'Usage Analytics' }).click()
    await expect(page.getByText('Assistants', { exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Workflow Executions' })).toBeVisible()

    // Test Cases — full CRUD + run.
    await page.getByLabel('QA and Testing menu').click()
    await page.getByRole('menuitem', { name: 'Test Cases' }).click()
    await page.getByRole('button', { name: 'New Test Case' }).click()
    const tcDialog = page.getByRole('dialog')
    await tcDialog.getByLabel('Assistant').click()
    await page.getByRole('option', { name: assistantName }).click()
    const tcName = `E2E Test Case ${Date.now()}`
    await tcDialog.getByLabel('Name').fill(tcName)
    await tcDialog.getByLabel('Question').fill('Reply with just the word OK and nothing else.')
    await tcDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('Test case created')).toBeVisible()
    await expect(page.getByRole('gridcell', { name: tcName })).toBeVisible()

    await page.getByRole('row', { name: new RegExp(tcName) }).getByLabel('Run').click()
    await expect(page.getByText(/Run complete/)).toBeVisible({ timeout: 90_000 })

    // Test History — the run just completed should be visible with a detail dialog.
    await page.getByLabel('QA and Testing menu').click()
    await page.getByRole('menuitem', { name: 'Test History' }).click()
    await expect(page.getByRole('row').nth(1)).toBeVisible()
    await page.getByRole('row').nth(1).click()
    await expect(page.getByText(tcName)).toBeVisible()
    await page.keyboard.press('Escape')

    // Clean up.
    await page.getByLabel('QA and Testing menu').click()
    await page.getByRole('menuitem', { name: 'Test Cases' }).click()
    await page.getByRole('row', { name: new RegExp(tcName) }).getByLabel('Delete').click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Test case deleted')).toBeVisible()

    await page.goto(`/assistants/${assistantId}`)
    await page.getByRole('button', { name: 'Delete' }).click()
    await page.getByRole('button', { name: 'Delete' }).last().click()
    await expect(page.getByText('Assistant deleted')).toBeVisible()
  })
})
