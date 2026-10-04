import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  FormControlLabel,
  Grid,
  Link as MuiLink,
  Paper,
  Stack,
  Switch,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from '@mui/material'
import LanguageIcon from '@mui/icons-material/Language'
import DevicesIcon from '@mui/icons-material/Devices'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import { PageHeader } from '../../../components/PageHeader'
import { ErrorState } from '../../../components/ErrorState'
import { EmptyState } from '../../../components/EmptyState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { CodeSnippet } from '../../../components/CodeSnippet'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { describeApiError } from '../../../lib/api/fetchJson'
import { getChatbot, getChatbotUsage, listChatbots, updateChatbot, updateChatbotWidget } from '../../chatbots/api'
import {
  buildFullChatbotUpdatePayload,
  encodeAllowedOriginsText,
  encodeSuggestedQuestionsText,
  parseAllowedOriginsText,
  parseSuggestedQuestionsText,
  type UpdateChatbotInput,
} from '../../chatbots/types'
import { WidgetPreview } from '../components/WidgetPreview'
import {
  buildWidgetScript,
  encodeWidgetAppearance,
  isValidWidgetColor,
  parseWidgetAppearance,
  WIDGET_COLOR_PRESETS,
  WIDGET_DEFAULT_COLOR,
  type WidgetAppearanceConfig,
} from '../types'

const HEX_COLOR_HELPER = 'Hex colour, e.g. #6d28d9'

function isExactWebOrigin(value: string) {
  try {
    const url = new URL(value)
    return (url.protocol === 'http:' || url.protocol === 'https:') && url.origin === value
  } catch {
    return false
  }
}

/**
 * Website widget deployment — the "connect to a website" surface from the product brief.
 *
 * Column split follows the brief: configure on the left, live preview in the middle,
 * installation and status on the right.
 *
 * Two different persistence models coexist here and the UI says which is which:
 *   - Appearance (title, accent colour, position) is encoded into the embed snippet's
 *     `data-*` attributes, because that is exactly how the shipped widget bundle reads
 *     it. It travels with the embed, not with the chatbot.
 *   - Copy and origin restrictions (welcome message, suggested questions, allowed
 *     domains) are saved to the chatbot through `PUT /chatbots/{id}` and apply to every
 *     embed of it, because the widget fetches them from `/public-info` at load.
 */
