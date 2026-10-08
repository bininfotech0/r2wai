import { test, expect } from './fixtures/cleanup'
import { loginAsAdmin } from './fixtures/auth'

// Phase 12's verify criteria: full admin CRUD, plus an explicit confirmation
// that a UI-hidden admin action still 403s/401s when invoked directly — RBAC
// is enforced server-side by role name ([Authorize(Roles=...)]), never by
// the client, and this proves the new Manage UI didn't weaken that.
test.describe('Admin', () => {
  test('Users & Roles: create a role, create a user, assign the role, then delete both', async ({ page }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/users')
    await expect(page.getByRole('heading', { name: 'Users & Roles' })).toBeVisible()

    // Roles tab.
    await page.getByRole('tab', { name: 'Roles' }).click()
    const roleName = `E2E Role ${Date.now()}`
    await page.getByRole('button', { name: 'New Role' }).click()
    const roleDialog = page.getByRole('dialog')
    await roleDialog.getByLabel('Name').fill(roleName)
    await roleDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('Role created')).toBeVisible()
    await expect(page.getByRole('gridcell', { name: roleName })).toBeVisible()

    // Users tab — create a user and assign the new role.
    await page.getByRole('tab', { name: 'Users', exact: true }).click()
    const externalId = `e2e-${Date.now()}`
    const email = `e2e-${Date.now()}@example.com`
    await page.getByRole('button', { name: 'New User' }).click()
    const userDialog = page.getByRole('dialog')
    await userDialog.getByLabel('External ID').fill(externalId)
    await userDialog.getByLabel('Email').fill(email)
    await userDialog.getByLabel('First name').fill('E2E')
    await userDialog.getByLabel('Last name').fill('Tester')
    await userDialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('User created')).toBeVisible()

    const userRow = page.getByRole('row', { name: new RegExp(email) })
    await expect(userRow).toBeVisible()
    await userRow.getByLabel('Roles').click()
    const rolesDialog = page.getByRole('dialog')
    await rolesDialog.getByRole('checkbox', { name: roleName }).check()
    await rolesDialog.getByRole('button', { name: 'Save' }).click()
    await expect(page.getByText('Roles updated')).toBeVisible()
    await expect(page.getByRole('gridcell', { name: new RegExp(roleName) })).toBeVisible()

    // Clean up.
    await userRow.getByLabel('Delete').click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('User deleted')).toBeVisible()

    await page.getByRole('tab', { name: 'Roles' }).click()
    await page.getByRole('row', { name: new RegExp(roleName) }).getByLabel('Delete').click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Role deleted')).toBeVisible()
  })

  test('AI Models: create, test connection, then delete', async ({ page, cleanup }) => {
    test.setTimeout(60_000)
    await loginAsAdmin(page)

    await page.goto('/models')
    await expect(page.getByRole('heading', { name: 'AI Models' })).toBeVisible()

    const name = `E2E Model ${Date.now()}`
    cleanup.add('model', name)
    await page.getByRole('button', { name: 'New Model' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Name').fill(name)
    await dialog.getByLabel('Model ID').fill('qwen3:4b')
    await dialog.getByLabel('Endpoint (optional)').fill('http://host.docker.internal:11434')
    await dialog.getByRole('button', { name: 'Create' }).click()
    await expect(page.getByText('Model created')).toBeVisible()

    const row = page.getByRole('row', { name: new RegExp(name) })
    await expect(row).toBeVisible()
    await row.getByLabel('Test connection').click()
    // Real live call to Ollama — assert it completes (either outcome), not a specific result.
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 20_000 })

    await row.getByLabel('Delete').click()
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expect(page.getByText('Model deleted')).toBeVisible()
  })

  test('RBAC: a hidden admin action still 401s when invoked directly without a token', async ({ request }) => {
    // Proves the Manage UI is a convenience layer, not the authorization
    // boundary — the API rejects this regardless of what the UI shows.
    const response = await request.post('http://localhost:8080/api/v1/admin/users', {
      data: { externalId: 'x', email: 'x@example.com', firstName: 'X', lastName: 'Y' },
    })
    expect(response.status()).toBe(401)
  })
})
