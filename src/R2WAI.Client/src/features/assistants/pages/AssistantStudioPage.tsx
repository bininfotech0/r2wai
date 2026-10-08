import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Grid,
  MenuItem,
  Paper,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import { Link as RouterLink } from 'react-router-dom'
import { LivePreviewPane } from '../components/LivePreviewPane'
import { AssistantApiAccessSection } from '../components/AssistantApiAccessSection'
import { PublishChecklist } from '../../../components/PublishChecklist'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { fetchJson } from '../../../lib/api/fetchJson'
import { deleteAssistant, getAssistant, getAssistantPublishReadiness, publishAssistant, updateAssistant } from '../api'
import { buildFullUpdatePayload, GENERATABLE_TYPES, type AssistantDto, type AssistantType, type BehaviorSettings } from '../types'
import type { KnowledgeBaseDto } from '../../knowledge/types'
import { CapabilitiesTab } from '../../capabilities/components/CapabilitiesTab'
import { createChatbot, listChatbots } from '../../chatbots/api'
import { listModels } from '../../admin/api'
import { describePromptPlaceholders } from '../../../lib/prompts/promptPlaceholders'

const TABS = ['Overview', 'Knowledge', 'Capabilities', 'Behavior', 'Security', 'Channels', 'Analytics'] as const

