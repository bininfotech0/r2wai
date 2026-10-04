import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
    // e2e/ holds Playwright specs (run via `npm run e2e`, not Vitest) —
    // excluded here or Vitest tries to execute them with its own runner.
    exclude: ['**/node_modules/**', '**/e2e/**'],
  },
})
