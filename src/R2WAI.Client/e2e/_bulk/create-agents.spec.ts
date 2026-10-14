// TEMPORARY bulk run (not part of the suite): creates and fully configures the agents in
// AGENTS_JSON through the real UI as admin, then publishes each one. Results go to RESULTS_DIR.
import { test, expect, type Page } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'
import { loginAsAdmin } from '../fixtures/auth'

interface AgentSpec {
  n: number
  name: string
  type: string
  description: string
  systemPrompt: string
  settings: { responseStyle: string; answerLength: string; citationsEnabled: boolean; askClarification: boolean; temperature: number; maxOutputTokens: number }
  knowledgeBase: string | null
  tool: string | null
  model: string | null
}

const agents: AgentSpec[] = JSON.parse(fs.readFileSync(process.env.AGENTS_JSON!, 'utf-8'))
const resultsFile = path.join(process.env.RESULTS_DIR!, 'create-results.jsonl')
const only = process.env.ONLY ? new Set(process.env.ONLY.split(',').map(Number)) : null

test.describe.configure({ mode: 'parallel' })

async function pick(page: Page, label: string, option: string | RegExp, scope = page) {
  await scope.getByLabel(label, { exact: true }).click()
  await page.getByRole('option', { name: option }).click()
}

for (const a of agents.filter((x) => !only || only.has(x.n))) {
  test(`#${a.n} ${a.name}`, async ({ page }) => {
    test.setTimeout(180_000)
    const steps: string[] = []
    let id = ''
    const record = (status: string, error?: string) =>
      fs.appendFileSync(resultsFile, JSON.stringify({ n: a.n, name: a.name, id, status, steps, error }) + '\n')
    try {
      await loginAsAdmin(page)
      await page.goto('/assistants')

      // 1. Create (manual path) with name, type and base model.
      await page.getByRole('button', { name: 'New agent' }).click()
      await page.getByRole('button', { name: /configure manually/i }).click()
      const dialog = page.getByRole('dialog')
      await dialog.getByLabel('Name').fill(a.name)
      await pick(page, 'Type', new RegExp(`^${a.type}$`), dialog)
      if (a.model) await pick(page, 'Base Model', new RegExp(`^${a.model} \\(`), dialog)
      await dialog.getByRole('button', { name: 'Create', exact: true }).click()
      await expect(page.getByRole('heading', { name: a.name, level: 1 })).toBeVisible({ timeout: 20_000 })
      id = page.url().split('/assistants/')[1]
      steps.push('created')

      // 2. Overview: description + instructions, saved together.
      const details = page.getByRole('region', { name: 'Details' })
      await details.getByRole('button', { name: 'Edit' }).click()
      await details.getByLabel('Description').fill(a.description)
      const instructions = page.getByRole('region', { name: 'Instructions' })
      await instructions.getByRole('button', { name: 'Edit' }).click()
      await instructions.getByLabel('Instructions').fill(a.systemPrompt)
      await page.getByRole('region', { name: 'Unsaved changes' }).getByRole('button', { name: 'Save' }).click()
      await expect(page.getByText('Saved', { exact: true })).toBeVisible()
      steps.push('overview')

      // 3. Knowledge source.
      if (a.knowledgeBase) {
        await page.getByRole('tab', { name: 'Knowledge' }).click()
        await page.getByRole('switch', { name: `Use ${a.knowledgeBase}` }).or(page.getByRole('checkbox', { name: `Use ${a.knowledgeBase}` })).click()
        await expect(page.getByText('Knowledge source updated')).toBeVisible()
        steps.push('knowledge')
      }

      // 4. Behaviour settings (reload first so the drawer builds its payload from fresh data).
      await page.reload()
      await expect(page.getByRole('heading', { name: a.name, level: 1 })).toBeVisible()
      await page.getByRole('button', { name: 'Settings', exact: true }).click()
      const drawer = page.locator('.MuiDrawer-paper').filter({ hasText: 'Generative AI' })
      await pick(page, 'Response style', a.settings.responseStyle, drawer)
      await pick(page, 'Answer length', a.settings.answerLength, drawer)
      await drawer.getByRole('switch', { name: 'Cite sources in answers' }).or(drawer.getByRole('checkbox', { name: 'Cite sources in answers' })).setChecked(a.settings.citationsEnabled)
      await drawer.getByRole('switch', { name: 'Ask clarifying questions' }).or(drawer.getByRole('checkbox', { name: 'Ask clarifying questions' })).setChecked(a.settings.askClarification)
      if (a.knowledgeBase) await pick(page, 'Knowledge retrieval', /^Agentic/, drawer)
      await drawer.getByRole('button', { name: /Model parameters/ }).click()
      await drawer.getByLabel('Temperature').fill(String(a.settings.temperature))
      await drawer.getByLabel('Maximum output tokens').fill(String(a.settings.maxOutputTokens))
      await drawer.getByRole('button', { name: 'Save settings' }).click()
      await expect(page.getByText('Settings saved')).toBeVisible()
      steps.push('settings')

      // 5. Tools.
      if (a.tool) {
        await page.reload()
        await expect(page.getByRole('heading', { name: a.name, level: 1 })).toBeVisible()
        await page.getByRole('tab', { name: 'Tools' }).click()
        await page.getByRole('button', { name: /Advanced — Individual tools/ }).click()
        await page.getByRole('button', { name: 'Add Tool' }).click()
        const toolDrawer = page.locator('.MuiDrawer-paper').filter({ has: page.getByRole('textbox', { name: 'Search tools' }) })
        await toolDrawer.getByRole('button', { name: new RegExp(`^${a.tool}`) }).click()
        await expect(toolDrawer.getByRole('button', { name: new RegExp(`^${a.tool}`) }).getByRole('checkbox')).toBeChecked()
        await page.keyboard.press('Escape')
        await expect(page.getByText('1 tool enabled.').or(page.getByRole('button', { name: 'Remove' }))).toBeVisible()
        steps.push('tools')
      }

      // 6. Publish through the readiness checklist.
      await page.getByRole('button', { name: 'Publish', exact: true }).click()
      await page.getByRole('button', { name: /^Publish \d/ }).click()
      await expect(page.getByText('Agent published')).toBeVisible()
      steps.push('published')
      record('ok')
    } catch (e) {
      await page.screenshot({ path: path.join(process.env.RESULTS_DIR!, `fail-create-${a.n}.png`) }).catch(() => {})
      record('fail', String(e).slice(0, 400))
      throw e
    }
  })
}