export function AssistantStudioPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [tab, setTab] = useState<(typeof TABS)[number]>('Overview')
  const [publishOpen, setPublishOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  const query = useQuery({
    queryKey: queryKeys.assistants.detail(id),
    queryFn: () => getAssistant(id!),
    enabled: !!id,
  })

  const assistant = query.data
  const readinessQuery = useQuery({
    queryKey: queryKeys.assistants.publishReadiness(id),
    queryFn: () => getAssistantPublishReadiness(id!),
    enabled: !!id && publishOpen,
  })

  // Overview fields
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [systemPrompt, setSystemPrompt] = useState('')
  const [type, setType] = useState<AssistantType>('General')
  const [modelConfigurationId, setModelConfigurationId] = useState('')

  useEffect(() => {
    if (!assistant) return
    setName(assistant.name)
    setDescription(assistant.description ?? '')
    setSystemPrompt(assistant.systemPrompt ?? '')
    setType(assistant.type)
    setModelConfigurationId(assistant.modelConfigurationId ?? '')
  }, [assistant])

  const hasUnsavedOverviewChanges = !!assistant && (
    name !== assistant.name ||
    description !== (assistant.description ?? '') ||
    systemPrompt !== (assistant.systemPrompt ?? '') ||
    type !== assistant.type ||
    modelConfigurationId !== (assistant.modelConfigurationId ?? '')
  )

  const modelsQuery = useQuery({ queryKey: ['admin', 'models'], queryFn: listModels, enabled: tab === 'Overview' })
  const activeModels = (modelsQuery.data?.items ?? []).filter((m) => m.isActive)

  const saveOverview = useMutation({
    mutationFn: () =>
      updateAssistant(
        id!,
        buildFullUpdatePayload(assistant!, {
          name,
          description,
          systemPrompt,
          type,
          ...(modelConfigurationId
            ? { modelConfigurationId }
            : { unlinkModelConfiguration: true }),
        }),
      ),
    onSuccess: () => {
      notify('Saved', 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(id) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.publishReadiness(id) })
    },
    onError: () => notify('Failed to save', 'error'),
  })

  const publishMutation = useMutation({
    mutationFn: () => publishAssistant(id!),
    onSuccess: () => {
      notify('Assistant published', 'success')
      setPublishOpen(false)
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(id) })
    },
    onError: () => notify('Failed to publish', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => deleteAssistant(id!),
    onSuccess: () => {
      notify('Assistant deleted', 'success')
      navigate('/assistants')
    },
    onError: () => notify('Failed to delete assistant', 'error'),
  })

  const channelsQuery = useQuery({
    queryKey: queryKeys.assistants.chatbots(id),
    queryFn: () => listChatbots(1, 20, id),
    enabled: !!id && tab === 'Channels',
  })

  const createChannelMutation = useMutation({
    mutationFn: () => createChatbot({ name: `${assistant!.name} Widget`, assistantId: id!, voiceEnabled: false }),
    onSuccess: (chatbot) => {
      notify('Embeddable chatbot created', 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.chatbots(id) })
      navigate(`/chatbots/${chatbot.id}`)
    },
    onError: () => notify('Failed to create chatbot', 'error'),
  })

  if (query.isError) {
    return <Box><Button component={RouterLink} to="/assistants" startIcon={<ArrowBackIcon />} size="small">AI Assistants</Button><ErrorState title="Unable to load assistant" description="The assistant details could not be retrieved." onRetry={() => void query.refetch()} /></Box>
  }

  if (query.isLoading || !assistant) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    )
  }

  const nextVersion = assistant.publishedVersion > 0 ? `${assistant.publishedVersion + 1}` : '1'

  return (
    <Box>
      <Stack direction="row" sx={{ alignItems: 'center', mb: 2, gap: 1 }}>
        <Button component={RouterLink} to="/assistants" startIcon={<ArrowBackIcon />} size="small">
          AI Assistants
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        <Button color="error" onClick={() => setDeleteOpen(true)}>
          Delete
        </Button>
        <Button variant="contained" disabled={hasUnsavedOverviewChanges} onClick={() => setPublishOpen(true)}>
          Publish
        </Button>
      </Stack>
      {hasUnsavedOverviewChanges && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Save your Overview changes before publishing so the published version matches what you see here.
        </Alert>
      )}

      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {assistant.name}
        </Typography>
        <Chip
          label={assistant.publishStatus}
          size="small"
          color={assistant.publishStatus === 'Published' ? 'success' : 'default'}
        />
      </Stack>

      <Tabs value={tab} onChange={(_, v: (typeof TABS)[number]) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        {TABS.map((t) => (
          <Tab key={t} label={t} value={t} />
        ))}
      </Tabs>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper variant="outlined" sx={{ p: 3, minHeight: 480 }}>
            {tab === 'Overview' && (
              <Stack spacing={2}>
                <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth />
                <TextField
                  select
                  label="Type"
                  value={type}
                  onChange={(e) => setType(e.target.value as AssistantType)}
                  fullWidth
                >
                  {GENERATABLE_TYPES.map((t) => (
                    <MenuItem key={t} value={t}>
                      {t}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  label="Description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  fullWidth
                  multiline
                  minRows={2}
                />
                <TextField
                  select
                  label="Base Model"
                  value={modelConfigurationId}
                  onChange={(e) => setModelConfigurationId(e.target.value)}
                  fullWidth
                  helperText="Leave on tenant default to use whichever model is marked default."
                  slotProps={{ inputLabel: { shrink: true }, select: { displayEmpty: true } }}
                >
                  <MenuItem value="">Use tenant default</MenuItem>
                  {activeModels.map((m) => (
                    <MenuItem key={m.id} value={m.id}>
                      {m.name} ({m.provider}){m.isDefault ? ' · Default' : ''}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  label="Instructions"
                  value={systemPrompt}
                  onChange={(e) => setSystemPrompt(e.target.value)}
                  fullWidth
                  multiline
                  minRows={6}
                  error={describePromptPlaceholders(systemPrompt).isWarning}
                  helperText={`The system prompt that defines this assistant's behavior. ${describePromptPlaceholders(systemPrompt).message}`}
                />
                <Box>
                  <Button
                    variant="contained"
                    disabled={saveOverview.isPending || !name.trim()}
                    onClick={() => saveOverview.mutate()}
                  >
                    {saveOverview.isPending ? 'Saving…' : 'Save changes'}
                  </Button>
                </Box>
              </Stack>
            )}

            {tab === 'Knowledge' && <KnowledgeTab assistant={assistant} />}

            {tab === 'Capabilities' && <CapabilitiesTab assistantId={assistant.id} toolsJson={assistant.tools} />}

            {tab === 'Behavior' && <BehaviorTab assistant={assistant} />}

            {tab === 'Security' && (
              <Stack spacing={2}>
                <Typography variant="body2" color="text.secondary">
                  Assistant-level security policy doesn't exist separately from the platform's
                  tenant-wide policies — this assistant is governed by the same Tool Gateway
                  authorization and audit rules as every other assistant.
                </Typography>
                <Typography variant="body2">
                  Enabled tools for this assistant are shown on the Capabilities tab (under
                  Advanced); each call still passes through server-side policy/authorization
                  checks regardless of what's configured here.
                </Typography>
                <Button component={RouterLink} to="/security" variant="outlined" size="small" sx={{ alignSelf: 'flex-start' }}>
                  Open Security & Policies
                </Button>
              </Stack>
            )}

            {tab === 'Channels' && (
              <Stack spacing={2}>
                <Typography variant="body2" color="text.secondary">
                  Publish this assistant to an external channel by creating an embeddable chatbot
                  backed by it — the chatbot's own detail page handles the widget embed code, an
                  optional origin allowlist, and webhook access for other systems to call in.
                </Typography>
                {assistant.publishStatus !== 'Published' && (
                  <Alert severity="warning" variant="outlined">
                    This assistant isn't published yet — a linked chatbot won't be able to serve chat
                    requests until you publish it.
                  </Alert>
                )}
                {(channelsQuery.data?.items ?? []).map((cb) => (
                  <Paper key={cb.id} variant="outlined" sx={{ p: 2 }}>
                    <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {cb.name}
                        </Typography>
                        <Chip
                          label={cb.status}
                          size="small"
                          color={cb.status === 'Active' ? 'success' : cb.status === 'Paused' ? 'warning' : 'default'}
                        />
                      </Stack>
                      <Button component={RouterLink} to={`/chatbots/${cb.id}`} size="small">
                        Manage
                      </Button>
                    </Stack>
                  </Paper>
                ))}
                {channelsQuery.data?.items.length === 0 && (
                  <Button
                    variant="contained"
                    sx={{ alignSelf: 'flex-start' }}
                    disabled={createChannelMutation.isPending}
                    onClick={() => createChannelMutation.mutate()}
                  >
                    {createChannelMutation.isPending ? 'Creating…' : 'Create embeddable chatbot'}
                  </Button>
                )}

                <AssistantApiAccessSection assistant={assistant} />
              </Stack>
            )}

            {tab === 'Analytics' && (
              <Stack spacing={1.5}>
                <Typography variant="body2">Used {assistant.usageCount} times</Typography>
                <Typography variant="body2">Status: {assistant.publishStatus}</Typography>
                {assistant.publishStatus === 'Published' && (
                  <Typography variant="body2">Published version: {assistant.publishedVersion}</Typography>
                )}
                <Typography variant="body2" color="text.secondary">
                  Created {new Date(assistant.createdAt).toLocaleString()}
                </Typography>
              </Stack>
            )}
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Paper variant="outlined" sx={{ height: 480, display: 'flex', flexDirection: 'column' }}>
            <LivePreviewPane assistantId={assistant.id} />
          </Paper>
        </Grid>
      </Grid>

      <PublishChecklist
        open={publishOpen}
        onClose={() => setPublishOpen(false)}
        onPublish={() => publishMutation.mutate()}
        title={assistant.name}
        version={nextVersion}
        isPublishing={publishMutation.isPending}
        checks={readinessQuery.data?.checks ?? []}
        checksLoading={readinessQuery.isLoading}
        checksError={readinessQuery.isError}
        onRetryChecks={() => void readinessQuery.refetch()}
      />

      <ConfirmDeleteDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
        title="Delete Assistant"
        message={`Delete "${assistant.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}

function KnowledgeTab({ assistant }: { assistant: AssistantDto }) {
  const navigate = useNavigate()
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const kbQuery = useQuery({
    queryKey: ['knowledgebases', 'all'],
    queryFn: () => fetchJson<{ items: KnowledgeBaseDto[] }>('/knowledgebases?page=1&pageSize=100'),
  })

  const mutation = useMutation({
    mutationFn: (kbId: string | null) =>
      updateAssistant(
        assistant.id,
        kbId === null
          ? buildFullUpdatePayload(assistant, { unlinkKnowledgeBase: true })
          : buildFullUpdatePayload(assistant, { knowledgeBaseId: kbId }),
      ),
    onSuccess: () => {
      notify('Knowledge source updated', 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(assistant.id) })
    },
    onError: () => notify('Failed to update knowledge source', 'error'),
  })

  return (
    <Stack spacing={1.5}>
      <Typography variant="body2" color="text.secondary">
        Knowledge available to this assistant. An assistant connects to one knowledge base at a
        time today.
      </Typography>
      {kbQuery.data?.items.length === 0 && (
        <EmptyState
          title="No knowledge bases yet"
          actionLabel="Go to Knowledge"
          onAction={() => navigate('/knowledge')}
        />
      )}
      {kbQuery.data?.items.map((kb) => (
        <Stack
          key={kb.id}
          direction="row"
          spacing={1}
          sx={{
            alignItems: 'center',
            p: 1,
            border: '1px solid',
            borderColor: assistant.knowledgeBaseId === kb.id ? 'primary.main' : 'divider',
            borderRadius: 1,
            cursor: 'pointer',
          }}
          onClick={() => mutation.mutate(assistant.knowledgeBaseId === kb.id ? null : kb.id)}
        >
          <Switch
            checked={assistant.knowledgeBaseId === kb.id}
            size="small"
            disabled={mutation.isPending}
            title={assistant.knowledgeBaseId === kb.id ? 'Click to remove this knowledge base' : undefined}
          />
          <Box sx={{ flexGrow: 1 }}>
            <Typography variant="body2">{kb.name}</Typography>
            <Typography variant="caption" color="text.secondary">
              {kb.documentCount} documents
            </Typography>
          </Box>
          <Chip label={kb.status} size="small" variant="outlined" />
        </Stack>
      ))}
    </Stack>
  )
}

const RESPONSE_STYLES: NonNullable<BehaviorSettings['responseStyle']>[] = [
  'Professional',
  'Friendly',
  'Concise',
  'Detailed',
]
const ANSWER_LENGTHS: NonNullable<BehaviorSettings['answerLength']>[] = ['Brief', 'Balanced', 'Thorough']

function BehaviorTab({ assistant }: { assistant: AssistantDto }) {
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const [settings, setSettings] = useState<BehaviorSettings>({})
  const [showAdvanced, setShowAdvanced] = useState(false)

  useEffect(() => {
    if (!assistant.settings) return
    try {
      setSettings(JSON.parse(assistant.settings) as BehaviorSettings)
    } catch {
      // malformed/legacy settings — start fresh rather than crash the tab
    }
  }, [assistant.settings])

  const mutation = useMutation({
    mutationFn: () =>
      updateAssistant(assistant.id, buildFullUpdatePayload(assistant, { settings: JSON.stringify(settings) })),
    onSuccess: () => {
      notify('Behavior settings saved', 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(assistant.id) })
    },
    onError: () => notify('Failed to save settings', 'error'),
  })

  return (
    <Stack spacing={2}>
      <TextField
        select
        label="Response style"
        value={settings.responseStyle ?? 'Professional'}
        onChange={(e) => setSettings((s) => ({ ...s, responseStyle: e.target.value as BehaviorSettings['responseStyle'] }))}
      >
        {RESPONSE_STYLES.map((v) => (
          <MenuItem key={v} value={v}>
            {v}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        label="Answer length"
        value={settings.answerLength ?? 'Balanced'}
        onChange={(e) => setSettings((s) => ({ ...s, answerLength: e.target.value as BehaviorSettings['answerLength'] }))}
      >
        {ANSWER_LENGTHS.map((v) => (
          <MenuItem key={v} value={v}>
            {v}
          </MenuItem>
        ))}
      </TextField>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Switch
          checked={settings.citationsEnabled ?? true}
          onChange={(e) => setSettings((s) => ({ ...s, citationsEnabled: e.target.checked }))}
        />
        <Typography variant="body2">Citations enabled</Typography>
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Switch
          checked={settings.askClarification ?? true}
          onChange={(e) => setSettings((s) => ({ ...s, askClarification: e.target.checked }))}
        />
        <Typography variant="body2">Ask clarifying questions</Typography>
      </Stack>

      {assistant.knowledgeBaseId && (
        <Box>
          <TextField
            select
            fullWidth
            label="Knowledge retrieval"
            value={settings.retrievalMode ?? 'Standard'}
            onChange={(e) => setSettings((s) => ({ ...s, retrievalMode: e.target.value as BehaviorSettings['retrievalMode'] }))}
          >
            <MenuItem value="Standard">Standard — one search, fastest</MenuItem>
            <MenuItem value="Agentic">Agentic — retries with a rewritten query if the first search is weak</MenuItem>
          </TextField>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
            {settings.retrievalMode === 'Agentic'
              ? "Adds up to one extra search (a rewritten query) when the first pass doesn't find a confident match, and tells the assistant to say so rather than guess if it still can't find one. Slightly slower on questions that need the retry."
              : 'A single knowledge base search per message. Switch to Agentic for questions that often need a differently-worded search to find the right answer.'}
          </Typography>
        </Box>
      )}

      <Button size="small" onClick={() => setShowAdvanced((v) => !v)} sx={{ alignSelf: 'flex-start' }}>
        {showAdvanced ? 'Hide advanced' : 'Advanced settings ▸'}
      </Button>
      {showAdvanced && (
        <Stack spacing={2}>
          <TextField
            label="Temperature"
            type="number"
            slotProps={{ htmlInput: { step: 0.1, min: 0, max: 2 } }}
            value={settings.temperature ?? 0.7}
            onChange={(e) => setSettings((s) => ({ ...s, temperature: Number(e.target.value) }))}
          />
          <TextField
            label="Maximum output tokens"
            type="number"
            value={settings.maxOutputTokens ?? 2048}
            onChange={(e) => setSettings((s) => ({ ...s, maxOutputTokens: Number(e.target.value) }))}
          />
        </Stack>
      )}

      <Box>
        <Button variant="contained" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Saving…' : 'Save changes'}
        </Button>
      </Box>
    </Stack>
  )
}