export function WidgetDeploymentPage() {
  const { chatbotId } = useParams<{ chatbotId: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [appearance, setAppearance] = useState<WidgetAppearanceConfig>({
    title: '',
    color: WIDGET_DEFAULT_COLOR,
    position: 'bottom-right',
  })
  const [viewport, setViewport] = useState<'desktop' | 'mobile'>('desktop')
  const [welcomeMessage, setWelcomeMessage] = useState('')
  const [suggestedQuestions, setSuggestedQuestions] = useState('')
  const [allowedOrigins, setAllowedOrigins] = useState('')
  const [voiceEnabled, setVoiceEnabled] = useState(false)
  const [colorInput, setColorInput] = useState(WIDGET_DEFAULT_COLOR)

  const listQuery = useQuery({ queryKey: ['chatbots', 'widget-picker'], queryFn: () => listChatbots(1, 200) })

  const chatbotQuery = useQuery({
    queryKey: ['chatbot', chatbotId],
    queryFn: () => getChatbot(chatbotId!),
    enabled: !!chatbotId,
  })

  const usageQuery = useQuery({
    queryKey: ['chatbot-usage', chatbotId],
    queryFn: () => getChatbotUsage(chatbotId!),
    enabled: !!chatbotId,
  })

  const chatbot = chatbotQuery.data

  // Which chatbot the form is currently seeded from. Seed exactly once per id: navigating
  // between two widget configs re-seeds, but a refetch after an autosave or a background
  // invalidation must not blow away edits the user has typed since. Guarding on the id
  // rather than on `chatbot` is what gives that, while still listing `chatbot` as a
  // dependency so the effect is not reading a stale closure.
  const [seededForId, setSeededForId] = useState<string | null>(null)
  useEffect(() => {
    if (!chatbot || seededForId === chatbot.id) return
    setWelcomeMessage(chatbot.welcomeMessage ?? '')
    setSuggestedQuestions(parseSuggestedQuestionsText(chatbot.suggestedQuestions))
    setAllowedOrigins(parseAllowedOriginsText(chatbot.allowedOrigins))
    setVoiceEnabled(chatbot.voiceEnabled)
    const savedAppearance = parseWidgetAppearance(chatbot.widgetSettings)
    setAppearance(savedAppearance)
    setColorInput(savedAppearance.color)
    setSeededForId(chatbot.id)
  }, [chatbot, seededForId])

  // Two independent writes behind one button: chatbot-wide fields (PUT /chatbots/{id}, shared
  // across every embed) and this embed's appearance + generated snippet (PUT .../widget, new —
  // see deploy/types.ts's WidgetAppearanceConfig doc comment for why these were never persisted
  // before). Both must land before the "saved" toast, so the second re-seed above is trustworthy.
  const saveMutation = useMutation({
    mutationFn: async (input: { details: UpdateChatbotInput; embedScript: string; widgetSettings: string }) => {
      await updateChatbot(chatbotId!, input.details)
      return updateChatbotWidget(chatbotId!, input.embedScript, input.widgetSettings)
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(['chatbot', chatbotId], updated)
      void queryClient.invalidateQueries({ queryKey: ['chatbots'] })
      notify('Widget settings saved')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not save widget settings.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  const origin = useMemo(() => (typeof window === 'undefined' ? '' : window.location.origin), [])

  const script = useMemo(
    () => (chatbotId ? buildWidgetScript({ origin, chatbotId, appearance }) : ''),
    [origin, chatbotId, appearance],
  )

  const originList = useMemo(
    () => allowedOrigins.split('\n').map((o) => o.trim()).filter(Boolean),
    [allowedOrigins],
  )

  function handleSave() {
    if (!chatbot || !isValidWidgetColor(colorInput) || originList.some((entry) => !isExactWebOrigin(entry))) return
    saveMutation.mutate({
      details: buildFullChatbotUpdatePayload(chatbot, {
        welcomeMessage: welcomeMessage.trim() || undefined,
        suggestedQuestions: encodeSuggestedQuestionsText(suggestedQuestions),
        allowedOrigins: encodeAllowedOriginsText(allowedOrigins),
        voiceEnabled,
      }),
      embedScript: script,
      widgetSettings: encodeWidgetAppearance(appearance),
    })
  }

  // "/deploy/widget" with no id is a picker rather than a redirect, because a tenant can
  // legitimately have zero or several chatbots and either case needs its own empty state.
  if (!chatbotId) {
    return (
      <Box>
        <PageHeader
          title="Website Widget"
          description="Embed a published agent as a floating chat widget on any website."
        />
        {listQuery.isLoading ? (
          <LoadingSkeleton count={3} />
        ) : listQuery.error ? (
          <ErrorState
            {...describeApiError(listQuery.error, 'Could not load your chatbots.')}
            onRetry={() => void listQuery.refetch()}
          />
        ) : (listQuery.data?.items.length ?? 0) === 0 ? (
          <EmptyState
            icon={LanguageIcon}
            title="No chatbots to deploy yet"
            description="A website widget is deployed from a chatbot. Create one to get an install snippet."
            actionLabel="Go to Chatbots"
            onAction={() => navigate('/chatbots')}
          />
        ) : (
          <Grid container spacing={2}>
            {listQuery.data!.items.map((c) => (
              <Grid key={c.id} size={{ xs: 12, sm: 6, md: 4 }}>
                <Paper
                  variant="outlined"
                  sx={{ p: 2, height: '100%', display: 'flex', flexDirection: 'column', gap: 1 }}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 600 }} noWrap title={c.name}>
                    {c.name}
                  </Typography>
                  <Stack direction="row" spacing={0.75} sx={{ flexWrap: 'wrap', gap: 0.5 }}>
                    <Chip
                      size="small"
                      variant="outlined"
                      label={c.status}
                      color={c.status === 'Active' ? 'success' : 'default'}
                    />
                    <Chip
                      size="small"
                      variant="outlined"
                      label={parseAllowedOriginsText(c.allowedOrigins).split('\n').filter(Boolean).length === 0 ? 'No domain restriction' : 'Domains restricted'}
                    />
                  </Stack>
                  <Button
                    size="small"
                    variant="outlined"
                    sx={{ mt: 'auto' }}
                    onClick={() => navigate(`/deploy/widget/${c.id}`)}
                  >
                    Configure widget
                  </Button>
                </Paper>
              </Grid>
            ))}
          </Grid>
        )}
      </Box>
    )
  }

  if (chatbotQuery.isLoading) {
    return (
      <Box>
        <PageHeader title="Website Widget" />
        <LoadingSkeleton variant="text" count={6} height={56} />
      </Box>
    )
  }

  if (chatbotQuery.error) {
    return (
      <Box>
        <PageHeader title="Website Widget" />
        <ErrorState
          {...describeApiError(chatbotQuery.error, 'Could not load this chatbot.')}
          onRetry={() => void chatbotQuery.refetch()}
        />
      </Box>
    )
  }

  if (!chatbot) return <Navigate to="/deploy/widget" replace />

  const colorValid = isValidWidgetColor(colorInput)
  const invalidOrigins = originList.filter((entry) => !isExactWebOrigin(entry))

  return (
    <Box>
      <PageHeader
        title={`Widget · ${chatbot.name}`}
        description="Configure the widget, preview it, then copy one script tag into your site."
        actions={
          <Button variant="contained" onClick={handleSave} disabled={saveMutation.isPending || !colorValid || invalidOrigins.length > 0}>
            {saveMutation.isPending ? 'Saving…' : 'Save settings'}
          </Button>
        }
      />

      {chatbot.status !== 'Active' && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          This chatbot is <strong>{chatbot.status.toLowerCase()}</strong>. The widget will still load and fetch
          its configuration, but the agent will not serve production traffic until the chatbot is active.
        </Alert>
      )}

      <Grid container spacing={2}>
        {/* ---------------- Left: configuration ---------------- */}
        <Grid size={{ xs: 12, lg: 4 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Stack spacing={3}>
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                  Appearance
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
                  Applied through the install script's attributes, so it travels with this embed rather than the
                  chatbot record.
                </Typography>
                <Stack spacing={1.5}>
                  <TextField
                    size="small"
                    label="Widget title"
                    value={appearance.title}
                    placeholder="Chat"
                    helperText="Leave blank to use the widget's default title."
                    onChange={(e) => setAppearance((a) => ({ ...a, title: e.target.value }))}
                  />

                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75 }}>
                      Accent colour
                    </Typography>
                    <Stack direction="row" spacing={0.75} sx={{ flexWrap: 'wrap', mb: 1 }}>
                      {WIDGET_COLOR_PRESETS.map((preset) => (
                        <Tooltip key={preset.value} title={preset.label}>
                          <Box
                            component="button"
                            type="button"
                            aria-label={`${preset.label} accent`}
                            aria-pressed={colorInput.toLowerCase() === preset.value}
                            onClick={() => {
                              setColorInput(preset.value)
                              setAppearance((a) => ({ ...a, color: preset.value }))
                            }}
                            sx={{
                              width: 26,
                              height: 26,
                              borderRadius: '8px',
                              bgcolor: preset.value,
                              border: '2px solid',
                              borderColor: colorInput.toLowerCase() === preset.value ? 'text.primary' : 'transparent',
                              cursor: 'pointer',
                              p: 0,
                            }}
                          />
                        </Tooltip>
                      ))}
                    </Stack>
                    <TextField
                      size="small"
                      fullWidth
                      label="Hex value"
                      value={colorInput}
                      error={!colorValid}
                      helperText={colorValid ? HEX_COLOR_HELPER : 'Enter a #rgb or #rrggbb value.'}
                      onChange={(e) => {
                        setColorInput(e.target.value)
                        if (isValidWidgetColor(e.target.value)) {
                          setAppearance((a) => ({ ...a, color: e.target.value.trim() }))
                        }
                      }}
                      slotProps={{ input: { startAdornment: <Box sx={{ width: 14, height: 14, borderRadius: '4px', bgcolor: colorValid ? colorInput : 'transparent', border: '1px solid', borderColor: 'divider', mr: 1 }} /> } }}
                    />
                  </Box>

                  <Box>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75 }}>
                      Position
                    </Typography>
                    <ToggleButtonGroup
                      size="small"
                      exclusive
                      value={appearance.position}
                      onChange={(_, v) => v && setAppearance((a) => ({ ...a, position: v }))}
                    >
                      <ToggleButton value="bottom-right">Bottom right</ToggleButton>
                      <ToggleButton value="bottom-left">Bottom left</ToggleButton>
                    </ToggleButtonGroup>
                  </Box>
                </Stack>
              </Box>

              <Divider />

              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                  Conversations
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
                  Saved to the chatbot. The widget fetches these from its public info endpoint when it loads.
                </Typography>
                <Stack spacing={1.5}>
                  <TextField
                    size="small"
                    label="Welcome message"
                    value={welcomeMessage}
                    multiline
                    minRows={2}
                    onChange={(e) => setWelcomeMessage(e.target.value)}
                  />
                  <TextField
                    size="small"
                    label="Suggested questions"
                    value={suggestedQuestions}
                    multiline
                    minRows={3}
                    placeholder={'How do I track my order?\nWhat are your opening hours?'}
                    helperText="One question per line. Shown as clickable chips when the widget opens."
                    onChange={(e) => setSuggestedQuestions(e.target.value)}
                  />
                  <FormControlLabel
                    control={<Switch checked={voiceEnabled} onChange={(e) => setVoiceEnabled(e.target.checked)} />}
                    label={<Typography variant="body2">Enable voice input and read-aloud</Typography>}
                  />
                </Stack>
              </Box>

              <Divider />

              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                  Allowed domains
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
                  Exact origins the widget will answer requests from, checked server-side on every message. Leave
                  empty to allow any site.
                </Typography>
                <TextField
                  size="small"
                  label="Origins"
                  value={allowedOrigins}
                  multiline
                  minRows={3}
                  placeholder={'https://www.example.com\nhttps://staging.example.com'}
                  error={invalidOrigins.length > 0}
                  helperText={invalidOrigins.length > 0
                    ? 'Use exact http:// or https:// origins only, with no path, query, or wildcard.'
                    : 'One exact origin per line, including scheme.'}
                  onChange={(e) => setAllowedOrigins(e.target.value)}
                />
                {originList.length > 0 && (
                  <Stack direction="row" spacing={0.5} sx={{ flexWrap: 'wrap', gap: 0.5, mt: 1 }}>
                    {originList.map((o) => (
                      <Chip key={o} size="small" label={o} onDelete={() => setAllowedOrigins(originList.filter((x) => x !== o).join('\n'))} />
                    ))}
                  </Stack>
                )}
              </Box>
            </Stack>
          </Paper>
        </Grid>

        {/* ---------------- Centre: live preview ---------------- */}
        <Grid size={{ xs: 12, lg: 5 }}>
          <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Live preview
              </Typography>
              <ToggleButtonGroup size="small" exclusive value={viewport} onChange={(_, v) => v && setViewport(v)}>
                <ToggleButton value="desktop" aria-label="Desktop preview">
                  <DevicesIcon fontSize="small" />
                </ToggleButton>
                <ToggleButton value="mobile" aria-label="Mobile preview">
                  <LanguageIcon fontSize="small" />
                </ToggleButton>
              </ToggleButtonGroup>
            </Stack>
            <WidgetPreview
              appearance={appearance}
              welcomeMessage={welcomeMessage}
              suggestedQuestions={suggestedQuestions.split('\n').map((q) => q.trim()).filter(Boolean)}
              voiceEnabled={voiceEnabled}
              viewport={viewport}
            />
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
              Reproduces the shipped widget bundle. Replies are simulated — the public chat endpoint only accepts
              requests from your allowed domains, so it cannot be called from this page.
            </Typography>
          </Paper>
        </Grid>

        {/* ---------------- Right: installation ---------------- */}
        <Grid size={{ xs: 12, lg: 3 }}>
          <Stack spacing={2}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                Install
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
                Paste this before the closing <code>&lt;/body&gt;</code> tag on your site. You copy one script tag
                — R2WAI hosts the widget runtime and serves the chat API.
              </Typography>
              <CodeSnippet code={script} language="html" maxHeight={180} />
              <Divider sx={{ my: 1.5 }} />
              <Stack spacing={1}>
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<OpenInNewIcon />}
                  component="a"
                  href={`/deploy/widget/${chatbotId}`}
                  target="_blank"
                  rel="noreferrer"
                >
                  Open this page
                </Button>
                <MuiLink
                  href={`${window.location.origin}/widget/widget.js`}
                  target="_blank"
                  rel="noreferrer"
                  underline="hover"
                  sx={{ fontSize: '0.8125rem' }}
                >
                  View the widget bundle that will be served
                </MuiLink>
              </Stack>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Installation status
                </Typography>
                <Button size="small" onClick={() => void chatbotQuery.refetch()} disabled={chatbotQuery.isFetching}>
                  {chatbotQuery.isFetching ? 'Checking…' : 'Refresh'}
                </Button>
              </Stack>
              <Stack spacing={1.25}>
                <StatusRow
                  ok={chatbot.status === 'Active'}
                  label="Chatbot active"
                  detail={chatbot.status === 'Active' ? 'The widget will serve conversations.' : 'Activate the chatbot to serve traffic.'}
                />
                <StatusRow
                  ok={originList.length > 0}
                  label="Domain restriction"
                  detail={
                    originList.length > 0
                      ? `Limited to ${originList.length} origin${originList.length === 1 ? '' : 's'}.`
                      : 'Any site can embed this widget.'
                  }
                />
                <StatusRow
                  ok={!!chatbot.widgetLastSeenAt}
                  label="Embed verification"
                  detail={
                    chatbot.widgetLastSeenAt
                      ? `Verified — loaded from ${chatbot.widgetLastSeenOrigin ?? 'an allowed origin'} on ${new Date(chatbot.widgetLastSeenAt).toLocaleString()}.`
                      : 'Not detected yet. Install the script above, open the page in a browser, then Refresh — this updates the first time a real browser loads the widget.'
                  }
                />
              </Stack>
              <Divider sx={{ my: 1.5 }} />
              <Alert severity="info" sx={{ '& .MuiAlert-message': { fontSize: '0.8125rem' } }}>
                You only copy and paste the script. R2WAI runs the widget and its backend — nothing is installed on
                your servers.
              </Alert>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
                Usage
              </Typography>
              {usageQuery.isLoading ? (
                <LoadingSkeleton variant="text" count={2} height={20} />
              ) : usageQuery.isError ? (
                <ErrorState title="Unable to load widget usage" onRetry={() => void usageQuery.refetch()} />
              ) : (
                <Stack spacing={0.75}>
                  <Typography variant="body2">
                    <strong>{usageQuery.data!.totalMessagesServed.toLocaleString()}</strong> messages served
                    (lifetime, across the website widget and the generic webhook).
                  </Typography>
                  <Typography variant="body2">
                    👍 <strong>{usageQuery.data!.positiveFeedbackCount.toLocaleString()}</strong> · 👎{' '}
                    <strong>{usageQuery.data!.negativeFeedbackCount.toLocaleString()}</strong>
                    {usageQuery.data!.positiveFeedbackCount + usageQuery.data!.negativeFeedbackCount === 0
                      ? ' — no feedback submitted yet.'
                      : ''}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {usageQuery.data!.tenantDailyCap === null
                      ? 'No daily usage cap is configured for this organisation.'
                      : `Organisation-wide today: ${usageQuery.data!.tenantDailyUsed.toLocaleString()} / ${usageQuery.data!.tenantDailyCap.toLocaleString()} requests. This cap is shared by every assistant and chatbot in your organisation, not just this widget.`}
                  </Typography>
                </Stack>
              )}
            </Paper>

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
                Before you publish
              </Typography>
              <Typography variant="body2" color="text.secondary">
                The chat endpoint this widget calls is unauthenticated — anyone who knows the chatbot id can send it
                messages. There is no per-embed token or dedicated rate limit beyond the tenant's usage cap. Do not
                link sensitive knowledge to a publicly embedded chatbot.
              </Typography>
            </Paper>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  )
}

function StatusRow({ ok, label, detail }: { ok: boolean; label: string; detail: string }) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
      <CheckCircleIcon
        fontSize="small"
        sx={{ color: ok ? 'success.main' : 'text.disabled', mt: 0.25, flexShrink: 0 }}
      />
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {label}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {detail}
        </Typography>
      </Box>
    </Stack>
  )
}
