// Visits every top-level page as the seeded demo admin and records what each one actually shows:
// API failures, console errors, headings, empty-state text, interactive elements, and a screenshot.
// Usage: R2WAI_BASE_URL=http://localhost:8080 node tests/e2e/page-usefulness-audit.mjs
import { chromium } from 'playwright';
import fs from 'node:fs';

const BASE_URL = process.env.R2WAI_BASE_URL ?? 'http://localhost:8080';
const EMAIL = process.env.R2WAI_TEST_EMAIL ?? 'admin@r2wai.io';
const PASSWORD = process.env.R2WAI_TEST_PASSWORD ?? 'R2wai_Admin!2026';
const OUT = process.env.R2WAI_AUDIT_OUT ?? 'tests/e2e/screenshots/usefulness';

const PAGES = [
  '/', '/assistants', '/playground', '/workspaces', '/integrations', '/mcp-connections', '/tools',
  '/models', '/knowledge', '/automations', '/chatbots', '/deploy', '/deploy/widget', '/runs',
  '/approvals', '/monitor', '/settings', '/users', '/security', '/developer', '/inbox', '/profile',
  '/departments', '/about',
];

const EMPTY_PATTERNS = /no .{0,40}(yet|found|available)|nothing (here|to show)|get started|empty|create your first/i;

fs.mkdirSync(OUT, { recursive: true });
const browser = await chromium.launch({ headless: true });
const page = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();

let apiFailures = [];
let consoleErrors = [];
page.on('response', (r) => {
  if (r.url().includes('/api/') && r.status() >= 400) apiFailures.push(`${r.status()} ${r.request().method()} ${new URL(r.url()).pathname}`);
});
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 160)); });

await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });
await page.locator('input[type="email"], input[name="email"], input[autocomplete="username"]').first().fill(EMAIL);
await page.locator('input[type="password"]').first().fill(PASSWORD);
await page.locator('button[type="submit"]').click();
await page.waitForURL((u) => !u.pathname.startsWith('/login'), { timeout: 20000 });

const results = [];
for (const path of PAGES) {
  apiFailures = [];
  consoleErrors = [];
  const r = { path };
  try {
    await page.goto(`${BASE_URL}${path}`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(1500);
    r.finalUrl = new URL(page.url()).pathname;
    const main = page.locator('main').first();
    const scope = (await main.count()) ? main : page.locator('body');
    r.headings = (await scope.locator('h1, h2, h3, h4, h5, h6').allInnerTexts()).map((t) => t.trim()).filter(Boolean).slice(0, 8);
    const text = (await scope.innerText()).replace(/\s+/g, ' ').trim();
    r.textLength = text.length;
    r.emptyHints = [...new Set((text.match(new RegExp(EMPTY_PATTERNS.source, 'gi')) ?? []))].slice(0, 5);
    r.buttons = await scope.locator('button:visible').count();
    r.rows = await scope.locator('tbody tr:visible, [role="row"]:visible, li:visible, .MuiCard-root:visible').count();
    r.inputs = await scope.locator('input:visible, textarea:visible, select:visible').count();
    r.notFound = /page not found|404/i.test(text);
    r.textSample = text.slice(0, 400);
    await page.screenshot({ path: `${OUT}/${path.replace(/\//g, '_').replace(/^_/, '') || 'home'}.png`, fullPage: true });
  } catch (e) {
    r.error = e.message.split('\n')[0];
  }
  r.apiFailures = [...new Set(apiFailures)];
  r.consoleErrors = [...new Set(consoleErrors)].slice(0, 5);
  results.push(r);
  console.log(`${path.padEnd(18)} api-fail=${r.apiFailures.length} console=${r.consoleErrors.length} text=${r.textLength ?? '-'} rows=${r.rows ?? '-'} btns=${r.buttons ?? '-'} ${r.error ?? ''}`);
}

fs.writeFileSync(`${OUT}/results.json`, JSON.stringify(results, null, 2));
await browser.close();
