import { test, expect } from '@playwright/test'
import { loginAsAdmin } from './fixtures/auth'

/**
 * Every main route renders its own content without tripping the route error boundary.
 *
 * This exists because a dashboard bug shipped through a green unit-test run and a green
 * production build: `/operations/daily-trend?days=30` returns the series wrapped in a `{ days }`
 * envelope, the page treated it as a bare array, and `(data ?? []).map` threw — white-screening
 * the entire dashboard. Type checking could not catch it, because the declared type matched the
 * type the code was written against; only the real response shape disagreed. Nothing in the suite
 * asserted that a page body actually rendered, so the crash was invisible until someone opened
 * the app.
 *
 * So this spec checks the page's main content rather than the persistent sidebar. A future
 * mismatch between a declared response type and the real endpoint now fails here instead of in
 * a user's browser.
 */
const ROUTES: { path: string; expect: RegExp | string }[] = [
  { path: '/', expect: 'Home' },
  { path: '/assistants', expect: /Agents|Assistants/ },
  { path: '/chatbots', expect: /Publish|Chatbots/ },
  { path: '/knowledge', expect: /Knowledge/i },
  { path: '/integrations', expect: /Integration/i },
  { path: '/playground', expect: /Playground/i },
  { path: '/runs', expect: /Execution|Runs/i },
  { path: '/approvals', expect: /Confirmation|Approval/i },
  { path: '/monitor', expect: /Monitor/i },
  { path: '/workspaces', expect: /Connections|Connected Systems|Workspace/i },
  { path: '/tools', expect: /Tools/i },
  { path: '/models', expect: /Model/i },
  { path: '/users', expect: /User/i },
  { path: '/security', expect: /Security|Polic/i },
  { path: '/settings', expect: /Setting/i },
  { path: '/developer', expect: /API|SDK|Developer/i },
  // Distribution has no sidebar entry of its own; it is reached from Publish.
  { path: '/deploy', expect: /Channels/i },
  { path: '/deploy/widget', expect: /Website Widget|Chatbots|Select/i },
  { path: '/inbox', expect: /Inbox/i },
  { path: '/about', expect: /About/i },
  { path: '/profile', expect: /Profile/i },
]

const ERROR_BOUNDARY = 'This page could not be displayed'

test.describe('Route smoke: no page white-screens', () => {
  test('every main route renders its own content without an error boundary', async ({ page }) => {
    test.setTimeout(180_000)
    await loginAsAdmin(page)

    const failures: string[] = []

    for (const route of ROUTES) {
      // A crash on the previous page can leave the router wedged, so recover explicitly
      // between routes rather than letting one failure cascade into every later assertion.
      const errors: string[] = []
      const onPageError = (e: Error) => errors.push(e.message)
      page.on('pageerror', onPageError)

      try {
        await page.goto(route.path, { waitUntil: 'domcontentloaded' })
        await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {})

        const errorBoundary = page.getByText(ERROR_BOUNDARY, { exact: true })

        if (await errorBoundary.isVisible().catch(() => false)) {
          failures.push(`${route.path}: React error boundary rendered`)
        } else {
          const mainContent = page.locator('#main-content')
          if (await mainContent.count() === 0) {
            failures.push(`${route.path}: main content landmark was not rendered`)
          } else if (!new RegExp(route.expect).test(await mainContent.innerText())) {
            failures.push(`${route.path}: expected ${route.expect} in the main content`)
          }
        }

        // Catch the underlying TypeError directly too, so the failure message names the cause
        // instead of only the generic boundary text.
        const crash = errors.find((e) => /is not a function|undefined is not|Cannot read/.test(e))
        if (crash) failures.push(`${route.path}: uncaught ${crash}`)
      } catch (err) {
        failures.push(`${route.path}: navigation failed — ${(err as Error).message}`)
      } finally {
        page.off('pageerror', onPageError)
      }
    }

    expect(failures, `Routes that failed to render:\n  - ${failures.join('\n  - ')}`).toEqual([])
  })
})
