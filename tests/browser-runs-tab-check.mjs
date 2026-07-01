import { chromium } from 'playwright';

const BASE_URL = 'http://localhost:3001';

async function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function main() {
  const browser = await chromium.launch({ headless: true, args: ['--window-size=1920,1080'] });
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();

  // Login
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle', timeout: 15000 });
  await sleep(3000);
  await page.locator("input[type='email']").first().fill('admin@r2wai.io');
  await page.locator("input[type='password']").first().fill('R2wai_Admin!2026');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await sleep(5000);
  console.log('Logged in:', page.url());

  // Go to workflows
  await page.goto(`${BASE_URL}/workflow-studio`, { waitUntil: 'networkidle', timeout: 20000 });
  await sleep(5000);

  // Screenshot of workflows tab
  await page.screenshot({ path: 'tests/screenshots/workflows-tab.png', fullPage: true });

  // Click Runs tab using force
  const runsTab = page.locator('[role="tab"]', { hasText: 'Runs' });
  await runsTab.click({ force: true });
  await sleep(5000);

  // Take screenshot
  await page.screenshot({ path: 'tests/screenshots/runs-tab-direct.png', fullPage: true });

  // Check content
  const content = await page.content();
  const hasNoRuns = content.includes('No workflow runs found');
  const hasRunningText = content.includes('Running');
  const hasPurchaseRequest = content.includes('Purchase Request');
  const hasInvoiceApproval = content.includes('Invoice Approval');

  console.log('Has "No workflow runs found":', hasNoRuns);
  console.log('Has "Running" text:', hasRunningText);
  console.log('Has "Purchase Request":', hasPurchaseRequest);
  console.log('Has "Invoice Approval":', hasInvoiceApproval);

  await browser.close();
}

main().catch(e => { console.error('Fatal:', e); process.exit(1); });
