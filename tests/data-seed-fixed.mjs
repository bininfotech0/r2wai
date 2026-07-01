// Quick data seeding with proper API calls
import http from 'http';

const API = 'http://localhost:5000/api/v1';
let TOKEN = '';
let pass = 0, fail = 0;

function api(method, path, body, token) {
  return new Promise((resolve) => {
    const data = body ? JSON.stringify(body) : '';
    const p = new URL(API + path);
    const opts = { hostname: p.hostname, port: p.port, path: p.pathname, method };
    opts.headers = { 'Content-Type': 'application/json' };
    if (data) opts.headers['Content-Length'] = Buffer.byteLength(data);
    if (token) opts.headers['Authorization'] = 'Bearer ' + token;
    const req = http.request(opts, res => { let b=''; res.on('data',c=>b+=c); res.on('end',()=>resolve({s:res.statusCode,b})); });
    req.on('error', e => resolve({s:0,b:e.message}));
    if (data) req.write(data);
    req.end();
  });
}

function report(name, ok, detail) {
  const icon = ok ? 'PASS' : 'FAIL';
  console.log(`  [${icon}] ${name.padEnd(45)} ${detail}`);
  if (ok) pass++; else fail++;
}

async function seed(label, path, items, makeBody) {
  console.log(`\n--- ${label} ---`);
  let created = 0, errors = [];
  for (let i = 0; i < items.length; i++) {
    const body = makeBody(items[i], i);
    try {
      const r = await api('POST', path, body, TOKEN);
      if (r.s === 200 || r.s === 201) created++;
      else if (errors.length < 3) errors.push(`#${i}: ${r.s} ${r.b.substring(0,60)}`);
    } catch(e) { if (errors.length < 3) errors.push(`#${i}: ${e.message}`); }
  }
  const ok = created >= 100;
  report(`${label} (${created}/${items.length})`, ok, errors.length ? `Errors: ${errors.join('; ')}` : '');
}

async function main() {
  console.log('=== R2WAI DATA SEEDING ===\n');

  // Login
  const r = await api('POST', '/auth/login', {email:'admin@r2wai.io',password:'R2wai_Admin!2026'});
  try { TOKEN = JSON.parse(r.b).token; } catch(e) {}
  if (!TOKEN) { console.log('Login failed'); process.exit(1); }
  console.log(`Login OK (token: ${TOKEN.substring(0,20)}...)\n`);

  // 1. Assistants
  const aTypes = ['General','HR','IT','Procurement','Finance','Legal','Compliance','Marketing','Sales','Operations'];
  const aNames = [];
  for (let i = 0; i < 105; i++) aNames.push(`Assistant ${i+1} - ${aTypes[i%10]}`);
  await seed('Assistants', '/assistants', aNames, (n,i) => ({
    name: n, description: `Enterprise assistant #${i+1}`, type: aTypes[i%10],
    systemPrompt: 'You are an enterprise assistant.', tools: [], knowledgeBaseIds: [],
    maxTokens: 4096, temperature: 0.3, isActive: true
  }));

  // 2. Chatbots
  const cNames = [];
  for (let i = 0; i < 105; i++) cNames.push(`Chatbot ${i+1}`);
  await seed('Chatbots', '/chatbots', cNames, (n,i) => ({
    name: n, knowledgeBaseId: null, modelConfigurationId: null
  }));

  // 3. Workflows
  const wNames = [];
  for (let i = 0; i < 105; i++) wNames.push(`Workflow ${i+1}`);
  await seed('Workflows', '/workflows', wNames, (n,i) => ({
    name: n, description: `Enterprise workflow #${i+1}`, steps: [], isActive: true
  }));

  // 4. Knowledge Bases
  const kNames = [];
  for (let i = 0; i < 105; i++) kNames.push(`Knowledge Base ${i+1}`);
  await seed('Knowledge Bases', '/knowledgebases', kNames, (n,i) => ({
    name: n, description: `Enterprise KB #${i+1}`, isActive: true
  }));

  // 5. Users
  const users = [];
  for (let i = 0; i < 105; i++) users.push(i);
  await seed('Users', '/admin/users', users, (_,i) => ({
    externalId: `ext_${i}`, firstName: `User${i}`, lastName: `Test${i}`,
    email: `user${i}@enterprise.com`, password: 'User123!', isActive: true
  }));

  // 6. API Keys
  const keys = [];
  for (let i = 0; i < 105; i++) keys.push(i);
  await seed('API Keys', '/admin/api-keys', keys, (_,i) => ({
    name: `API Key ${i+1}`, scopes: ['read','write'], roles: ['Admin'], isActive: true
  }));

  // 7. Webhooks
  const whs = [];
  for (let i = 0; i < 105; i++) whs.push(i);
  await seed('Webhooks', '/admin/webhooks', whs, (_,i) => ({
    name: `Webhook ${i+1}`, slug: `wh-${i}`, url: `https://hooks.r2wai.io/${i}`, triggerType: 'Http', isActive: true
  }));

  // 8. Conversations
  const convs = [];
  for (let i = 0; i < 105; i++) convs.push(i);
  await seed('Conversations', '/chat/conversations', convs, (_,i) => ({
    title: `Conversation ${i+1}`, module: ['HR','IT','Finance','Ops','Legal'][i%5]
  }));

  // 9. Integrations
  const intTypes = ['Http','Email','Database','Script','Custom'];
  const ints = [];
  for (let i = 0; i < 105; i++) ints.push(i);
  await seed('Integrations', '/integrations', ints, (_,i) => ({
    name: `Integration ${i+1}`, type: intTypes[i%5],
    description: `Enterprise integration #${i+1}`,
    endpointUrl: `https://api.int${i}.com`,
    configuration: JSON.stringify({key:`v${i}`})
  }));

  // 10. Model Configs
  const models = [];
  for (let i = 0; i < 105; i++) models.push(i);
  await seed('Model Configs', '/admin/models', models, (_,i) => ({
    name: `Model ${i+1}`, provider: 'openai', modelId: 'gpt-4',
    displayName: `GPT-4 Variant ${i+1}`, endpoint: 'https://api.openai.com/v1',
    capabilities: ['chat'], maxTokens: 4096, isActive: true
  }));

  // 11. Schedules (need valid workflow ID - skip if none exist)
  try {
    const wf = await api('GET', '/workflows?page=1&pageSize=1', null, TOKEN);
    const wfData = JSON.parse(wf.b);
    const wfId = wfData.items?.[0]?.id || wfData[0]?.id || '';
    const scheds = [];
    for (let i = 0; i < 105; i++) scheds.push(i);
    await seed('Schedules', '/workflows/schedules', scheds, (_,i) => ({
      name: `Schedule ${i+1}`, cronExpression: '0 0 * * *',
      workflowId: wfId || '00000000-0000-0000-0000-000000000001', isActive: true
    }));
  } catch(e) { console.log('  Schedules: SKIP (no workflow ID)'); fail++; }

  // 12. Approvals
  const approvs = [];
  for (let i = 0; i < 105; i++) approvs.push(i);
  await seed('Approvals', '/approvals', approvs, (_,i) => ({
    title: `Approval Request ${i+1}`, description: `Enterprise approval #${i+1}`,
    priority: ['High','Medium','Low'][i%3], dueAt: new Date(Date.now()+7*86400000).toISOString()
  }));

  // Summary
  console.log(`\n=== SUMMARY: ${pass} passed, ${fail} failed, ${Math.round(pass/(pass+fail)*100)}% rate ===`);
}

main().catch(e => { console.error('Fatal:', e); process.exit(1); });
