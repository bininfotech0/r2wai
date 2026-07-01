import { chromium } from 'playwright';
const BASE = 'http://localhost:3001';
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function main() {
  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  let pass = 0, fail = 0;

  function check(name, ok, detail = '') {
    console.log(`  [${ok ? 'PASS' : 'FAIL'}] ${name}${detail ? ' — ' + detail : ''}`);
    ok ? pass++ : fail++;
  }

  // Login
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  await page.locator("input[type='email']").first().fill('admin@r2wai.io');
  await page.locator("input[type='password']").first().fill('R2wai_Admin!2026');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await sleep(5000);
  check('Login', !page.url().includes('/login'));

  // Home — recent activity
  console.log('\n--- Home ---');
  await page.goto(`${BASE}/`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  const homeContent = await page.content();
  check('Home loads activity', homeContent.includes('Recent Activity'));
  check('Home has audit entries', homeContent.includes('Create') || homeContent.includes('Update'));
  await page.screenshot({ path: 'tests/screenshots/final-home.png', fullPage: true });

  // Workflows — runs tab with data
  console.log('\n--- Workflow Runs ---');
  await page.goto(`${BASE}/workflow-studio`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(4000);
  await page.locator('[role="tab"]', { hasText: 'Runs' }).click({ force: true });
  await sleep(4000);
  const wfContent = await page.content();
  check('Runs tab has instances', wfContent.includes('Purchase Request') || wfContent.includes('Invoice'));
  check('Runs tab shows Running status', wfContent.includes('Running'));
  await page.screenshot({ path: 'tests/screenshots/final-runs.png', fullPage: true });

  // Assistant chat
  console.log('\n--- Assistant Chat ---');
  await page.goto(`${BASE}/assistant-studio`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  const firstAssistant = page.locator('.mud-list-item').first();
  if (await firstAssistant.isVisible()) {
    await firstAssistant.click();
    await sleep(2000);
    const chatBtn = page.locator('[aria-label="Chat"]');
    if (await chatBtn.isVisible()) {
      await chatBtn.click();
      await sleep(2000);
      const input = page.locator('.mud-dialog input[type="text"], .mud-dialog .mud-input-slot').first();
      await input.fill('Hello');
      await page.locator('.mud-dialog').locator('button', { hasText: 'Send' }).click();
      console.log('    Waiting for AI response...');
      for (let i = 0; i < 30; i++) {
        const html = await page.locator('.mud-dialog').innerHTML();
        if (html.includes('Assistant') && !html.includes('Thinking...')) break;
        await sleep(5000);
      }
      const dlg = await page.locator('.mud-dialog').innerHTML();
      const gotReply = dlg.includes('Assistant') && !dlg.includes('Thinking...');
      check('Chat gets AI response', gotReply);
      await page.screenshot({ path: 'tests/screenshots/final-chat.png', fullPage: true });
      await page.locator('.mud-dialog button', { hasText: 'Close' }).click().catch(() => {});
    }
  }

  // Operations
  console.log('\n--- Operations ---');
  await page.goto(`${BASE}/operations`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  const opsContent = await page.content();
  check('Operations shows health', opsContent.includes('Healthy'));
  check('Operations shows workflows count', opsContent.includes('Workflows'));
  await page.screenshot({ path: 'tests/screenshots/final-operations.png', fullPage: true });

  console.log(`\n=== Final: ${pass} PASS / ${fail} FAIL ===`);
  await browser.close();
}

main().catch(e => { console.error('Fatal:', e); process.exit(1); });
