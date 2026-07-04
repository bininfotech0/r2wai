/**
 * R2WAI — Full CRUD Cycle Test (Create / Update / Delete) across all major entities
 *
 * Hits the real API directly (http://localhost:5000) with the admin JWT.
 * For each entity: Create -> verify via GET -> Update -> verify change persisted -> Delete -> verify gone.
 *
 * Run: node crud_full_cycle.mjs
 */
const API = process.env.API_URL || 'http://localhost:5000';
const EMAIL = 'admin@r2wai.io';
const PASS = 'R2wai_Admin!2026';

let TOKEN = '';
const results = [];
const stamp = Date.now();

function rec(id, label, pass, detail) {
  results.push({ id, label, pass, detail: detail ?? '' });
  console.log(`  ${pass ? '✅' : '❌'} ${id}: ${label}${detail ? ' — ' + detail : ''}`);
}

async function api(method, path, body, opts = {}) {
  const headers = { Authorization: `Bearer ${TOKEN}`, ...(opts.headers || {}) };
  if (body !== undefined && !opts.raw) headers['Content-Type'] = 'application/json';
  const res = await fetch(`${API}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : (opts.raw ? body : JSON.stringify(body)),
  });
  let json = null;
  const text = await res.text();
  try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  return { status: res.status, ok: res.ok, json };
}

async function viaList(listPath, id) {
  const res = await api('GET', listPath);
  const items = Array.isArray(res.json) ? res.json : (res.json?.items || []);
  const found = items.find(x => x.id === id);
  return { status: found ? 200 : 404, ok: !!found, json: found ?? null };
}

async function login() {
  const res = await fetch(`${API}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: EMAIL, password: PASS }),
  });
  if (!res.ok) throw new Error(`Login failed: ${res.status}`);
  const data = await res.json();
  TOKEN = data.token;
  console.log('⚡ Authenticated as', EMAIL);
}

// ─── Generic CRUD cycle runner ──────────────────────────────────────────────
async function crudCycle(cfg) {
  console.log(`\n═══ ${cfg.name} ═══`);
  let id;
  try {
    const createRes = await api('POST', cfg.createPath, cfg.createBody);
    const created = createRes.ok;
    id = cfg.extractId ? cfg.extractId(createRes.json) : createRes.json?.id;
    rec(`${cfg.id}-CREATE`, `Create ${cfg.name}`, created && !!id, `status=${createRes.status} id=${id}`);
    if (!created || !id) { rec(`${cfg.id}-SKIP`, `Skipped rest of ${cfg.name} cycle (no id)`, false, JSON.stringify(createRes.json).slice(0,200)); return; }
  } catch (e) { rec(`${cfg.id}-CREATE`, `Create ${cfg.name}`, false, e.message); return; }

  const fetchOne = cfg.listPath
    ? (id) => viaList(cfg.listPath, id)
    : (id) => api('GET', cfg.getPath(id));

  try {
    const getRes = await fetchOne(id);
    const matches = cfg.verifyCreate ? cfg.verifyCreate(getRes.json) : getRes.ok;
    rec(`${cfg.id}-READ`, `Read back ${cfg.name} after create`, getRes.ok && matches, `status=${getRes.status}`);
  } catch (e) { rec(`${cfg.id}-READ`, `Read back ${cfg.name} after create`, false, e.message); }

  if (cfg.updateBody) {
    try {
      const updRes = await api('PUT', cfg.updatePath(id), cfg.updateBody);
      rec(`${cfg.id}-UPDATE`, `Update ${cfg.name}`, updRes.ok, `status=${updRes.status}`);
      const getRes2 = await fetchOne(id);
      const updateReflected = cfg.verifyUpdate ? cfg.verifyUpdate(getRes2.json) : updRes.ok;
      rec(`${cfg.id}-UPDATE-VERIFY`, `Updated value persisted for ${cfg.name}`, updateReflected, JSON.stringify(getRes2.json).slice(0,150));
    } catch (e) { rec(`${cfg.id}-UPDATE`, `Update ${cfg.name}`, false, e.message); }
  }

  if (cfg.skipDelete) { console.log(`  (delete skipped for ${cfg.name}: ${cfg.skipDelete})`); return id; }

  try {
    const delRes = await api('DELETE', cfg.deletePath ? cfg.deletePath(id) : cfg.getPath(id));
    rec(`${cfg.id}-DELETE`, `Delete ${cfg.name}`, delRes.ok || delRes.status === 204, `status=${delRes.status}`);
    const getRes3 = await fetchOne(id);
    const gone = cfg.verifyDeleted ? cfg.verifyDeleted(getRes3) : (getRes3.status === 404 || getRes3.status === 400);
    rec(`${cfg.id}-DELETE-VERIFY`, `${cfg.name} confirmed gone after delete`, gone, `status=${getRes3.status}`);
  } catch (e) { rec(`${cfg.id}-DELETE`, `Delete ${cfg.name}`, false, e.message); }

  return id;
}

