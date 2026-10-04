import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Navigate, useParams } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Divider,
  Grid,
  Paper,
  Stack,
  Step,
  StepContent,
  StepLabel,
  Stepper,
  TextField,
  Typography,
} from '@mui/material'
import { PageHeader } from '../../../components/PageHeader'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { CodeSnippet } from '../../../components/CodeSnippet'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { describeApiError } from '../../../lib/api/fetchJson'
import {
  addChatbotChannel,
  getChatbot,
  listChatbotChannels,
  removeChatbotChannel,
} from '../../chatbots/api'
import type { ChatbotChannelDto } from '../../chatbots/types'
import { CHANNEL_CAPABILITIES } from '../types'

interface WhatsAppCredentials {
  phoneNumberId: string
  businessAccountId: string
  accessToken: string
}

/**
 * WhatsApp deployment.
 *
 * The honest scope of this page, and why it reads the way it does:
 *
 *   `POST /chatbots/{id}/channels/WhatsApp` validates the enum and stores the submitted
 *   payload encrypted. That is a real, persisted write — so the form below genuinely
 *   saves. But the platform ships no WhatsApp Cloud API adapter: there is no token
 *   exchange, no signature-verified inbound webhook, and no outbound sender. So this
 *   page does not offer a "Test connection" button that would only report that a row was
 *   written, and it never claims the business account is connected.
 *
 * The credentials are write-only. The API returns no channel payload to read back, so
 * once saved the values cannot be displayed again — the form shows a "configured" state
 * and re-entry requires retyping. That is the correct behaviour, not a missing feature.
 */
