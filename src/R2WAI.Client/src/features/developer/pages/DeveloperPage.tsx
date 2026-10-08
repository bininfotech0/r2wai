import { useMemo, useState } from 'react'
import { Box, Button, Chip, Grid, Paper, Stack, Tab, Tabs, Typography } from '@mui/material'
import CodeIcon from '@mui/icons-material/Code'
import KeyOutlined from '@mui/icons-material/KeyOutlined'
import WebhookOutlined from '@mui/icons-material/WebhookOutlined'
import InfoOutlined from '@mui/icons-material/InfoOutlined'
import { CodeSnippet } from '../../../components/CodeSnippet'
import { DeveloperTab } from '../../settings/components/DeveloperTab'

/**
 * Developer hub.
 *
 * Composes the existing settings DeveloperTab rather than re-implementing API-key and
 * webhook management — those forms, dialogs and mutations already work and are covered by
 * `settings`; a second copy here would drift from them immediately.
 *
 * The quickstart is generated from the *real* request contracts, not from aspirational
 * documentation. Two details it deliberately gets right that a hand-written sample would
 * usually get wrong:
 *   - `POST /chat/conversations/{id}/messages` is `[Consumes("multipart/form-data")]`, so
 *     the sample must not send a JSON content type or list `content` in a `-d` JSON body.
 *   - `POST /chatbots/{id}/chat` is `[AllowAnonymous]`, so the sample sends no key at all.
 *     It is still gated server-side — the chatbot must be Active, the caller's Origin must
 *     be in the allowlist (`IsRequestOriginAllowed`), and the tenant must be under its
 *     AiUsage cap — which the caveat below states rather than implying it is wide open.
 */
export function DeveloperPage() {
  const [tab, setTab] = useState(0)

  // The SPA is served same-origin and the API is proxied at /api/v1 (lib/api/fetchJson.ts),
  // so the absolute base a caller needs is derived rather than hardcoded to a host.
  const baseUrl = useMemo(
    () => `${window.location.origin}/api/v1`,
    [],
  )

  const curlListAssistants = `curl ${baseUrl}/assistants?page=1&pageSize=20 \\
  -H "X-Api-Key: $R2WAI_API_KEY" \\
  -H "Accept: application/json"`

  const curlCreateConversation = `# 1. Start a conversation
curl -X POST ${baseUrl}/chat/conversations \\
  -H "Authorization: Bearer $R2WAI_TOKEN" \\
  -H "Content-Type: application/json" \\
  -d '{"title":"Support thread"}'

# 2. Send a message — this endpoint is multipart/form-data, not JSON
curl -X POST ${baseUrl}/chat/conversations/$CONVERSATION_ID/messages \\
  -H "Authorization: Bearer $R2WAI_TOKEN" \\
  -F "content=What is our refund policy?" \\
  -F "idempotencyKey=$(uuidgen)"`

  const fetchPublicChat = `// No API key: the public embed endpoint is [AllowAnonymous].
// It still requires an Origin in the chatbot's allowlist, and the
// chatbot to be Active, and the tenant to be under its daily AiUsage cap.
// Reuse one sessionId per conversation so the bot remembers earlier turns;
// omit it and every message is answered on its own.
const sessionId = crypto.randomUUID()
const res = await fetch(\`${baseUrl}/chatbots/\${CHATBOT_ID}/chat\`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ message: 'Hello', sessionId }),
})
const { reply } = await res.json()`

  return (
    <Box>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 2 }}>
        <CodeIcon color="primary" />
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Developer
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Authenticate with an API key, call the REST API, and receive inbound webhooks.
          </Typography>
        </Box>
      </Stack>

      <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'baseline' }, mb: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            Base URL
          </Typography>
          <Chip label="same origin" size="small" variant="outlined" />
          <Box sx={{ flexGrow: 1 }} />
          <Button size="small" startIcon={<KeyOutlined />} onClick={() => setTab(1)}>
            Manage API keys
          </Button>
        </Stack>
        <CodeSnippet code={baseUrl} language="text" />
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          All routes are prefixed <code>/api/v1</code>. Authenticate with either an API key
          (<code>X-Api-Key</code>) or a bearer token from <code>POST /auth/login</code>.
        </Typography>
      </Paper>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" sx={{ mb: 2 }}>
        <Tab icon={<KeyOutlined />} iconPosition="start" label="Quickstart" />
        <Tab icon={<WebhookOutlined />} iconPosition="start" label="Keys & webhooks" />
      </Tabs>

      {tab === 0 ? (
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, lg: 6 }}>
            <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                List assistants
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                <code>GET /assistants</code> — paginated, accepts <code>page</code>,{' '}
                <code>pageSize</code>, <code>search</code> and <code>applicationId</code>.
              </Typography>
              <CodeSnippet code={curlListAssistants} language="bash" />
            </Paper>
          </Grid>

          <Grid size={{ xs: 12, lg: 6 }}>
            <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                Authenticated chat
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                <code>POST /chat/conversations</code> then{' '}
                <code>POST /chat/conversations/&#123;id&#125;/messages</code>. The message
                endpoint is <strong>multipart/form-data</strong>; passing JSON returns 415.
              </Typography>
              <CodeSnippet code={curlCreateConversation} language="bash" />
            </Paper>
          </Grid>

          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                Public embed endpoint
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                <code>POST /chatbots/&#123;id&#125;/chat</code> — the endpoint the website
                widget calls. Takes <code>{'{ "message": "...", "sessionId": "..." }'}</code> and no
                credentials. The optional session id gives the bot memory of that conversation.
              </Typography>
              <CodeSnippet code={fetchPublicChat} language="javascript" />
            </Paper>
          </Grid>

          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                <InfoOutlined fontSize="small" color="warning" />
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                  Not available yet
                </Typography>
              </Stack>
              <Typography variant="body2" color="text.secondary">
                There is no API request log viewer, no published SDK or client library, and
                no MCP registration endpoint yet. A per-key delivery log and an operations
                request log are both specified in{' '}
                <code>docs/api/MISSING-BACKEND-ENDPOINTS.md</code> (sections 3.6 and 2.5).
              </Typography>
            </Paper>
          </Grid>
        </Grid>
      ) : (
        <DeveloperTab />
      )}
    </Box>
  )
}
