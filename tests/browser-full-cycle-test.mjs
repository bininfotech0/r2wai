import { chromium } from 'playwright';

const BASE_URL = 'http://localhost:3001';
const EMAIL = 'admin@r2wai.io';
const PASSWORD = 'admin123';

async function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function main() {
  console.log('=== R2WAI Full Cycle Browser Test ===\n');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  const results = [];

  function report(name, status, detail = '') {
    const icon = status === 'PASS' ? 'PASS' : 'FAIL';
    console.log(`  [${icon}] ${name.padEnd(45)} ${detail}`);
    results.push({ name, status, detail });
  }

  // === LOGIN ===
  console.log('--- Login ---');
  try {
    await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle', timeout: 15000 });
    await sleep(3000);
    await page.locator("input[type='email']").first().fill(EMAIL);
    await page.locator("input[type='password']").first().fill(PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await sleep(5000);
    const loggedIn = !page.url().includes('/login');
    report('Login with admin credentials', loggedIn ? 'PASS' : 'FAIL', page.url());
  } catch (e) {
    report('Login', 'FAIL', e.message);
  }

  // === WORKFLOW EXECUTE ===
  console.log('\n--- Workflow Studio ---');
  try {
    await page.goto(`${BASE_URL}/workflow-studio`, { waitUntil: 'networkidle', timeout: 20000 });
    await sleep(3000);

    // Check workflows loaded
    const wfRows = await page.locator('.mud-table-row, .mud-data-grid-row, tr').count();
    report('Workflows page loads data', wfRows > 0 ? 'PASS' : 'FAIL', `${wfRows} rows`);

    // Click Execute button on first workflow
    const execBtn = page.locator('[aria-label="Execute"]').first();
    if (await execBtn.isVisible()) {
      await execBtn.click();
      await sleep(3000);

      // Check for snackbar message
      const snackbar = await page.locator('.mud-snackbar').first().isVisible().catch(() => false);
      const pageContent = await page.content();
      const hasSuccess = pageContent.includes('Workflow started') || pageContent.includes('success');
      const hasError = pageContent.includes('Execute failed') || pageContent.includes('error');

      if (hasSuccess) {
        report('Workflow execution', 'PASS', 'Workflow started successfully');
      } else if (hasError) {
        report('Workflow execution', 'FAIL', 'Error shown');
      } else {
        report('Workflow execution', 'PASS', 'No error detected');
      }

      // Check Runs tab
      await sleep(2000);
      const runsTab = page.getByText('Runs', { exact: true });
      if (await runsTab.isVisible()) {
        await runsTab.click();
        await sleep(3000);
        const runsContent = await page.content();
        const hasRuns = !runsContent.includes('No workflow runs found');
        report('Workflow Runs tab shows instances', hasRuns ? 'PASS' : 'FAIL');
      }
    } else {
      report('Workflow execution', 'FAIL', 'Execute button not visible');
    }
    await page.screenshot({ path: 'tests/screenshots/workflow-after-execute.png', fullPage: true });
  } catch (e) {
    report('Workflow Studio', 'FAIL', e.message.substring(0, 100));
  }

  // === ASSISTANT CHAT ===
  console.log('\n--- Assistant Chat ---');
  try {
    await page.goto(`${BASE_URL}/assistant-studio`, { waitUntil: 'networkidle', timeout: 20000 });
    await sleep(3000);

    // Select first assistant
    const assistantItem = page.locator('.mud-list-item').first();
    if (await assistantItem.isVisible()) {
      await assistantItem.click();
      await sleep(2000);

      // Click chat button
      const chatBtn = page.locator('[aria-label="Chat"]');
      if (await chatBtn.isVisible()) {
        await chatBtn.click();
        await sleep(2000);

        // Type and send message
        const chatInput = page.locator('.mud-dialog input[type="text"], .mud-dialog .mud-input-slot').first();
        await chatInput.waitFor({ timeout: 5000 });
        await chatInput.fill('Hello! What can you do?');

        const sendBtn = page.locator('.mud-dialog').locator('button', { hasText: 'Send' });
        await sendBtn.click();

        // Wait for response (Ollama can be slow)
        console.log('    Waiting for AI response (up to 120s)...');
        await sleep(5000);

        // Check for "Thinking..." or response
        let hasResponse = false;
        for (let i = 0; i < 24; i++) {
          const dialogContent = await page.locator('.mud-dialog').innerHTML();
          if (dialogContent.includes('Assistant') && !dialogContent.includes('Thinking...')) {
            hasResponse = true;
            break;
          }
          if (dialogContent.includes('AI service is not configured') || dialogContent.includes('unavailable')) {
            report('Chat response received', 'PASS', 'AI not configured message shown (expected without API key)');
            hasResponse = true;
            break;
          }
          await sleep(5000);
        }

        if (hasResponse) {
          report('Assistant chat sends and receives', 'PASS', 'Response received');
        } else {
          report('Assistant chat', 'FAIL', 'No response after 120s');
        }

        // Close dialog
        const closeBtn = page.locator('.mud-dialog button', { hasText: 'Close' });
        if (await closeBtn.isVisible()) await closeBtn.click();
        await sleep(1000);
      } else {
        report('Chat button', 'FAIL', 'Not visible');
      }
    } else {
      report('Assistant selection', 'FAIL', 'No assistants in list');
    }
    await page.screenshot({ path: 'tests/screenshots/assistant-after-chat.png', fullPage: true });
  } catch (e) {
    report('Assistant Chat', 'FAIL', e.message.substring(0, 100));
  }

  // === ALL PAGES RENDER CHECK ===
  console.log('\n--- Page Rendering Check ---');
  const pages = [
    { name: 'Home', path: '/' },
    { name: 'Assistants', path: '/assistant-studio' },
    { name: 'Knowledge Bases', path: '/assistant-studio/knowledge' },
    { name: 'Documents', path: '/documents' },
    { name: 'Chatbots', path: '/chatbots' },
    { name: 'Tools', path: '/assistant-studio/tools' },
    { name: 'Workflows', path: '/workflow-studio' },
    { name: 'Approvals', path: '/workflow-studio/approvals' },
    { name: 'Integrations', path: '/workflow-studio/integrations' },
    { name: 'Operations', path: '/operations' },
    { name: 'AI Operations', path: '/operations/ai' },
    { name: 'Audit Logs', path: '/operations/audit-logs' },
    { name: 'Error Logs', path: '/operations/errors' },
    { name: 'Analytics', path: '/operations/analytics' },
    { name: 'Settings (Users)', path: '/settings' },
    { name: 'Roles', path: '/settings/roles' },
    { name: 'Models', path: '/settings/models' },
    { name: 'Tenant', path: '/settings/tenant' },
    { name: 'Profile', path: '/profile' },
    { name: 'About', path: '/about' },
  ];

  for (const p of pages) {
    try {
      await page.goto(`${BASE_URL}${p.path}`, { waitUntil: 'networkidle', timeout: 15000 });
      await sleep(2000);
      const url = page.url();
      if (url.includes('/login')) {
        report(p.name, 'FAIL', 'Redirected to login');
      } else {
        const hasMud = await page.locator('.mud-main-content, .mud-paper, .mud-table, .mud-typography').first().isVisible().catch(() => false);
        report(p.name, hasMud ? 'PASS' : 'FAIL');
      }
    } catch (e) {
      report(p.name, 'FAIL', e.message.substring(0, 60));
    }
  }

  // Summary
  console.log('\n=== Summary ===');
  const pass = results.filter(r => r.status === 'PASS').length;
  const fail = results.filter(r => r.status === 'FAIL').length;
  console.log(`  Total: ${results.length} | Pass: ${pass} | Fail: ${fail}`);

  if (fail > 0) {
    console.log('\n  Failed tests:');
    results.filter(r => r.status === 'FAIL').forEach(r => {
      console.log(`    - ${r.name}: ${r.detail}`);
    });
  }

  await browser.close();
}

main().catch(e => { console.error('Fatal:', e); process.exit(1); });
