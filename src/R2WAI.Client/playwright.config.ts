import { defineConfig, devices } from '@playwright/test'

// Runs against the strangler-fig gateway (docker compose, port 8080) so
// React and Blazor share one origin exactly as they do in production — see
// the migration plan's "Testing & Done Per Phase" section. Point
// PLAYWRIGHT_BASE_URL elsewhere (e.g. the Vite dev server) for a quicker
// inner loop once more of the app is cut over.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: 'list',
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:8080',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
