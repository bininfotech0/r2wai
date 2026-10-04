import type { Page } from '@playwright/test'

/** Real seeded admin account — see ApplicationDbContextSeed.cs. */
export const ADMIN_EMAIL = 'admin@r2wai.io'
export const ADMIN_PASSWORD = 'R2wai_Admin!2026'

// A second tenant's admin — exists purely so cross-tenant isolation can be regression-tested
// (every other seeded account belongs to the one default tenant). See ApplicationDbContextSeed.cs.
export const OTHER_TENANT_ADMIN_EMAIL = 'othertenant-admin@r2wai.io'
export const OTHER_TENANT_ADMIN_PASSWORD = 'R2wai_OtherTenant!2026'

export async function loginAsAdmin(page: Page) {
  await page.goto('/login')
  await page.getByLabel('Email or Aadhaar Number').fill(ADMIN_EMAIL)
  await page.getByRole('textbox', { name: 'Password' }).fill(ADMIN_PASSWORD)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await page.waitForURL('/')
}

export async function loginAsOtherTenantAdmin(page: Page) {
  await page.goto('/login')
  await page.getByLabel('Email or Aadhaar Number').fill(OTHER_TENANT_ADMIN_EMAIL)
  await page.getByRole('textbox', { name: 'Password' }).fill(OTHER_TENANT_ADMIN_PASSWORD)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await page.waitForURL('/')
}
