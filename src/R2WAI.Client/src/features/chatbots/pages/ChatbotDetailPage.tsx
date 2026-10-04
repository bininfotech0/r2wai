import { useEffect, useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import CodeIcon from '@mui/icons-material/Code'
import AddIcon from '@mui/icons-material/Add'
import RefreshIcon from '@mui/icons-material/Refresh'
import InfoOutlined from '@mui/icons-material/InfoOutlined'
import LanguageIcon from '@mui/icons-material/Language'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { ErrorState } from '../../../components/ErrorState'
import { TooltipIconButton } from '../../../components/TooltipIconButton'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import {
  addChatbotChannel,
  deleteChatbot,
  getChatbot,
  getWebhookKeyInfo,
  listChatbotChannels,
  regenerateWebhookKey,
  removeChatbotChannel,
  setChatbotStatus,
  updateChatbot,
} from '../api'
import { ChatbotEmbedDialog } from '../dialogs/ChatbotEmbedDialog'
import {
  buildFullChatbotUpdatePayload,
  CHATBOT_CHANNEL_TYPES,
  encodeAllowedOriginsText,
  encodeSuggestedQuestionsText,
  parseAllowedOriginsText,
  parseSuggestedQuestionsText,
  type ChatbotChannelType,
  type ChatbotStatus,
} from '../types'

const STATUS_COLOR: Record<string, 'success' | 'warning' | 'default'> = {
  Active: 'success',
  Paused: 'warning',
  Draft: 'default',
}

export function ChatbotDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [description, setDescription] = useState('')
  const [welcomeMessage, setWelcomeMessage] = useState('')
  const [suggestedQuestions, setSuggestedQuestions] = useState('')
  const [promptTemplate, setPromptTemplate] = useState('')
  const [voiceEnabled, setVoiceEnabled] = useState(false)
  const [allowedOrigins, setAllowedOrigins] = useState('')
  const [embedOpen, setEmbedOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [newChannel, setNewChannel] = useState<ChatbotChannelType>('Teams')

  const query = useQuery({
    queryKey: ['chatbots', id],
    queryFn: () => getChatbot(id!),
    enabled: !!id,
  })
  const chatbot = query.data

  const channelsQuery = useQuery({
    queryKey: ['chatbot-channels', id],
    queryFn: () => listChatbotChannels(id!),
    enabled: !!id,
  })

  const webhookQuery = useQuery({
    queryKey: ['chatbot-webhook', id],
    queryFn: () => getWebhookKeyInfo(id!),
    enabled: !!id,
  })

  useEffect(() => {
    if (!chatbot) return
    setDescription(chatbot.description ?? '')
    setWelcomeMessage(chatbot.welcomeMessage ?? '')
    setSuggestedQuestions(parseSuggestedQuestionsText(chatbot.suggestedQuestions))
    setPromptTemplate(chatbot.promptTemplate ?? '')
    setVoiceEnabled(chatbot.voiceEnabled)
    setAllowedOrigins(parseAllowedOriginsText(chatbot.allowedOrigins))
  }, [chatbot])

  const saveMutation = useMutation({
    mutationFn: () =>
      updateChatbot(
        id!,
        buildFullChatbotUpdatePayload(chatbot!, {
          description,
          welcomeMessage,
          suggestedQuestions: encodeSuggestedQuestionsText(suggestedQuestions),
          promptTemplate,
          voiceEnabled,
          allowedOrigins: encodeAllowedOriginsText(allowedOrigins),
        }),
      ),
    onSuccess: () => {
      notify('Chatbot saved', 'success')
      void queryClient.invalidateQueries({ queryKey: ['chatbots', id] })
    },
    onError: () => notify('Failed to save chatbot', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => deleteChatbot(id!),
    onSuccess: () => {
      notify('Chatbot deleted', 'success')
      navigate('/chatbots')
    },
    onError: () => notify('Failed to delete chatbot', 'error'),
  })

  const addChannelMutation = useMutation({
    mutationFn: () => addChatbotChannel(id!, newChannel, '{}'),
    onSuccess: () => {
      notify(`${newChannel} channel configuration saved`, 'success')
      void queryClient.invalidateQueries({ queryKey: ['chatbot-channels', id] })
    },
    onError: () => notify('Failed to save channel configuration', 'error'),
  })

  const removeChannelMutation = useMutation({
    mutationFn: (channel: ChatbotChannelType) => removeChatbotChannel(id!, channel),
    onSuccess: () => {
      notify('Channel removed', 'success')
      void queryClient.invalidateQueries({ queryKey: ['chatbot-channels', id] })
    },
    onError: () => notify('Failed to remove channel', 'error'),
  })

  const statusMutation = useMutation({
    mutationFn: (status: ChatbotStatus) => setChatbotStatus(id!, status),
    onSuccess: (result) => {
      notify(`Chatbot ${result.status === 'Active' ? 'published' : result.status.toLowerCase()}`, 'success')
      void queryClient.invalidateQueries({ queryKey: ['chatbots', id] })
    },
    onError: () => notify('Failed to update status', 'error'),
  })

  const regenerateKeyMutation = useMutation({
    mutationFn: () => regenerateWebhookKey(id!),
    onSuccess: (result) => {
      notify(`New webhook key: ${result.key} (shown once — copy it now)`, 'success')
      void queryClient.invalidateQueries({ queryKey: ['chatbot-webhook', id] })
    },
    onError: () => notify('Failed to regenerate webhook key', 'error'),
  })

  if (query.isError) {
    return <ErrorState title="Unable to load chatbot" description="The chatbot details could not be retrieved." onRetry={() => void query.refetch()} />
  }

  if (query.isLoading || !chatbot) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    )
  }

  const connectedChannels = new Set((channelsQuery.data ?? []).map((c) => c.channel))
  const availableChannels = CHATBOT_CHANNEL_TYPES.filter((c) => !connectedChannels.has(c))

  return (
    <Box>
      <Stack direction="row" sx={{ alignItems: 'center', mb: 0.5 }}>
        <Button component={RouterLink} to="/chatbots" startIcon={<ArrowBackIcon />} size="small">
          Chatbots
        </Button>
      </Stack>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'center' }, justifyContent: 'space-between', mb: 2 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap', minWidth: 0 }}>
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            {chatbot.name}
          </Typography>
          <Chip label={chatbot.status} size="small" color={STATUS_COLOR[chatbot.status] ?? 'default'} variant="outlined" />
          {chatbot.publishedAssistantVersionNumber != null && (
            <Tooltip
              title={
                `Assistant version ${chatbot.publishedAssistantVersionNumber} was the linked assistant's published ` +
                `version when this chatbot was last published` +
                `${chatbot.publishedAt ? ` (${new Date(chatbot.publishedAt).toLocaleString()})` : ''}. ` +
                'This is a record only — the chatbot still serves the assistant’s current live configuration, ' +
                'not a version pinned to this snapshot.'
              }
            >
              <Chip
                icon={<InfoOutlined fontSize="small" />}
                label={`Assistant v${chatbot.publishedAssistantVersionNumber}`}
                size="small"
                variant="outlined"
              />
            </Tooltip>
          )}
        </Stack>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', justifyContent: { xs: 'flex-end', sm: 'flex-start' } }}>
          {chatbot.status !== 'Active' && (
            <Button
              variant="contained"
              disabled={statusMutation.isPending}
              onClick={() => statusMutation.mutate('Active')}
            >
              Publish
            </Button>
          )}
          {chatbot.status === 'Active' && (
            <Button disabled={statusMutation.isPending} onClick={() => statusMutation.mutate('Paused')}>
              Pause
            </Button>
          )}
          {chatbot.status === 'Paused' && (
            <Button disabled={statusMutation.isPending} onClick={() => statusMutation.mutate('Draft')}>
              Revert to draft
            </Button>
          )}
          <Button startIcon={<CodeIcon />} onClick={() => setEmbedOpen(true)}>
            Embed
          </Button>
          <Button color="error" onClick={() => setDeleteOpen(true)}>
            Delete
          </Button>
        </Stack>
      </Stack>

      <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" sx={{ mb: 1.5 }}>
          Configuration
        </Typography>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1}>
            {chatbot.assistantId && <Chip label="Assistant linked" size="small" variant="outlined" />}
            {chatbot.knowledgeBaseId && <Chip label="Knowledge base linked" size="small" variant="outlined" />}
            {!chatbot.assistantId && !chatbot.knowledgeBaseId && (
              <Typography variant="caption" color="text.secondary">
                No assistant or knowledge base linked
              </Typography>
            )}
          </Stack>
          <TextField label="Description" fullWidth multiline minRows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
          <TextField label="Welcome message" fullWidth value={welcomeMessage} onChange={(e) => setWelcomeMessage(e.target.value)} />
          <TextField
            label="Suggested questions"
            fullWidth
            multiline
            minRows={2}
            helperText="One per line"
            value={suggestedQuestions}
            onChange={(e) => setSuggestedQuestions(e.target.value)}
          />
          <TextField
            label="Prompt template"
            fullWidth
            multiline
            minRows={3}
            value={promptTemplate}
            onChange={(e) => setPromptTemplate(e.target.value)}
          />
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Switch checked={voiceEnabled} onChange={(e) => setVoiceEnabled(e.target.checked)} />
            <Typography variant="body2">Voice enabled</Typography>
          </Stack>
          <TextField
            label="Allowed embed origins"
            fullWidth
            multiline
            minRows={2}
            helperText="One exact origin per line, e.g. https://acme.example — leave empty to allow the widget on any site (default)."
            value={allowedOrigins}
            onChange={(e) => setAllowedOrigins(e.target.value)}
          />
          <Box>
            <Button variant="contained" disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? 'Saving…' : 'Save'}
            </Button>
          </Box>
        </Stack>
      </Paper>

      <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" sx={{ mb: 1.5 }}>
          Channels
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1, mb: 1.5 }}>
          {channelsQuery.isLoading && (
            <Typography variant="body2" color="text.secondary">Loading channels…</Typography>
          )}
          {channelsQuery.isError && (
            <Alert severity="error" action={<Button color="inherit" size="small" onClick={() => void channelsQuery.refetch()}>Retry</Button>}>
              Channel status is unavailable. Connect controls are hidden until it loads.
            </Alert>
          )}
          {channelsQuery.isSuccess && (channelsQuery.data ?? []).map((c) => (
            <Chip
              key={c.channel}
              label={c.channel}
              size="small"
              variant="outlined"
              onDelete={() => removeChannelMutation.mutate(c.channel)}
            />
          ))}
          {channelsQuery.isSuccess && (channelsQuery.data ?? []).length === 0 && (
            <Typography variant="body2" color="text.secondary">
              No channel configurations saved.
            </Typography>
          )}
        </Stack>
        {/* The website widget is configured on its own screen rather than inline here, because
            it needs an embed snippet, allowed origins and a live preview. It gets a link in this
            block because "where is this chatbot published" is the question this block answers —
            it is no longer its own nav entry, so without a link it would be unreachable from here. */}
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1, mb: 1.5 }}>
          <Button
            component={RouterLink}
            to={`/deploy/widget/${chatbot.id}`}
            size="small"
            variant="outlined"
            startIcon={<LanguageIcon />}
          >
            Website widget
          </Button>
          {connectedChannels.has('WhatsApp') && (
            <Button component={RouterLink} to={`/deploy/whatsapp/${chatbot.id}`} size="small" variant="outlined">
              WhatsApp settings
            </Button>
          )}
        </Stack>
        {channelsQuery.isSuccess && availableChannels.length > 0 && (
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel id="new-channel-label">Channel</InputLabel>
              <Select
                labelId="new-channel-label"
                label="Channel"
                value={newChannel}
                onChange={(e) => setNewChannel(e.target.value as ChatbotChannelType)}
              >
                {availableChannels.map((c) => (
                  <MenuItem key={c} value={c}>
                    {c}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button size="small" startIcon={<AddIcon />} onClick={() => addChannelMutation.mutate()} disabled={addChannelMutation.isPending}>
              Connect
            </Button>
          </Stack>
        )}
      </Paper>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="subtitle2">Webhook Key</Typography>
          <Tooltip title="POST { message } to the URL below with this key in an X-Webhook-Key header to get { reply } back — the same generic channel a custom integration or a provider-specific adapter (WhatsApp, Slack, Telegram) can send through.">
            <InfoOutlined fontSize="small" color="disabled" />
          </Tooltip>
        </Stack>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            {webhookQuery.isLoading
              ? 'Loading webhook key…'
              : webhookQuery.isError
                ? 'Webhook key status is unavailable.'
                : webhookQuery.data?.hasKey
                  ? `Key: ${webhookQuery.data.keyPrefix}…`
                  : 'No key generated yet'}
          </Typography>
          <TooltipIconButton size="small" aria-label="Regenerate webhook key" onClick={() => regenerateKeyMutation.mutate()} disabled={regenerateKeyMutation.isPending || !webhookQuery.isSuccess}>
            <RefreshIcon fontSize="small" />
          </TooltipIconButton>
        </Stack>
        {webhookQuery.isError && (
          <Alert severity="error" sx={{ mt: 1 }} action={<Button color="inherit" size="small" onClick={() => void webhookQuery.refetch()}>Retry</Button>}>
            The current key status could not be checked. Regeneration is disabled until it loads.
          </Alert>
        )}
        {webhookQuery.data?.webhookUrl && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5, fontFamily: 'monospace', wordBreak: 'break-all' }}>
            {webhookQuery.data.webhookUrl}
          </Typography>
        )}
      </Paper>

      <ChatbotEmbedDialog open={embedOpen} onClose={() => setEmbedOpen(false)} chatbotId={id!} />

      <ConfirmDeleteDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
        title="Delete Chatbot"
        message={`Delete "${chatbot.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
