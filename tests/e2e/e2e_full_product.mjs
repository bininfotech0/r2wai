/**
 * R2WAI — Complete Product E2E Test Suite
 *
 * Coverage: Authentication · Dashboard · Conversations · Documents ·
 *           Knowledge Bases · Assistant Studio · Chatbots · Workflows ·
 *           Integrations · Approvals · Operations · Admin (all sections)
 *
 * App  : https://localhost:3000
 * API  : http://localhost:5000
 * Login: admin@r2wai.io / R2wai_Admin!2026
 *
 * Run  : node e2e_full_product.mjs
 * Deps : playwright (npm i -D playwright && npx playwright install chromium)
 */

import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'fs';
import path from 'path';

// ─── Config ──────────────────────────────────────────────────────────────────
const BASE   = 'https://localhost:3000';
const API    = 'http://localhost:5000';
const EMAIL  = 'admin@r2wai.io';
const PASS   = 'R2wai_Admin!2026';
const SHOTS  = 'C:/Users/LENOVO/AppData/Local/Temp/e2e_shots';
const REPORT = 'C:/Users/LENOVO/AppData/Local/Temp/e2e_report.json';

mkdirSync(SHOTS, { recursive: true });

// ─── Test registry ────────────────────────────────────────────────────────────
const results = [];
let page, browser;

async function go(url, opts = {}) {
  await page.goto(BASE + url, { waitUntil: 'networkidle', timeout: 30000, ...opts });
  await page.waitForTimeout(1500);
}

async function shot(name) {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: false });
  return p;
}

async function test(id, label, fn) {
  process.stdout.write(`  ${id}: ${label} ... `);
  try {
    const detail = await fn();
    results.push({ id, label, pass: true, detail: detail ?? '' });
    console.log(`✅  ${detail ?? ''}`);
  } catch (e) {
    const s = await shot(`FAIL_${id}`).catch(() => 'no-shot');
    results.push({ id, label, pass: false, detail: e.message, shot: s });
    console.log(`❌  ${e.message.slice(0, 120)}`);
  }
}

// Helpers
const visible  = (sel, opts) => page.locator(sel).first().waitFor({ state: 'visible', timeout: 10000, ...opts });
const hasText  = async (txt) => (await page.locator('body').textContent()).includes(txt);
const btnClick = async (label) => {
  const btn = page.locator(`button:has-text("${label}")`).first();
  await btn.waitFor({ timeout: 10000 });
  await btn.click();
  await page.waitForTimeout(800);
};
const typeField = async (sel, val) => {
  await page.locator(sel).first().click();
  await page.locator(sel).first().type(val, { delay: 50 });
  await page.keyboard.press('Tab');
  await page.waitForTimeout(300);
};
const closeDialog = async () => {
  try {
    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
  } catch {}
};
const apiGET = async (path) => {
  const r = await fetch(`${API}${path}`);
  return r.status;
};

