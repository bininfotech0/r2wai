import { chromium } from 'playwright';
const BASE = 'http://localhost:3001';
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function main() {
  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();

  // Login
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  await page.locator("input[type='email']").first().fill('admin@r2wai.io');
  await page.locator("input[type='password']").first().fill('R2wai_Admin!2026');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await sleep(5000);

  // Go to workflows
  await page.goto(`${BASE}/workflow-studio`, { waitUntil: 'networkidle', timeout: 20000 });
  await sleep(5000);

  // Click Runs tab via the actual tab text element
  const tabs = await page.locator('[role="tab"]').all();
  console.log(`Found ${tabs.length} tabs`);
  for (let i = 0; i < tabs.length; i++) {
    const text = await tabs[i].textContent();
    console.log(`  Tab ${i}: "${text.trim()}"`);
    if (text.trim().includes('Runs')) {
      await tabs[i].dispatchEvent('click');
      console.log('  -> Dispatched click on Runs tab');
      break;
    }
  }
  await sleep(5000);
  await page.screenshot({ path: 'tests/screenshots/runs-tab-visual.png', fullPage: true });

  // Check what's visible
  const content = await page.content();
  console.log('\nPage has "Purchase Request":', content.includes('Purchase Request'));
  console.log('Page has "Invoice Approval":', content.includes('Invoice Approval'));
  console.log('Page has "No workflow runs":', content.includes('No workflow runs found'));

  // Also check the chat screenshot
  await page.goto(`${BASE}/assistant-studio`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  await page.locator('.mud-list-item').first().click();
  await sleep(2000);
  await page.locator('[aria-label="Chat"]').click();
  await sleep(2000);
  const input = page.locator('.mud-dialog input[type="text"], .mud-dialog .mud-input-slot').first();
  await input.fill('Hello, what can you help me with?');
  await page.locator('.mud-dialog').locator('button', { hasText: 'Send' }).click();
  console.log('\nWaiting for chat response...');
  for (let i = 0; i < 30; i++) {
    const html = await page.locator('.mud-dialog').innerHTML();
    if (html.includes('Assistant') && !html.includes('Thinking...')) {
      console.log('Got response!');
      break;
    }
    await sleep(5000);
  }
  await page.screenshot({ path: 'tests/screenshots/chat-response-visual.png', fullPage: true });

  await browser.close();
}

main().catch(e => { console.error('Fatal:', e); process.exit(1); });