export function WhatsAppSetupPage() {
  const { chatbotId } = useParams<{ chatbotId: string }>()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [form, setForm] = useState<WhatsAppCredentials>({ phoneNumberId: '', businessAccountId: '', accessToken: '' })
  const [step, setStep] = useState(0)

  const chatbotQuery = useQuery({
    queryKey: ['chatbot', chatbotId],
    queryFn: () => getChatbot(chatbotId!),
    enabled: !!chatbotId,
  })
  const channelsQuery = useQuery({
    queryKey: ['chatbot', chatbotId, 'channels'],
    queryFn: () => listChatbotChannels(chatbotId!),
    enabled: !!chatbotId,
  })

  useEffect(() => {
    setForm({ phoneNumberId: '', businessAccountId: '', accessToken: '' })
    setStep(0)
  }, [chatbotId])

  const saveMutation = useMutation({
    mutationFn: (credentials: WhatsAppCredentials) =>
      addChatbotChannel(chatbotId!, 'WhatsApp', JSON.stringify(credentials)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['chatbot', chatbotId, 'channels'] })
      // Never repopulate the token field from anything — clear it on success.
      setForm({ phoneNumberId: '', businessAccountId: '', accessToken: '' })
      setStep(1)
      notify('WhatsApp credentials stored')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not store the WhatsApp configuration.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  const removeMutation = useMutation({
    mutationFn: () => removeChatbotChannel(chatbotId!, 'WhatsApp'),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['chatbot', chatbotId, 'channels'] })
      setStep(0)
      notify('WhatsApp configuration removed', 'info')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not remove the WhatsApp configuration.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  const whatsappChannel = (channelsQuery.data ?? []).find((c: ChatbotChannelDto) => c.channel === 'WhatsApp')
  const isConfigured = channelsQuery.isSuccess && !!whatsappChannel
  const capability = CHANNEL_CAPABILITIES.WhatsApp
  const canSubmit = form.phoneNumberId.trim() && form.businessAccountId.trim() && form.accessToken.trim()

  if (chatbotQuery.isLoading) {
    return (
      <Box>
        <PageHeader title="WhatsApp" />
        <LoadingSkeleton variant="text" count={6} height={56} />
      </Box>
    )
  }
  if (chatbotQuery.error) {
    return (
      <Box>
        <PageHeader title="WhatsApp" />
        <ErrorState {...describeApiError(chatbotQuery.error, 'Could not load this chatbot.')} onRetry={() => void chatbotQuery.refetch()} />
      </Box>
    )
  }
  if (!chatbotId || !chatbotQuery.data) return <Navigate to="/deploy" replace />

  // Storing credentials over an existing configuration replaces it, so a failed channel
  // read must not be allowed to look like "nothing stored" and invite a blind overwrite.
  if (channelsQuery.isError) {
    return (
      <Box>
        <PageHeader title={`WhatsApp · ${chatbotQuery.data.name}`} />
        <ErrorState
          {...describeApiError(
            channelsQuery.error,
            'Could not read the existing WhatsApp configuration for this chatbot.',
          )}
          onRetry={() => void channelsQuery.refetch()}
        />
      </Box>
    )
  }

  const chatbot = chatbotQuery.data

  return (
    <Box>
      <PageHeader
        title={`WhatsApp · ${chatbot.name}`}
        description="Connect this agent to the WhatsApp Business Platform."
      />

      <Alert severity="warning" sx={{ mb: 2 }}>
        {capability.note}
      </Alert>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Stepper activeStep={step} orientation="vertical">
              <Step expanded>
                <StepLabel>Business credentials</StepLabel>
                <StepContent>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    These are stored encrypted at rest and are never returned by the API, so they cannot be displayed
                    again after saving. Treat them like a password: if you lose them, create a new access token in
                    the Meta developer console.
                  </Typography>
                  <Stack spacing={2}>
                    <TextField
                      size="small"
                      label="Phone number ID"
                      value={form.phoneNumberId}
                      helperText="From the WhatsApp Business Account dashboard, not the phone number itself."
                      onChange={(e) => setForm((f) => ({ ...f, phoneNumberId: e.target.value }))}
                    />
                    <TextField
                      size="small"
                      label="Business account ID (WABA ID)"
                      value={form.businessAccountId}
                      onChange={(e) => setForm((f) => ({ ...f, businessAccountId: e.target.value }))}
                    />
                    <TextField
                      size="small"
                      label="Permanent access token"
                      type="password"
                      value={form.accessToken}
                      autoComplete="off"
                      onChange={(e) => setForm((f) => ({ ...f, accessToken: e.target.value }))}
                    />
                    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'center' } }}>
                      <Button
                        variant="contained"
                        disabled={!canSubmit || saveMutation.isPending || channelsQuery.isLoading}
                        onClick={() => saveMutation.mutate(form)}
                      >
                        {channelsQuery.isLoading ? 'Checking…' : saveMutation.isPending ? 'Saving…' : isConfigured ? 'Replace credentials' : 'Store credentials'}
                      </Button>
                      {isConfigured && (
                        <Button
                          color="error"
                          disabled={removeMutation.isPending}
                          onClick={() => removeMutation.mutate()}
                        >
                          Remove
                        </Button>
                      )}
                    </Stack>
                  </Stack>
                </StepContent>
              </Step>

              <Step>
                <StepLabel>Callback URL</StepLabel>
                <StepContent>
                  <Alert severity="warning" sx={{ mb: 1.5 }}>
                    This endpoint is <strong>not deployed</strong>. Registering it in Meta today
                    will not deliver messages — inbound WhatsApp messages will not reach the
                    agent until the provider adapter ships (see{' '}
                    <code>docs/api/MISSING-BACKEND-ENDPOINTS.md</code> §2.4, items 38 and 43).
                  </Alert>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                    The planned inbound path, keyed by the WhatsApp phone number ID rather than
                    the chatbot ID. It will verify Meta&apos;s <code>X-Hub-Signature-256</code>{' '}
                    HMAC before accepting anything.
                  </Typography>
                  <CodeSnippet
                    language="planned callback URL"
                    code={`${typeof window === 'undefined' ? '' : window.location.origin}/api/v1/chatbots/whatsapp/${form.phoneNumberId.trim() || '{phoneNumberId}'}/webhook`}
                  />
                </StepContent>
              </Step>

              <Step>
                <StepLabel>Message templates and policy</StepLabel>
                <StepContent>
                  <Alert severity="info">
                    Template approval status and messaging policy are read from Meta's Graph API. That integration
                    is not deployed, so this page cannot show your current template approval state.
                  </Alert>
                </StepContent>
              </Step>

              <Step>
                <StepLabel>Verify and publish</StepLabel>
                <StepContent>
                  <Alert severity="info">
                    <strong>Test connection is unavailable.</strong> A live check requires calling Meta's Graph API
                    with the stored token, and no adapter is deployed to do it. Publishing the channel here would
                    show a green tick for something that cannot send a message, so the control is withheld.
                  </Alert>
                </StepContent>
              </Step>
            </Stepper>
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, md: 5 }}>
          <Stack spacing={2}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>
                Connection status
              </Typography>
              <Stack spacing={1.25}>
                <StatusLine
                  ok={isConfigured}
                  label="Credentials stored"
                  value={channelsQuery.isLoading
                    ? 'Checking existing configuration…'
                    : isConfigured
                      ? `Stored ${new Date(whatsappChannel!.connectedAt).toLocaleString()}`
                      : 'Not stored'}
                />
                <StatusLine
                  ok={false}
                  label="Provider verification"
                  value="Unavailable — no WhatsApp Cloud API adapter is deployed, so the business account cannot be confirmed."
                />
                <StatusLine
                  ok={false}
                  label="Inbound messages"
                  value="Unavailable — the callback URL is not yet routed to the agent."
                />
                <StatusLine
                  ok={false}
                  label="Outbound delivery"
                  value="Unavailable — no sender is implemented for this channel."
                />
                <StatusLine
                  ok={chatbot.status === 'Active'}
                  label="Chatbot active"
                  value={chatbot.status === 'Active' ? 'Yes' : `No — currently ${chatbot.status}`}
                />
              </Stack>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
                How messaging works here
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                R2WAI operates the messaging runtime and the agent behind it. WhatsApp remains the system of record for
                the conversation; R2WAI adds the AI layer once the provider connection is live.
              </Typography>
              <Divider sx={{ my: 1.5 }} />
              <Typography variant="caption" color="text.secondary">
                Tracked in <code>docs/api/MISSING-BACKEND-ENDPOINTS.md</code> §2.4 — endpoints 38–43.
              </Typography>
            </Paper>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  )
}

function StatusLine({ ok, label, value }: { ok: boolean; label: string; value: string }) {
  return (
    <Box>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Box
          aria-hidden
          sx={{ width: 8, height: 8, borderRadius: '50%', flexShrink: 0, bgcolor: ok ? 'success.main' : 'text.disabled' }}
        />
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {label}
        </Typography>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ pl: 2.25, display: 'block' }}>
        {value}
      </Typography>
    </Box>
  )
}