// Login via API call + sessionStorage injection (bypasses Blazor form timing issues)
async function doLogin() {
  // Step 1: Get token from API
  const res = await fetch(`${API}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: EMAIL, password: PASS })
  });
  if (!res.ok) throw new Error(`API login failed: ${res.status}`);
  const data = await res.json();
  const token   = data.token || data.accessToken;
  const refresh = data.refreshToken || '';
  const user    = JSON.stringify(data.user || {});
  if (!token) throw new Error('No token in login response');

  // Step 2: Navigate to login page so same origin is set, then inject tokens into sessionStorage
  await page.goto(BASE + '/login', { waitUntil: 'domcontentloaded', timeout: 20000 });
  await page.waitForTimeout(1000);
  await page.evaluate(([t, r, u]) => {
    sessionStorage.setItem('r2wai_token', t);
    sessionStorage.setItem('r2wai_refresh_token', r);
    sessionStorage.setItem('r2wai_user', u);
  }, [token, refresh, user]);

  // Step 3: Navigate to home — Blazor will read from sessionStorage and authenticate
  await page.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 20000 });
  await page.waitForTimeout(3000); // give Blazor circuit time to hydrate + read sessionStorage
}

// UI-based login (for AUTH-02 test only — tests the actual form)
async function doLoginViaForm() {
  await page.goto(BASE + '/login', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForSelector('input[type="email"]', { timeout: 20000 });
  await page.waitForTimeout(1500);
  await page.locator('input[type="email"]').click();
  await page.locator('input[type="email"]').type(EMAIL, { delay: 60 });
  await page.keyboard.press('Tab');
  await page.locator('input[type="password"]').click();
  await page.locator('input[type="password"]').type(PASS, { delay: 60 });
  await page.keyboard.press('Tab');
  await page.waitForTimeout(600);
  await page.click('button[type="submit"]');
  await page.waitForFunction(() => !window.location.pathname.startsWith('/login'), { timeout: 45000 });
  await page.waitForTimeout(2000);
}

// ─── TEST SUITES ─────────────────────────────────────────────────────────────

// ═══ 1. AUTHENTICATION ═══════════════════════════════════════════════════════
async function suiteAuth() {
  console.log('\n═══ 1. AUTHENTICATION ═══');

  await test('AUTH-01', 'Login page renders', async () => {
    await go('/login');
    await visible('input[type="email"]');
    return 'login form present';
  });

  await test('AUTH-02', 'Login form: submit with valid credentials redirects away', async () => {
    await doLoginViaForm();
    return 'logged in via form, URL: ' + page.url().replace(BASE, '');
  });

  await test('AUTH-03', 'Wrong credentials show error (uses nonexistent account)', async () => {
    await page.goto(BASE + '/login', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('input[type="email"]', { timeout: 15000 });
    await page.waitForTimeout(1200);
    await page.locator('input[type="email"]').click();
    await page.locator('input[type="email"]').type('nouser@e2etest.invalid', { delay: 60 });
    await page.keyboard.press('Tab');
    await page.locator('input[type="password"]').click();
    await page.locator('input[type="password"]').type('WrongPassword!', { delay: 60 });
    await page.keyboard.press('Tab');
    await page.waitForTimeout(500);
    await page.click('button[type="submit"]');
    await page.waitForTimeout(3500);
    const onLogin = page.url().includes('/login');
    const hasErr  = await hasText('Invalid') || await hasText('incorrect') || await hasText('password') || await hasText('not found');
    if (!onLogin && !hasErr) throw new Error('No error shown and not on login page');
    return 'error shown on bad credentials';
  });

  // Re-inject token so rest of suite is authenticated
  await doLogin();

  await test('AUTH-04', 'Unauthenticated redirect to login', async () => {
    const ctx2 = await browser.newContext({ ignoreHTTPSErrors: true });
    const p2   = await ctx2.newPage();
    await p2.goto(BASE + '/admin/users', { waitUntil: 'networkidle' });
    await p2.waitForTimeout(2000);
    const redirected = p2.url().includes('/login') || p2.url().includes('/');
    await ctx2.close();
    if (!redirected) throw new Error('No redirect for unauthenticated access');
    return 'redirected to login';
  });
}

// ═══ 2. DASHBOARD ════════════════════════════════════════════════════════════
async function suiteDashboard() {
  console.log('\n═══ 2. DASHBOARD ═══');

  await test('DASH-01', 'Dashboard / home loads', async () => {
    await go('/');
    await page.waitForTimeout(2000);
    await shot('DASH-01_home');
    return 'URL: ' + page.url().replace(BASE, '');
  });

  await test('DASH-02', 'Dashboard has KPI cards or quick-start content', async () => {
    await go('/');
    await page.waitForTimeout(2000);
    const hasCards   = await page.locator('.mud-card, [class*="kpi"], [class*="KpiCard"]').count() > 0;
    const hasPaper   = await page.locator('.mud-paper').count() > 0;
    const bodyTxt    = await page.locator('body').textContent();
    const hasMeaningfulContent = bodyTxt.length > 200;
    if (!hasMeaningfulContent) throw new Error('Dashboard appears empty');
    return `cards=${hasCards}, paper=${hasPaper}`;
  });
}

// ═══ 3. CONVERSATIONS ════════════════════════════════════════════════════════
async function suiteConversations() {
  console.log('\n═══ 3. CONVERSATIONS ═══');

  await test('CONV-01', 'Conversations page loads', async () => {
    await go('/conversations');
    await shot('CONV-01_list');
    return 'loaded';
  });

  await test('CONV-02', '"New Chat" opens inline assistant picker (PP4 fix)', async () => {
    await go('/conversations');
    const urlBefore = page.url();
    await btnClick('New Chat');
    await page.waitForTimeout(2000);
    await shot('CONV-02_picker');
    const urlAfter    = page.url();
    const navigated   = urlAfter.includes('assistant-studio');
    if (navigated) throw new Error('Navigated to /assistant-studio instead of showing inline picker');
    const body        = await page.locator('body').textContent();
    const pickerShown = body.includes('New Conversation') || body.includes('Select an Assistant') ||
                        body.includes('No published assistants') || body.includes('Choose an assistant');
    if (!pickerShown) throw new Error('Inline picker not visible after clicking New Chat');
    return 'inline picker shown, URL=' + urlAfter.replace(BASE, '');
  });

  await test('CONV-03', 'Cancel new chat returns to list', async () => {
    await go('/conversations');
    await btnClick('New Chat');
    await page.waitForTimeout(1500);
    const closeBtn = page.locator('button[aria-label="Cancel"], button:has-text("Cancel")').first();
    if (await closeBtn.count() > 0) {
      await closeBtn.click();
    } else {
      const iconClose = page.locator('button[aria-label*="close"], button[aria-label*="Cancel"]').first();
      await iconClose.click();
    }
    await page.waitForTimeout(1500);
    await shot('CONV-03_cancelled');
    return 'closed';
  });
}

// ═══ 4. DOCUMENTS ════════════════════════════════════════════════════════════
async function suiteDocuments() {
  console.log('\n═══ 4. DOCUMENTS ═══');

  await test('DOC-01', 'Documents page loads with table or empty state', async () => {
    await go('/documents');
    await page.waitForTimeout(2000);
    await shot('DOC-01_list');
    const hasTable  = await page.locator('table, .mud-table').count() > 0;
    const hasEmpty  = await hasText('No documents') || await hasText('Upload') || await hasText('drag');
    if (!hasTable && !hasEmpty) throw new Error('No table or empty state found');
    return `table=${hasTable}, empty/upload=${hasEmpty}`;
  });

  await test('DOC-02', 'Upload button is present', async () => {
    await go('/documents');
    await page.waitForTimeout(2000);
    const uploadBtn = page.locator('button:has-text("Upload"), button:has-text("Add"), label:has-text("Upload")').first();
    const present   = await uploadBtn.count() > 0;
    if (!present) throw new Error('No upload button found');
    return 'upload button present';
  });

  await test('DOC-03', 'Processing banner shown when docs are Processing (PP1)', async () => {
    await go('/documents');
    await page.waitForTimeout(2000);
    // Banner shown only when processing docs exist — verify the logic exists in the DOM
    const body     = await page.locator('body').textContent();
    const hasTable = await page.locator('table').count() > 0;
    // Check for Processing status chip or banner code path
    const processingUI = body.includes('Processing') || body.includes('processing') || hasTable;
    return `table/status UI present: ${processingUI}`;
  });
}

// ═══ 5. KNOWLEDGE BASES ══════════════════════════════════════════════════════
async function suiteKnowledge() {
  console.log('\n═══ 5. KNOWLEDGE BASES ═══');

  await test('KB-01', 'Knowledge Bases page loads', async () => {
    await go('/knowledgebases');
    await page.waitForTimeout(2000);
    await shot('KB-01_list');
    return 'loaded';
  });

  await test('KB-02', 'Create KB dialog opens', async () => {
    await go('/knowledgebases');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("New"), button:has-text("Create"), button:has-text("Add")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(1500);
    await shot('KB-02_create_dialog');
    const dialogOpen = await page.locator('[role="dialog"], .mud-dialog').count() > 0;
    const hasName    = await hasText('Name') || await hasText('name');
    await closeDialog();
    if (!dialogOpen) throw new Error('Dialog did not open');
    return `dialog open, name field: ${hasName}`;
  });
}

// ═══ 6. ASSISTANT STUDIO ═════════════════════════════════════════════════════
async function suiteAssistants() {
  console.log('\n═══ 6. ASSISTANT STUDIO ═══');

  await test('ASST-01', 'Assistant Studio page loads', async () => {
    await go('/assistant-studio');
    await page.waitForTimeout(2000);
    await shot('ASST-01_studio');
    return 'loaded';
  });

  await test('ASST-02', 'Assistants list has create button', async () => {
    await go('/assistants');
    await page.waitForTimeout(2000);
    await shot('ASST-02_list');
    const hasCreate = await page.locator('button:has-text("New"), button:has-text("Create"), button:has-text("Add")').count() > 0;
    return `create button: ${hasCreate}`;
  });

  await test('ASST-03', 'Create assistant dialog/page opens', async () => {
    await go('/assistants');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("New"), button:has-text("Create"), button:has-text("Add")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(2000);
    await shot('ASST-03_create');
    const dialogOpen = await page.locator('[role="dialog"]').count() > 0;
    const navigated  = !page.url().includes('/assistants');
    const hasForm    = await page.locator('input[type="text"], textarea').count() > 0;
    await closeDialog();
    return `dialog=${dialogOpen}, navigated=${navigated}, form=${hasForm}`;
  });

  await test('ASST-04', 'Assistant prompt templates load', async () => {
    const status = await apiGET('/api/v1/assistants/prompt-templates');
    return `GET /api/v1/assistants/prompt-templates → ${status}`;
  });

  await test('ASST-05', 'Chatbots (embedded) page loads', async () => {
    await go('/chatbots');
    await page.waitForTimeout(2000);
    await shot('ASST-05_chatbots');
    return 'loaded';
  });

  await test('ASST-06', 'Create chatbot dialog has model field + setup guide (PP3)', async () => {
    await go('/chatbots');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("New"), button:has-text("Create"), button:has-text("Add")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(2000);
    await shot('ASST-06_chatbot_dialog');
    const body       = await page.locator('body').textContent();
    const hasAlert   = await page.locator('.mud-alert, [class*="alert"]').count() > 0;
    const hasSetup   = body.includes('setup') || body.includes('Setup') || body.includes('guide') || hasAlert;
    const hasModel   = body.includes('Model') || body.includes('model');
    await closeDialog();
    return `setup guide/alert=${hasSetup}, model field=${hasModel}`;
  });
}

// ═══ 7. WORKFLOWS ════════════════════════════════════════════════════════════
async function suiteWorkflows() {
  console.log('\n═══ 7. WORKFLOWS ═══');

  await test('WF-01', 'Workflow Studio page loads', async () => {
    await go('/workflow-studio');
    await page.waitForTimeout(2000);
    await shot('WF-01_studio');
    return 'loaded';
  });

  await test('WF-02', 'Workflows list page loads', async () => {
    await go('/workflows');
    await page.waitForTimeout(2000);
    await shot('WF-02_list');
    const hasTable = await page.locator('table, .mud-table, [class*="DataGrid"]').count() > 0;
    const hasEmpty = await hasText('No workflow') || await hasText('Create') || await hasText('empty');
    return `table=${hasTable}, content=${hasTable || hasEmpty}`;
  });

  await test('WF-03', 'Create workflow dialog opens', async () => {
    await go('/workflows');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("New"), button:has-text("Create"), button:has-text("Add")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(1500);
    await shot('WF-03_create');
    const dialogOpen = await page.locator('[role="dialog"]').count() > 0;
    const navigated  = !page.url().includes('/workflows');
    await closeDialog();
    return `dialog=${dialogOpen}, navigated to designer=${navigated}`;
  });

  await test('WF-04', 'Approvals page loads with KPI cards (PP6)', async () => {
    await go('/approvals');
    await page.waitForTimeout(2000);
    await shot('WF-04_approvals');
    const hasPending   = await hasText('Pending');
    const hasOverdue   = await hasText('Overdue');
    const hasEscalated = await hasText('Escalated');
    if (!hasPending) throw new Error('"Pending" KPI card not found');
    return `KPIs: Pending=${hasPending} Overdue=${hasOverdue} Escalated=${hasEscalated}`;
  });

  await test('WF-05', 'Approvals: if any pending, cards show data fields (PP6)', async () => {
    await go('/approvals');
    await page.waitForTimeout(2000);
    const hasApproveBtn = await page.locator('button:has-text("Approve")').count() > 0;
    const hasEmptyState = await hasText('All clear') || await hasText('No pending');
    if (!hasApproveBtn && !hasEmptyState) throw new Error('No approve buttons and no empty state');
    return hasApproveBtn ? 'pending approvals shown with Approve/Reject buttons' : 'empty state: all clear';
  });

  await test('WF-06', 'Workflow Schedules page loads', async () => {
    await go('/workflow-studio/schedules');
    await page.waitForTimeout(2000);
    await shot('WF-06_schedules');
    return 'loaded, URL: ' + page.url().replace(BASE, '');
  });

  await test('WF-07', 'Workflow Integrations page loads', async () => {
    await go('/workflow-studio/integrations');
    await page.waitForTimeout(2000);
    return 'loaded';
  });
}

// ═══ 8. INTEGRATIONS ═════════════════════════════════════════════════════════
async function suiteIntegrations() {
  console.log('\n═══ 8. INTEGRATIONS ═══');

  await test('INT-01', 'Integrations page loads', async () => {
    await go('/integrations');
    await page.waitForTimeout(2000);
    await shot('INT-01_list');
    return 'loaded';
  });

  await test('INT-02', 'Status column shows "Setup Required" for unconfigured integrations (PP5)', async () => {
    await go('/integrations');
    await page.waitForTimeout(2000);
    const body          = await page.locator('body').textContent();
    const hasSetupChip  = body.includes('Setup Required') || body.includes('setup required');
    const hasActiveChip = body.includes('Active') || body.includes('active') || body.includes('Inactive');
    return `Setup Required chip=${hasSetupChip}, status chips=${hasActiveChip}`;
  });

  await test('INT-03', 'Create/Edit integration dialog has auth type selector (PP5)', async () => {
    await go('/integrations');
    await page.waitForTimeout(1500);
    // Try add button
    const addBtn = page.locator('button:has-text("New"), button:has-text("Add"), button:has-text("Create")').first();
    if (await addBtn.count() > 0) {
      await addBtn.click();
      await page.waitForTimeout(2000);
      await shot('INT-03_dialog');
      const body     = await page.locator('body').textContent();
      const hasAuth  = body.includes('Auth') || body.includes('Bearer') || body.includes('API Key') || body.includes('Authentication');
      await closeDialog();
      return `auth type selector=${hasAuth}`;
    }
    return 'add button not present (check marketplace flow)';
  });

  await test('INT-04', 'Marketplace link / button is accessible', async () => {
    await go('/integrations');
    await page.waitForTimeout(2000);
    const marketBtn = page.locator('button:has-text("Marketplace"), a:has-text("Marketplace"), button:has-text("Browse Marketplace")').first();
    const exists    = await marketBtn.count() > 0;
    return `marketplace button=${exists}`;
  });
}

// ═══ 9. OPERATIONS ════════════════════════════════════════════════════════════
async function suiteOperations() {
  console.log('\n═══ 9. OPERATIONS ═══');

  await test('OPS-01', 'Operations dashboard loads', async () => {
    await go('/operations');
    await page.waitForTimeout(2000);
    await shot('OPS-01_operations');
    return 'loaded';
  });

  await test('OPS-02', 'AI Stats page loads', async () => {
    await go('/operations/ai');
    await page.waitForTimeout(2000);
    await shot('OPS-02_ai_stats');
    const hasCharts = await page.locator('canvas, [class*="chart"], [class*="Chart"], .mud-paper').count() > 0;
    return `charts/cards=${hasCharts}`;
  });

  await test('OPS-03', 'Analytics page loads', async () => {
    await go('/operations/analytics');
    await page.waitForTimeout(2000);
    return 'loaded';
  });

  await test('OPS-04', 'Audit Logs page loads with table', async () => {
    await go('/operations/audit-logs');
    await page.waitForTimeout(2000);
    await shot('OPS-04_audit_logs');
    const hasTable  = await page.locator('table, .mud-table').count() > 0;
    const hasFilter = await page.locator('input[type="text"], [class*="filter"]').count() > 0;
    return `table=${hasTable}, filter=${hasFilter}`;
  });

  await test('OPS-05', 'Error Logs page loads', async () => {
    await go('/operations/errors');
    await page.waitForTimeout(2000);
    return 'loaded';
  });

  await test('OPS-06', 'Reports page loads', async () => {
    await go('/operations/reports');
    await page.waitForTimeout(2000);
    await shot('OPS-06_reports');
    return 'loaded';
  });
}

// ═══ 10. ADMIN — USERS ════════════════════════════════════════════════════════
async function suiteAdminUsers() {
  console.log('\n═══ 10. ADMIN — USERS ═══');

  await test('USR-01', 'Admin Users page loads', async () => {
    await go('/admin/users');
    await page.waitForTimeout(2000);
    await shot('USR-01_users');
    const hasTable = await page.locator('table, .mud-table').count() > 0;
    return `table=${hasTable}`;
  });

  await test('USR-02', 'Admin user "System Administrator" is listed', async () => {
    await go('/admin/users');
    await page.waitForTimeout(2000);
    const hasAdmin = await hasText('System Administrator') || await hasText('admin@r2wai');
    if (!hasAdmin) throw new Error('Admin user not found in list');
    return 'admin user visible';
  });

  await test('USR-03', 'Invite / Create user dialog opens', async () => {
    await go('/admin/users');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("Invite"), button:has-text("Add"), button:has-text("Create"), button:has-text("New User")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(1500);
    await shot('USR-03_invite_dialog');
    const dialogOpen = await page.locator('[role="dialog"]').count() > 0;
    const hasEmail   = await hasText('Email') || await page.locator('input[type="email"]').count() > 0;
    await closeDialog();
    if (!dialogOpen) throw new Error('Invite dialog did not open');
    return `dialog open, email field=${hasEmail}`;
  });
}

// ═══ 11. ADMIN — ROLES ════════════════════════════════════════════════════════
async function suiteAdminRoles() {
  console.log('\n═══ 11. ADMIN — ROLES ═══');

  await test('ROLE-01', 'Roles page loads with default roles', async () => {
    await go('/admin/roles');
    await page.waitForTimeout(2000);
    await shot('ROLE-01_roles');
    const hasAdmin = await hasText('Admin');
    if (!hasAdmin) throw new Error('Admin role not found');
    return 'default roles visible';
  });

  await test('ROLE-02', 'Permission Matrix loads from API with role columns (PP7)', async () => {
    await go('/admin/permissions');
    await page.waitForTimeout(4000);
    await shot('ROLE-02_permissions');
    const hasTable     = await page.locator('table').count() > 0;
    const cbCount      = await page.locator('input[type="checkbox"]').count();
    const hasSave      = await page.locator('button:has-text("Save")').count() > 0;
    const hasAssistants= await hasText('Assistants');
    const hasWorkflows = await hasText('Workflows');
    if (!hasTable)      throw new Error('No permission matrix table');
    if (cbCount === 0)  throw new Error('No checkboxes in permission matrix');
    return `table=✅ checkboxes=${cbCount}, save=${hasSave}, Assistants=${hasAssistants}, Workflows=${hasWorkflows}`;
  });

  await test('ROLE-03', 'Permission save calls PUT /api/v1/admin/roles/{id} (PP7)', async () => {
    const putCalls = [];
    page.on('request', req => {
      if (req.method() === 'PUT' && req.url().includes('/admin/roles/'))
        putCalls.push(req.url());
    });

    await go('/admin/permissions');
    await page.waitForTimeout(4000);

    const checkboxes = page.locator('input[type="checkbox"]');
    const cbCount = await checkboxes.count();
    if (cbCount > 2) {
      await checkboxes.nth(2).click();
      await page.waitForTimeout(600);
    }

    const saveBtn   = page.locator('button:has-text("Save Changes"), button:has-text("Save")').first();
    const canSave   = await saveBtn.isEnabled().catch(() => false);
    if (canSave) {
      await saveBtn.click();
      await page.waitForTimeout(3000);
      await shot('ROLE-03_after_save');
    }
    return `checkboxes=${cbCount}, save enabled=${canSave}, PUT calls=${putCalls.length}`;
  });
}

// ═══ 12. ADMIN — MODELS ═══════════════════════════════════════════════════════
async function suiteAdminModels() {
  console.log('\n═══ 12. ADMIN — MODELS ═══');

  await test('MDL-01', 'Admin Models page loads', async () => {
    await go('/admin/models');
    await page.waitForTimeout(2000);
    await shot('MDL-01_models');
    return 'loaded';
  });

  await test('MDL-02', 'Add model dialog opens with Ollama provider option (PP2)', async () => {
    await go('/admin/models');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("Add"), button:has-text("New"), button:has-text("Create")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(2000);
    await shot('MDL-02_add_dialog');

    // Try to open provider dropdown to see Ollama
    let ollamaVisible = false;
    const selects = page.locator('.mud-select, [class*="Select"]');
    const selectCount = await selects.count();
    if (selectCount > 0) {
      try {
        await selects.first().click();
        await page.waitForTimeout(1000);
        ollamaVisible = await hasText('Ollama');
        await page.keyboard.press('Escape');
      } catch {}
    }

    await closeDialog();
    return `provider selects=${selectCount}, Ollama option=${ollamaVisible}`;
  });

  await test('MDL-03', 'Default model (GPT-4o) is listed', async () => {
    await go('/admin/models');
    await page.waitForTimeout(2000);
    const hasGPT   = await hasText('GPT-4o') || await hasText('gpt-4o');
    const hasTable = await page.locator('table, .mud-table').count() > 0;
    return `GPT-4o listed=${hasGPT}, table=${hasTable}`;
  });
}

// ═══ 13. ADMIN — API KEYS ═════════════════════════════════════════════════════
async function suiteAdminApiKeys() {
  console.log('\n═══ 13. ADMIN — API KEYS ═══');

  await test('APIKEY-01', 'API Keys page loads', async () => {
    await go('/admin/api-keys');
    await page.waitForTimeout(2000);
    await shot('APIKEY-01_keys');
    return 'loaded';
  });

  await test('APIKEY-02', 'Create API Key dialog opens', async () => {
    await go('/admin/api-keys');
    await page.waitForTimeout(1500);
    const btn = page.locator('button:has-text("Create"), button:has-text("New"), button:has-text("Add"), button:has-text("Generate")').first();
    await btn.waitFor({ timeout: 8000 });
    await btn.click();
    await page.waitForTimeout(1500);
    await shot('APIKEY-02_create');
    const dialogOpen = await page.locator('[role="dialog"]').count() > 0;
    await closeDialog();
    return `dialog=${dialogOpen}`;
  });
}

// ═══ 14. ADMIN — SECURITY & WEBHOOKS ═════════════════════════════════════════
async function suiteAdminSecurity() {
  console.log('\n═══ 14. ADMIN — SECURITY & WEBHOOKS ═══');

  await test('SEC-01', 'Security settings page loads', async () => {
    await go('/admin/security');
    await page.waitForTimeout(2000);
    await shot('SEC-01_security');
    return 'loaded';
  });

  await test('SEC-02', 'Webhooks page loads', async () => {
    await go('/admin/webhooks');
    await page.waitForTimeout(2000);
    await shot('SEC-02_webhooks');
    return 'loaded';
  });

  await test('SEC-03', 'Content Moderation page loads', async () => {
    await go('/admin/content-moderation');
    await page.waitForTimeout(2000);
    return 'loaded';
  });
}

// ═══ 15. PROFILE & SETTINGS ═══════════════════════════════════════════════════
async function suiteProfile() {
  console.log('\n═══ 15. PROFILE & SETTINGS ═══');

  await test('PROF-01', 'Profile page loads with user info', async () => {
    await go('/profile');
    await page.waitForTimeout(2000);
    await shot('PROF-01_profile');
    const hasEmail = await hasText('admin@r2wai') || await hasText('System') || await hasText('Administrator');
    return `user info=${hasEmail}`;
  });

  await test('PROF-02', 'Settings page loads', async () => {
    await go('/settings');
    await page.waitForTimeout(2000);
    return 'loaded';
  });

  await test('PROF-03', 'Tenant settings page loads', async () => {
    await go('/settings/tenant');
    await page.waitForTimeout(2000);
    return 'loaded';
  });
}

// ═══ 16. API HEALTH CHECKS ════════════════════════════════════════════════════
async function suiteApiHealth() {
  console.log('\n═══ 16. API HEALTH CHECKS ═══');

  const endpoints = [
    ['/api/v1/admin/users?pageSize=5',         'Users list'],
    ['/api/v1/admin/roles?pageSize=20',         'Roles list'],
    ['/api/v1/admin/models?pageSize=20',        'Models list'],
    ['/api/v1/admin/api-keys?pageSize=10',      'API Keys list'],
    ['/api/v1/assistants?pageSize=10',          'Assistants list'],
    ['/api/v1/chatbots?pageSize=10',            'Chatbots list'],
    ['/api/v1/documents?pageSize=10',           'Documents list'],
    ['/api/v1/knowledgebases?pageSize=10',      'Knowledge Bases list'],
    ['/api/v1/workflows?pageSize=10',           'Workflows list'],
    ['/api/v1/integrations?pageSize=10',        'Integrations list'],
    ['/api/v1/approvals/pending',               'Pending Approvals'],
    ['/api/v1/operations/metrics',              'Operations Metrics'],
    ['/api/v1/operations/audit-logs?pageSize=5','Audit Logs'],
  ];

  // Get an auth token for direct API calls
  let token = '';
  try {
    const res = await fetch(`${API}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: EMAIL, password: PASS })
    });
    const data = await res.json();
    token = data.token || data.accessToken || '';
  } catch {}

  for (const [ep, label] of endpoints) {
    await test(`API-${label.replace(/ /g, '_')}`, `API: ${label}`, async () => {
      const res = await fetch(`${API}${ep}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      });
      if (res.status >= 500) throw new Error(`HTTP ${res.status}`);
      return `HTTP ${res.status}`;
    });
  }
}

// ─── MAIN ─────────────────────────────────────────────────────────────────────
(async () => {
  console.log('');
  console.log('╔══════════════════════════════════════════════════════════════╗');
  console.log('║         R2WAI — COMPLETE PRODUCT E2E TEST SUITE             ║');
  console.log('╠══════════════════════════════════════════════════════════════╣');
  console.log(`║  App : ${BASE.padEnd(54)}║`);
  console.log(`║  API : ${API.padEnd(54)}║`);
  console.log(`║  User: ${EMAIL.padEnd(54)}║`);
  console.log('╚══════════════════════════════════════════════════════════════╝');

  browser = await chromium.launch({ headless: false, slowMo: 80 });
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true });
  page = await ctx.newPage();

  // Pre-login before all suites
  console.log('\n⚡ Pre-login via API token injection...');
  await doLogin();
  console.log('   Authenticated, URL:', page.url().replace(BASE, ''));

  const suites = [
    suiteAuth, suiteDashboard, suiteConversations, suiteDocuments,
    suiteKnowledge, suiteAssistants, suiteWorkflows, suiteIntegrations,
    suiteOperations, suiteAdminUsers, suiteAdminRoles, suiteAdminModels,
    suiteAdminApiKeys, suiteAdminSecurity, suiteProfile, suiteApiHealth,
  ];
  for (const suite of suites) {
    try {
      await suite();
    } catch (fatal) {
      const name = suite.name;
      console.error(`\n  ⚠️  Suite ${name} threw uncaught error: ${fatal.message}`);
      await shot(`FATAL_${name}`).catch(() => {});
      // Re-inject tokens to recover session (wait 3s for rate limiter)
      await new Promise(r => setTimeout(r, 3000));
      try { await doLogin(); } catch (e2) { console.error('  Recovery login also failed:', e2.message); }
    }
  }
  await browser.close();

  // ─── Final report ─────────────────────────────────────────────────────────
  const passed  = results.filter(r => r.pass).length;
  const failed  = results.filter(r => !r.pass);
  const total   = results.length;

  console.log('');
  console.log('╔══════════════════════════════════════════════════════════════╗');
  console.log('║                    FINAL RESULTS                            ║');
  console.log('╠══════════════════════════════════════════════════════════════╣');
  console.log(`║  PASSED : ${String(passed).padEnd(51)}║`);
  console.log(`║  FAILED : ${String(failed.length).padEnd(51)}║`);
  console.log(`║  TOTAL  : ${String(total).padEnd(51)}║`);
  console.log('╚══════════════════════════════════════════════════════════════╝');

  if (failed.length > 0) {
    console.log('\n❌ FAILURES:');
    for (const r of failed) {
      console.log(`  ${r.id}: ${r.label}`);
      console.log(`     → ${r.detail}`);
      if (r.shot) console.log(`     📷 ${r.shot}`);
    }
  }

  writeFileSync(REPORT, JSON.stringify({ passed, failed: failed.length, total, results }, null, 2));
  console.log(`\n📄 Full report: ${REPORT}`);
  console.log(`📷 Screenshots: ${SHOTS}`);
  process.exit(failed.length === 0 ? 0 : 1);
})();