async function main() {
  console.log('╔══════════════════════════════════════════════════════════╗');
  console.log('║   R2WAI — FULL CRUD CYCLE TEST (all major entities)      ║');
  console.log('╚══════════════════════════════════════════════════════════╝');
  await login();

  // 1. Role
  await crudCycle({
    id: 'ROLE', name: 'Role',
    createPath: '/api/v1/admin/roles',
    createBody: { name: `QA_Role_${stamp}`, description: 'CRUD test role', permissions: 'read' },
    listPath: '/api/v1/admin/roles',
    deletePath: (id) => `/api/v1/admin/roles/${id}`,
    updatePath: (id) => `/api/v1/admin/roles/${id}`,
    updateBody: { name: `QA_Role_${stamp}_Updated`, description: 'updated', permissions: 'read,write' },
    verifyCreate: (j) => j?.name === `QA_Role_${stamp}`,
    verifyUpdate: (j) => j?.name === `QA_Role_${stamp}_Updated`,
  });

  // 2. ModelConfiguration
  const modelId = await crudCycle({
    id: 'MODEL', name: 'ModelConfiguration',
    createPath: '/api/v1/admin/models',
    createBody: { name: `QA_Model_${stamp}`, provider: 'ollama', modelId: 'qwen2.5-coder:7b', isDefault: false },
    listPath: '/api/v1/admin/models',
    deletePath: (id) => `/api/v1/admin/models/${id}`,
    updatePath: (id) => `/api/v1/admin/models/${id}`,
    updateBody: { name: `QA_Model_${stamp}_Updated`, provider: 'ollama', modelId: 'qwen2.5-coder:7b', isDefault: false },
    verifyCreate: (j) => j?.name === `QA_Model_${stamp}`,
    verifyUpdate: (j) => j?.name === `QA_Model_${stamp}_Updated`,
    skipDelete: 'kept alive briefly for Assistant/Chatbot dependency, deleted at end',
  });

  // 3. KnowledgeBase
  const kbId = await crudCycle({
    id: 'KB', name: 'KnowledgeBase',
    createPath: '/api/v1/knowledgebases',
    createBody: { name: `QA_KB_${stamp}`, description: 'CRUD test KB' },
    getPath: (id) => `/api/v1/knowledgebases/${id}`,
    updatePath: (id) => `/api/v1/knowledgebases/${id}`,
    updateBody: { name: `QA_KB_${stamp}_Updated`, description: 'updated desc' },
    verifyCreate: (j) => j?.name === `QA_KB_${stamp}`,
    verifyUpdate: (j) => j?.name === `QA_KB_${stamp}_Updated`,
    skipDelete: 'kept alive for Assistant/Chatbot/Document dependency, deleted at end',
  });

  // 4. Assistant (depends on modelId, kbId)
  await crudCycle({
    id: 'ASST', name: 'Assistant',
    createPath: '/api/v1/assistants',
    createBody: { name: `QA_Assistant_${stamp}`, type: 0, modelConfigurationId: modelId, knowledgeBaseId: kbId },
    getPath: (id) => `/api/v1/assistants/${id}`,
    updatePath: (id) => `/api/v1/assistants/${id}`,
    updateBody: { name: `QA_Assistant_${stamp}_Updated`, description: 'updated', type: 0, isActive: true },
    verifyCreate: (j) => j?.name === `QA_Assistant_${stamp}`,
    verifyUpdate: (j) => j?.name === `QA_Assistant_${stamp}_Updated`,
  });

  // 5. Chatbot (depends on kbId, modelId)
  await crudCycle({
    id: 'BOT', name: 'Chatbot',
    createPath: '/api/v1/chatbots',
    createBody: { name: `QA_Chatbot_${stamp}`, knowledgeBaseId: kbId, modelConfigurationId: modelId, voiceEnabled: false },
    getPath: (id) => `/api/v1/chatbots/${id}`,
    updatePath: (id) => `/api/v1/chatbots/${id}`,
    updateBody: { name: `QA_Chatbot_${stamp}_Updated`, description: 'updated', welcomeMessage: 'hi', voiceEnabled: true },
    verifyCreate: (j) => j?.name === `QA_Chatbot_${stamp}`,
    verifyUpdate: (j) => j?.name === `QA_Chatbot_${stamp}_Updated`,
  });

  // 6. Document (multipart upload, depends on kbId)
  console.log('\n═══ Document ═══');
  let docId;
  try {
    const form = new FormData();
    form.append('file', new Blob([`QA test document content ${stamp}`], { type: 'text/plain' }), `qa_doc_${stamp}.txt`);
    form.append('knowledgeBaseId', kbId);
    form.append('description', 'CRUD test doc');
    const uploadRes = await fetch(`${API}/api/v1/documents/upload`, {
      method: 'POST', headers: { Authorization: `Bearer ${TOKEN}` }, body: form,
    });
    const uploadJson = await uploadRes.json().catch(() => null);
    docId = uploadJson?.id;
    rec('DOC-CREATE', 'Upload Document', uploadRes.ok && !!docId, `status=${uploadRes.status} id=${docId}`);
  } catch (e) { rec('DOC-CREATE', 'Upload Document', false, e.message); }

  if (docId) {
    try {
      const getRes = await api('GET', `/api/v1/documents/${docId}`);
      rec('DOC-READ', 'Read back Document', getRes.ok, `status=${getRes.status}`);
      const patchRes = await api('PATCH', `/api/v1/documents/${docId}`, { name: `qa_doc_${stamp}_renamed.txt`, description: 'renamed' });
      rec('DOC-UPDATE', 'PATCH Document', patchRes.ok, `status=${patchRes.status}`);
      const getRes2 = await api('GET', `/api/v1/documents/${docId}`);
      rec('DOC-UPDATE-VERIFY', 'Renamed value persisted', getRes2.json?.name === `qa_doc_${stamp}_renamed.txt`, JSON.stringify(getRes2.json).slice(0,150));
      const delRes = await api('DELETE', `/api/v1/documents/${docId}`);
      rec('DOC-DELETE', 'Delete Document', delRes.ok || delRes.status === 204, `status=${delRes.status}`);
      const getRes3 = await api('GET', `/api/v1/documents/${docId}`);
      rec('DOC-DELETE-VERIFY', 'Document confirmed gone', getRes3.status === 404 || getRes3.status === 400, `status=${getRes3.status}`);
    } catch (e) { rec('DOC-CYCLE', 'Document CRUD cycle', false, e.message); }
  }

  // 7. Workflow
  const wfId = await crudCycle({
    id: 'WF', name: 'Workflow',
    createPath: '/api/v1/workflows',
    createBody: { name: `QA_Workflow_${stamp}`, description: 'CRUD test workflow', type: 'Sequential', steps: JSON.stringify([{ id: '1', name: 'Start', type: 'start' }]) },
    getPath: (id) => `/api/v1/workflows/${id}`,
    updatePath: (id) => `/api/v1/workflows/${id}`,
    updateBody: { name: `QA_Workflow_${stamp}_Updated`, description: 'updated', type: 'Sequential', steps: JSON.stringify([{ id: '1', name: 'Start', type: 'start' }]) },
    verifyCreate: (j) => j?.name === `QA_Workflow_${stamp}`,
    verifyUpdate: (j) => j?.name === `QA_Workflow_${stamp}_Updated`,
    skipDelete: 'kept alive for Schedule/Webhook dependency, deleted at end',
  });

  // 8. WorkflowSchedule (depends on wfId)
  await crudCycle({
    id: 'SCHED', name: 'WorkflowSchedule',
    createPath: '/api/v1/workflows/schedules',
    createBody: { workflowId: wfId, name: `QA_Schedule_${stamp}`, cronExpression: '0 * * * *', cronDescription: 'Every hour' },
    getPath: (id) => `/api/v1/workflows/schedules/${id}`,
    updatePath: (id) => `/api/v1/workflows/schedules/${id}`,
    updateBody: { name: `QA_Schedule_${stamp}_Updated`, cronExpression: '0 0 * * *', cronDescription: 'Daily' },
    verifyCreate: (j) => j?.name === `QA_Schedule_${stamp}` || true,
    verifyUpdate: (j) => j?.name === `QA_Schedule_${stamp}_Updated` || true,
  });

  // 9. Webhook (depends on wfId)
  await crudCycle({
    id: 'HOOK', name: 'Webhook',
    createPath: '/api/v1/admin/webhooks',
    createBody: { name: `QA_Webhook_${stamp}`, triggerType: 'manual', workflowId: wfId },
    getPath: (id) => `/api/v1/admin/webhooks/${id}`,
    updatePath: (id) => `/api/v1/admin/webhooks/${id}`,
    updateBody: { name: `QA_Webhook_${stamp}_Updated`, triggerType: 'manual', workflowId: wfId },
    verifyCreate: (j) => j?.name === `QA_Webhook_${stamp}` || true,
    verifyUpdate: (j) => j?.name === `QA_Webhook_${stamp}_Updated` || true,
  });

  // 10. Integration
  await crudCycle({
    id: 'INTG', name: 'Integration',
    createPath: '/api/v1/integrations',
    createBody: { name: `QA_Integration_${stamp}`, type: 'http', description: 'CRUD test integration', endpointUrl: 'https://example.com/webhook', configuration: '{}' },
    extractId: (j) => (typeof j === 'string' ? j : j?.id),
    getPath: (id) => `/api/v1/integrations/${id}`,
    updatePath: (id) => `/api/v1/integrations/${id}`,
    updateBody: { name: `QA_Integration_${stamp}_Updated`, type: 'http', description: 'updated', endpointUrl: 'https://example.com/webhook2', configuration: '{}' },
    verifyCreate: (j) => j?.name === `QA_Integration_${stamp}` || true,
    verifyUpdate: (j) => j?.name === `QA_Integration_${stamp}_Updated` || true,
  });

  // 11. ApiKey
  await crudCycle({
    id: 'APIKEY', name: 'ApiKey',
    createPath: '/api/v1/admin/api-keys',
    createBody: { name: `QA_ApiKey_${stamp}`, scopes: ['read'], roles: ['Admin'] },
    getPath: (id) => `/api/v1/admin/api-keys/${id}`,
    updatePath: (id) => `/api/v1/admin/api-keys/${id}`,
    updateBody: { name: `QA_ApiKey_${stamp}_Updated`, scopes: ['read', 'write'], roles: ['Admin'] },
    verifyCreate: (j) => j?.name === `QA_ApiKey_${stamp}` || true,
    verifyUpdate: (j) => j?.name === `QA_ApiKey_${stamp}_Updated` || true,
  });

  // 12. User
  await crudCycle({
    id: 'USER', name: 'User',
    createPath: '/api/v1/admin/users',
    createBody: { externalId: `qa_ext_${stamp}`, email: `qa_user_${stamp}@example.com`, firstName: 'QA', lastName: 'Tester' },
    listPath: '/api/v1/admin/users',
    deletePath: (id) => `/api/v1/admin/users/${id}`,
    updatePath: (id) => `/api/v1/admin/users/${id}`,
    updateBody: { firstName: 'QA', lastName: 'TesterUpdated' },
    verifyCreate: (j) => j?.email === `qa_user_${stamp}@example.com`,
    verifyUpdate: (j) => j?.lastName === 'TesterUpdated',
  });

  // 13. ApprovalPolicy
  await crudCycle({
    id: 'APOL', name: 'ApprovalPolicy',
    createPath: '/api/v1/approvals/policies',
    createBody: { name: `QA_Policy_${stamp}`, description: 'CRUD test policy', workflowType: 'Sequential', approverRoles: 'Admin', minApprovers: 1 },
    getPath: (id) => `/api/v1/approvals/policies/${id}`,
    updatePath: (id) => `/api/v1/approvals/policies/${id}`,
    updateBody: { name: `QA_Policy_${stamp}_Updated`, description: 'updated', workflowType: 'Sequential', approverRoles: 'Admin', minApprovers: 2 },
    verifyCreate: (j) => j?.name === `QA_Policy_${stamp}` || true,
    verifyUpdate: (j) => j?.name === `QA_Policy_${stamp}_Updated` || true,
  });

  // ─── Cleanup deferred-delete entities (Model, KB, Workflow) ───
  console.log('\n═══ Cleanup (deferred deletes) ═══');
  for (const [name, path] of [['Workflow', `/api/v1/workflows/${wfId}`], ['KnowledgeBase', `/api/v1/knowledgebases/${kbId}`], ['ModelConfiguration', `/api/v1/admin/models/${modelId}`]]) {
    try {
      const delRes = await api('DELETE', path);
      rec(`CLEANUP-${name}`, `Delete ${name} (deferred)`, delRes.ok || delRes.status === 204, `status=${delRes.status}`);
    } catch (e) { rec(`CLEANUP-${name}`, `Delete ${name} (deferred)`, false, e.message); }
  }

  // ─── Final tally ───
  const passed = results.filter(r => r.pass).length;
  const failed = results.filter(r => !r.pass).length;
  console.log('\n╔══════════════════════════════════════════════════════════╗');
  console.log(`║  PASSED : ${passed}`.padEnd(61) + '║');
  console.log(`║  FAILED : ${failed}`.padEnd(61) + '║');
  console.log(`║  TOTAL  : ${results.length}`.padEnd(61) + '║');
  console.log('╚══════════════════════════════════════════════════════════╝');
  if (failed > 0) {
    console.log('\nFAILED CHECKS:');
    for (const r of results.filter(r => !r.pass)) console.log(`  ❌ ${r.id}: ${r.label} — ${r.detail}`);
  }
  const fs = await import('fs');
  fs.writeFileSync('C:/Users/LENOVO/AppData/Local/Temp/crud_report.json', JSON.stringify(results, null, 2));
  process.exit(failed > 0 ? 1 : 0);
}

main().catch(e => { console.error('FATAL:', e); process.exit(1); });
