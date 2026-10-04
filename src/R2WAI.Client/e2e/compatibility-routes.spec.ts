import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

test('customer-facing route aliases preserve bookmarks and redirect to the supported pages', async ({ page }) => {
  await loginAsAdmin(page)

  const aliases: Array<[string, string]> = [
    ['/home?from=bookmark#top', '/?from=bookmark#top'],
    ['/agents', '/assistants'],
    ['/agents/00000000-0000-4000-8000-000000000001', '/assistants/00000000-0000-4000-8000-000000000001'],
    ['/connections', '/workspaces'],
    ['/connections/00000000-0000-4000-8000-000000000002', '/workspaces/00000000-0000-4000-8000-000000000002'],
    ['/knowledge/new', '/knowledge'],
    ['/publish', '/chatbots'],
    ['/activity', '/runs'],
    ['/activity/00000000-0000-4000-8000-000000000003', '/runs'],
  ]

  for (const [alias, destination] of aliases) {
    await page.goto(alias)
    await expect(page).toHaveURL(new URL(destination, page.url()).href)
  }
})
