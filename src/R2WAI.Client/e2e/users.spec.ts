import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

// Phase 10: the Roles tab now shows which nav persona each real system role maps to — the RBAC
// collapse migration means this is now a genuine 1:1 mapping (3 system roles, 3 personas), not a
// many-to-one grouping. Verifies the column renders real data, not a placeholder.
test('Users & Roles: Roles tab shows the real nav persona for each system role', async ({ page }) => {
  await loginAsAdmin(page)

  await page.goto('/users')
  await expect(page.getByRole('heading', { name: 'Users & Roles' })).toBeVisible()

  await page.getByRole('tab', { name: 'Roles' }).click()
  const systemAdminRow = page.getByRole('row', { name: /SystemAdmin/ })
  await expect(systemAdminRow).toBeVisible()
  await expect(systemAdminRow.getByText('Super Admin', { exact: true })).toBeVisible()

  const adminRow = page.getByRole('row').filter({ hasText: 'System administrator with full access' })
  await expect(adminRow.getByText('Admin', { exact: true }).last()).toBeVisible()
})
