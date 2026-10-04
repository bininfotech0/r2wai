import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 10's verify criteria (main SPA half): Connected System CRUD plus its
// sub-resources (APIs, Configuration, Versions) against the real API.
// UI label is "Connected System" — backed by the unchanged ConnectedApplication
// entity/`/api/v1/applications` route, see types.ts. Moved out of primary Build
// nav into Manage and relabeled off "Workspace" once that word was redefined to
// mean the bigger Assistant/Capability/Knowledge container this entity sits under.
test.describe('Connected Systems', () => {
  test('add a connected system, manage its APIs/config/versions, change status, then delete', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/workspaces')
    await expect(page.getByRole('heading', { name: 'Connections' })).toBeVisible()

    const uniqueName = `E2E Connected System ${Date.now()}`
    const uniqueCode = `EA${Date.now()}`
    await page.getByRole('button', { name: 'Add Connected System' }).click()
    const createDialog = page.getByRole('dialog')
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByLabel('Code').fill(uniqueCode)
    await createDialog.getByRole('button', { name: 'Create' }).click()

    await expect(page).toHaveURL(/\/workspaces\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

    // APIs tab.
    await page.getByRole('tab', { name: 'APIs' }).click()
    await page.getByRole('button', { name: 'Add API' }).click()
    const apiDialog = page.getByRole('dialog')
    await apiDialog.getByLabel('Name').fill('Payroll API')
    await apiDialog.getByLabel('Base URL').fill('https://payroll.example.gov')
    await apiDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('API added')).toBeVisible()
    await expect(page.getByText('Payroll API')).toBeVisible()

    // Configuration tab.
    await page.getByRole('tab', { name: 'Configuration' }).click()
    await page.getByLabel('Max retries').fill('5')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await expect(page.getByText('Configuration saved')).toBeVisible()

    // Versions tab.
    await page.getByRole('tab', { name: 'Versions' }).click()
    await page.getByRole('button', { name: 'Create Version' }).click()
    await expect(page.getByText('Version created')).toBeVisible()
    await expect(page.getByText('v1')).toBeVisible()

    // Discovery — StartDiscovery is no longer a manual status flip (see
    // ApplicationAction/APPLICATION_ACTIONS in types.ts); it's driven by parsing a real OpenAPI
    // spec, which also carries the connected system straight through to Configuring.
    await page.getByRole('button', { name: 'Discover from OpenAPI Spec' }).click()
    const discoverDialog = page.getByRole('dialog')
    await discoverDialog.getByRole('tab', { name: 'Upload file' }).click()
    await discoverDialog.locator('input[type="file"]').setInputFiles({
      name: 'spec.json',
      mimeType: 'application/json',
      buffer: Buffer.from(JSON.stringify({
        openapi: '3.0.0',
        info: { title: 'Payroll API', version: '1.0.0' },
        paths: {
          '/employees': {
            get: { operationId: 'listEmployees', summary: 'List employees', responses: { '200': { description: 'OK' } } },
          },
        },
      })),
    })
    await discoverDialog.getByRole('button', { name: 'Discover' }).click()
    await expect(page.getByText(/Discovered 1 operation/)).toBeVisible()
    await expect(page.getByText('Configuring')).toBeVisible()

    // Clean up.
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    const confirmDialog = page.getByRole('dialog')
    await confirmDialog.getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Connected system deleted')).toBeVisible()
    await expect(page).toHaveURL(/\/workspaces$/)
  })

  test('a connected system can be created with no department at all', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    // A connected system is a first-class integration and does not require an organizational
    // classification. The standard creation flow should not ask the user to choose a Department.
    await page.goto('/workspaces')
    await expect(page.getByRole('heading', { name: 'Connections' })).toBeVisible()

    const uniqueName = `E2E No Department ${Date.now()}`
    const uniqueCode = `END${Date.now()}`
    await page.getByRole('button', { name: 'Add Connected System' }).click()
    const createDialog = page.getByRole('dialog')

    await expect(createDialog.getByLabel(/department/i)).toHaveCount(0)

    // Deliberately do NOT touch Department. Only Name and Code are supplied.
    await createDialog.getByLabel('Name').fill(uniqueName)
    await createDialog.getByLabel('Code').fill(uniqueCode)
    await createDialog.getByRole('button', { name: 'Create' }).click()

    // Creating must succeed and land on the new system's workspace.
    await expect(page).toHaveURL(/\/workspaces\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { name: uniqueName })).toBeVisible()

    // Department remains optional legacy metadata and is not part of the everyday connection list.
    await page.goto('/workspaces')
    await page.getByPlaceholder(/Search connected systems/i).fill(uniqueName)
    const row = page.getByRole('row', { name: new RegExp(uniqueName) })
    await expect(row).toBeVisible()
    await expect(page.getByRole('columnheader', { name: 'Department' })).toHaveCount(0)

    // Clean up from the detail page — the list DataTable has no per-row actions column.
    await row.click()
    await expect(page).toHaveURL(/\/workspaces\/[0-9a-f-]+$/)
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    const confirmDialog = page.getByRole('dialog')
    await confirmDialog.getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Connected system deleted')).toBeVisible()
    await expect(page).toHaveURL(/\/workspaces$/)
  })

  test('a duplicate Code for a department-less system is rejected', async ({ page }) => {
    test.setTimeout(90_000)
    await loginAsAdmin(page)
    await page.goto('/workspaces')

    const sharedCode = `EDUP${Date.now()}`
    const first = `E2E Dup A ${Date.now()}`
    const second = `E2E Dup B ${Date.now()}`

    const createWithoutDepartment = async (name: string) => {
      await page.getByRole('button', { name: 'Add Connected System' }).click()
      const dialog = page.getByRole('dialog')
      await dialog.getByLabel('Name').fill(name)
      await dialog.getByLabel('Code').fill(sharedCode)
      await dialog.getByRole('button', { name: 'Create' }).click()
    }

    // Department-less systems are Code-unique per tenant. That used to be an accidental side
    // effect of DepartmentId being NOT NULL: Postgres treats NULLs as distinct in a unique
    // index, so relaxing the column silently dropped the guarantee until the filtered unique
    // index IX_Applications_TenantId_Code_NoDepartment was added.
    await createWithoutDepartment(first)
    await expect(page).toHaveURL(/\/workspaces\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { name: first })).toBeVisible()

    await page.goto('/workspaces')
    const duplicateResponse = page.waitForResponse((response) =>
      response.url().includes('/api/v1/applications') && response.request().method() === 'POST',
    )
    await createWithoutDepartment(second)
    // The API's command validator returns 400 before the database unique index is reached.
    expect((await duplicateResponse).status()).toBe(400)

    // Rejected: the dialog stays open, the UI surfaces its create failure, and no navigation occurs.
    await expect(page.getByText('Failed to add connected system')).toBeVisible()
    await expect(page).toHaveURL(/\/workspaces$/)

    // Dismiss whatever dialog is open, then clean up the one that was created.
    const stillOpen = page.getByRole('dialog')
    if (await stillOpen.count()) {
      await stillOpen.getByRole('button', { name: /cancel|close/i }).first().click()
    }

    await page.getByPlaceholder(/Search connected systems/i).fill(first)
    const row = page.getByRole('row', { name: new RegExp(first) })
    await row.click()
    await page.getByRole('button', { name: 'Delete', exact: true }).click()
    const confirmDialog = page.getByRole('dialog')
    await confirmDialog.getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Connected system deleted')).toBeVisible()
  })

  test('old /applications URLs redirect to /workspaces, and an unknown route shows a real 404 page', async ({ page }) => {
    await loginAsAdmin(page)

    await page.goto('/applications')
    await expect(page).toHaveURL(/\/workspaces$/)
    await expect(page.getByRole('heading', { name: 'Connections' })).toBeVisible()

    await page.goto('/this-route-does-not-exist')
    await expect(page.getByText('Page not found')).toBeVisible()
    await page.getByRole('button', { name: 'Go to Home' }).click()
    await expect(page).toHaveURL(/\/$/)
  })
})
