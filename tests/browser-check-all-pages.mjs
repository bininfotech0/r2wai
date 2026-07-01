import { chromium } from 'playwright';

const BASE_URL = 'http://localhost:3001';
const API_URL = 'http://localhost:5000';
const EMAIL = 'admin@r2wai.io';
const PASSWORD = 'R2wai_Admin!2026';

const PAGES = [
  { name: 'Home', path: '/' },
  { name: 'Assistants', path: '/assistant-studio' },
  { name: 'Knowledge Bases', path: '/assistant-studio/knowledge' },
  { name: 'Documents', path: '/documents' },
  { name: 'Chatbots', path: '/chatbots' },
  { name: 'Tools', path: '/assistant-studio/tools' },
  { name: 'Workflows', path: '/workflow-studio' },
  { name: 'Approvals', path: '/workflow-studio/approvals' },
  { name: 'Integrations', path: '/workflow-studio/integrations' },
  { name: 'Operations Dashboard', path: '/operations' },
  { name: 'AI Operations', path: '/operations/ai' },
  { name: 'Audit Logs', path: '/operations/audit-logs' },
  { name: 'Error Logs', path: '/operations/errors' },
  { name: 'Usage Analytics', path: '/operations/analytics' },
  { name: 'Settings (Users)', path: '/settings' },
  { name: 'Roles', path: '/settings/roles' },
  { name: 'Models', path: '/settings/models' },
  { name: 'Tenant Settings', path: '/settings/tenant' },
  { name: 'Profile', path: '/profile' },
  { name: 'About', path: '/about' },
];

async function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function main() {
  console.log('=== R2WAI Browser Page Check ===\n');

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  // Collect console errors
  const consoleErrors = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      consoleErrors.push(msg.text());
    }
  });

  // Step 1: Login
  console.log('--- Step 1: Login ---');
  try {
    await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle', timeout: 15000 });
    await sleep(3000); // Wait for Blazor circuit

    // Check login page rendered
    const title = await page.title();
    console.log(`  Login page title: ${title}`);

    // Fill email
    const emailInput = page.locator("input[type='email']").first();
    await emailInput.waitFor({ timeout: 10000 });
    await emailInput.fill(EMAIL);

    // Fill password
    const passwordInput = page.locator("input[type='password']").first();
    await passwordInput.waitFor({ timeout: 5000 });
    await passwordInput.fill(PASSWORD);

    // Click Sign In
    const signInBtn = page.getByRole('button', { name: 'Sign in' });
    await signInBtn.waitFor({ timeout: 5000 });
    await signInBtn.click();

    // Wait for redirect to home
    await sleep(5000);
    const url = page.url();
    if (url.includes('/login')) {
      // Try again - sometimes Blazor needs a moment
      console.log('  Still on login page, waiting longer...');
      await sleep(5000);
    }

    const postLoginUrl = page.url();
    const loginSuccess = !postLoginUrl.includes('/login');
    console.log(`  Login result: ${loginSuccess ? 'SUCCESS' : 'FAILED'}`);
    console.log(`  Current URL: ${postLoginUrl}\n`);

    if (!loginSuccess) {
      const content = await page.content();
      if (content.includes('Invalid') || content.includes('error')) {
        console.log('  Error message detected on page');
      }
      // Take screenshot
      await page.screenshot({ path: 'tests/screenshots/login-failed.png', fullPage: true });
      console.log('  Screenshot saved: tests/screenshots/login-failed.png');
    }
  } catch (err) {
    console.log(`  Login error: ${err.message}`);
    await page.screenshot({ path: 'tests/screenshots/login-error.png', fullPage: true });
  }

  // Step 2: Check all pages
  console.log('--- Step 2: Checking All Pages ---\n');
  const results = [];

  for (const pageInfo of PAGES) {
    const result = { name: pageInfo.name, path: pageInfo.path, status: 'UNKNOWN', errors: [] };
    try {
      await page.goto(`${BASE_URL}${pageInfo.path}`, { waitUntil: 'networkidle', timeout: 20000 });
      await sleep(3000); // Wait for Blazor rendering

      const currentUrl = page.url();

      // Check if redirected to login (auth failed)
      if (currentUrl.includes('/login')) {
        result.status = 'AUTH_REDIRECT';
        result.errors.push('Redirected to login - not authenticated');
      } else {
        // Check for error states in the page
        const content = await page.content();

        // Check for common error indicators
        const hasError = content.includes('An error has occurred') ||
                        content.includes('Sorry, there\'s nothing at this address') ||
                        content.includes('Unhandled exception');

        const hasNotAuthorized = content.includes('not authorized');

        if (hasError) {
          result.status = 'ERROR';
          result.errors.push('Error content detected on page');
        } else if (hasNotAuthorized) {
          result.status = 'UNAUTHORIZED';
          result.errors.push('Not authorized message shown');
        } else {
          // Check that MudBlazor components rendered
          const hasMudContent = await page.locator('.mud-main-content, .mud-paper, .mud-table, .mud-card, .mud-typography').first().isVisible().catch(() => false);
          if (hasMudContent) {
            result.status = 'OK';
          } else {
            result.status = 'EMPTY';
            result.errors.push('No MudBlazor content found');
          }
        }
      }

      // Take screenshot
      const screenshotName = pageInfo.path.replace(/\//g, '_').replace(/^_/, '') || 'home';
      await page.screenshot({
        path: `tests/screenshots/${screenshotName}.png`,
        fullPage: true
      });

    } catch (err) {
      result.status = 'TIMEOUT/ERROR';
      result.errors.push(err.message);
    }

    const statusIcon = result.status === 'OK' ? 'PASS' :
                       result.status === 'AUTH_REDIRECT' ? 'AUTH' :
                       'FAIL';
    console.log(`  [${statusIcon}] ${pageInfo.name.padEnd(25)} ${pageInfo.path.padEnd(35)} ${result.status}`);
    if (result.errors.length > 0) {
      result.errors.forEach(e => console.log(`        -> ${e}`));
    }
    results.push(result);
  }

  // Summary
  console.log('\n--- Summary ---');
  const ok = results.filter(r => r.status === 'OK').length;
  const authRedirect = results.filter(r => r.status === 'AUTH_REDIRECT').length;
  const errors = results.filter(r => !['OK', 'AUTH_REDIRECT'].includes(r.status)).length;
  console.log(`  Total pages: ${results.length}`);
  console.log(`  OK: ${ok}`);
  console.log(`  Auth Redirect: ${authRedirect}`);
  console.log(`  Errors/Other: ${errors}`);

  if (consoleErrors.length > 0) {
    console.log(`\n--- Browser Console Errors (${consoleErrors.length}) ---`);
    consoleErrors.slice(0, 20).forEach(e => console.log(`  ${e.substring(0, 150)}`));
  }

  console.log('\nScreenshots saved to: tests/screenshots/');

  await browser.close();
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
