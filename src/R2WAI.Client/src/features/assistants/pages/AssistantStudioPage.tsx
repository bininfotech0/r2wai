import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Avatar,
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  FormControlLabel,
  IconButton,
  List,
  ListItem,
  ListItemText,
  MenuItem,
  Paper,
  Skeleton,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import CloseIcon from '@mui/icons-material/Close'
import ChatOutlined from '@mui/icons-material/ChatOutlined'
import SettingsOutlined from '@mui/icons-material/SettingsOutlined'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import { Link as RouterLink } from 'react-router-dom'
import { LivePreviewPane } from '../components/LivePreviewPane'
import { AgentSettingsDrawer } from '../components/AgentSettingsDrawer'
import { AgentChannelsTab } from '../components/AgentChannelsTab'
import { DelegatesTab } from '../components/DelegatesTab'
import { parseEnabledIds } from '../toolsJson'
import { PublishChecklist } from '../../../components/PublishChecklist'
import { SectionCard } from '../../../components/SectionCard'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { fetchJson } from '../../../lib/api/fetchJson'
import {
  createAssistantVersion,
  deleteAssistant,
  getAssistant,
  getAssistantPublishReadiness,
  listAssistantVersions,
  publishAssistant,
  rollbackAssistantVersion,
  updateAssistant,
} from '../api'
import { buildFullUpdatePayload, GENERATABLE_TYPES, type AssistantDto, type AssistantType } from '../types'
import type { KnowledgeBaseDto } from '../../knowledge/types'
import { CapabilitiesTab } from '../../capabilities/components/CapabilitiesTab'
import { listModels } from '../../admin/api'
import { describePromptPlaceholders } from '../../../lib/prompts/promptPlaceholders'

const TABS = ['Overview', 'Knowledge', 'Tools', 'Channels', 'Versions', 'Analytics'] as const
type StudioTab = (typeof TABS)[number]

// Copilot Studio keeps "Test your agent" beside every tab; remember whether it was closed.
const TEST_PANE_KEY = 'r2wai.agent.testPane'
const TEST_PANE_WIDTH = 400

function readTestPaneOpen(): boolean {
  try {
    return window.localStorage.getItem(TEST_PANE_KEY) !== 'closed'
  } catch {
    return true
  }
}

const knowledgeBasesQuery = {
  queryKey: ['knowledgebases', 'all'] as const,
  queryFn: () => fetchJson<{ items: KnowledgeBaseDto[] }>('/knowledgebases?page=1&pageSize=100'),
}

export function AssistantStudioPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const theme = useTheme()
  const paneDocked = useMediaQuery(theme.breakpoints.up('lg'))
  const [tab, setTab] = useState<StudioTab>('Overview')
  const [publishOpen, setPublishOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [versionDialogOpen, setVersionDialogOpen] = useState(false)
  const [versionNote, setVersionNote] = useState('')
  const [versionPublish, setVersionPublish] = useState(true)
  // Docked (large screens): open unless the user closed it. Overlay (smaller): opened on demand.
  const [dockedTestOpen, setDockedTestOpen] = useState(readTestPaneOpen)
  const [overlayTestOpen, setOverlayTestOpen] = useState(false)
  const testOpen = paneDocked ? dockedTestOpen : overlayTestOpen

  const toggleTest = () => {
    if (!paneDocked) {
      setOverlayTestOpen((o) => !o)
      return
    }
    const next = !dockedTestOpen
    setDockedTestOpen(next)
    try {
      window.localStorage.setItem(TEST_PANE_KEY, next ? 'open' : 'closed')
    } catch {
      // Preference only; the toggle still works for this visit.
    }
  }

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

  // Overview fields, edited in place on the Details and Instructions cards.
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [systemPrompt, setSystemPrompt] = useState('')
  const [type, setType] = useState<AssistantType>('General')
  const [modelConfigurationId, setModelConfigurationId] = useState('')
  const [editingDetails, setEditingDetails] = useState(false)
  const [editingInstructions, setEditingInstructions] = useState(false)
  const [instructionsExpanded, setInstructionsExpanded] = useState(false)

  const resetOverview = (a: AssistantDto) => {
    setName(a.name)
    setDescription(a.description ?? '')
    setSystemPrompt(a.systemPrompt ?? '')
    setType(a.type)
    setModelConfigurationId(a.modelConfigurationId ?? '')
  }

  useEffect(() => {
    if (assistant) resetOverview(assistant)
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
  const kbQuery = useQuery({ ...knowledgeBasesQuery, enabled: tab === 'Overview' })
  const versionsQuery = useQuery({
    queryKey: ['assistant-versions', id],
    queryFn: () => listAssistantVersions(id!),
    enabled: !!id && tab === 'Versions',
  })

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
      setEditingDetails(false)
      setEditingInstructions(false)
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(id) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.publishReadiness(id) })
    },
    onError: () => notify('Failed to save', 'error'),
  })

  const publishMutation = useMutation({
    mutationFn: () => publishAssistant(id!),
    onSuccess: () => {
      notify('Agent published', 'success')
      setPublishOpen(false)
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(id) })
    },
    onError: () => notify('Failed to publish', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => deleteAssistant(id!),
    onSuccess: () => {
      notify('Agent deleted', 'success')
      navigate('/assistants')
    },
    onError: () => notify('Failed to delete agent', 'error'),
  })

  const createVersionMutation = useMutation({
    mutationFn: () => createAssistantVersion(id!, versionNote, versionPublish),
    onSuccess: () => {
      notify('Version created', 'success')
      setVersionDialogOpen(false)
      setVersionNote('')
      void queryClient.invalidateQueries({ queryKey: ['assistant-versions', id] })
    },
    onError: () => notify('Failed to create version', 'error'),
  })

  const rollbackVersionMutation = useMutation({
    mutationFn: (versionId: string) => rollbackAssistantVersion(id!, versionId),
    onSuccess: () => {
      notify('Rolled back — a new version was created', 'success')
      void queryClient.invalidateQueries({ queryKey: ['assistant-versions', id] })
    },
    onError: () => notify('Failed to roll back', 'error'),
  })

  if (query.isError) {
    return (
      <Box>
        <Button component={RouterLink} to="/assistants" startIcon={<ArrowBackIcon />} size="small">Agents</Button>
        <ErrorState title="Unable to load agent" description="The agent details could not be retrieved." onRetry={() => void query.refetch()} />
      </Box>
    )
  }

  if (query.isLoading || !assistant) {
    return (
      <Stack spacing={2} role="status" aria-busy="true">
        <Skeleton variant="text" width={240} height={40} />
        <Skeleton variant="rounded" height={40} />
        <Skeleton variant="rounded" height={160} />
        <Skeleton variant="rounded" height={220} />
      </Stack>
    )
  }

  const nextVersion = assistant.publishedVersion > 0 ? `${assistant.publishedVersion + 1}` : '1'
  const statusLabel = assistant.publishStatus === 'Published'
    ? `Published · v${assistant.publishedVersion}`
    : assistant.publishStatus
  const modelName = modelConfigurationId
    ? activeModels.find((m) => m.id === modelConfigurationId)?.name ?? 'Custom model'
    : 'Tenant default model'
  const linkedKb = kbQuery.data?.items.find((kb) => kb.id === assistant.knowledgeBaseId)
  const enabledToolCount = parseEnabledIds(assistant.tools).length
  const placeholderCheck = describePromptPlaceholders(systemPrompt)

  const testPane = (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Test your agent"
      sx={{ height: '100%', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}
    >
      <LivePreviewPane
        assistantId={assistant.id}
        title="Test your agent"
        subtitle="As users will see it"
        headerAction={(
          <Tooltip title="Close test pane">
            <IconButton size="small" aria-label="Close test pane" onClick={toggleTest}>
              <CloseIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      />
    </Paper>
  )

  return (
    <Box>
      {/* Header: Copilot Studio's agent bar. */}
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { md: 'center' }, mb: 2 }}
      >
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flex: 1, minWidth: 0 }}>
          <Tooltip title="Back to Agents">
            <IconButton component={RouterLink} to="/assistants" aria-label="Back to Agents" size="small">
              <ArrowBackIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          {assistant.avatarUrl ? (
            <Avatar src={assistant.avatarUrl} alt="" sx={{ width: 40, height: 40 }} />
          ) : (
            <Box sx={{ display: 'grid', placeItems: 'center', width: 40, height: 40, borderRadius: 2, flexShrink: 0, bgcolor: 'action.hover', color: 'primary.main' }}>
              <SmartToyOutlined />
            </Box>
          )}
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="h5" component="h1" noWrap title={assistant.name} sx={{ fontWeight: 700, lineHeight: 1.2 }}>
              {assistant.name}
            </Typography>
            <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', mt: 0.25 }}>
              <Box
                component="span"
                aria-hidden
                sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: assistant.publishStatus === 'Published' ? 'success.main' : 'text.secondary' }}
              />
              <Typography variant="caption" color="text.secondary">
                {statusLabel} · {assistant.type}
              </Typography>
            </Stack>
          </Box>
        </Stack>
        <Stack direction="row" spacing={1} sx={{ flexShrink: 0 }}>
          <Button
            variant={testOpen ? 'contained' : 'outlined'}
            color={testOpen ? 'secondary' : 'inherit'}
            startIcon={<ChatOutlined />}
            onClick={toggleTest}
            aria-pressed={testOpen}
          >
            Test
          </Button>
          <Button variant="outlined" color="inherit" startIcon={<SettingsOutlined />} onClick={() => setSettingsOpen(true)}>
            Settings
          </Button>
          <Tooltip title={hasUnsavedOverviewChanges ? 'Save your changes first, so the published version matches what you see' : ''}>
            <span>
              <Button variant="contained" disabled={hasUnsavedOverviewChanges} onClick={() => setPublishOpen(true)}>
                Publish
              </Button>
            </span>
          </Tooltip>
        </Stack>
      </Stack>

      <Box sx={{ display: 'flex', gap: 3, alignItems: 'flex-start' }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Tabs
            value={tab}
            onChange={(_, v: StudioTab) => setTab(v)}
            variant="scrollable"
            scrollButtons="auto"
            aria-label="Agent sections"
            sx={{ mb: 2.5, borderBottom: 1, borderColor: 'divider' }}
          >
            {TABS.map((t) => <Tab key={t} label={t} value={t} />)}
          </Tabs>

          {tab === 'Overview' && (
            <Stack spacing={2} sx={{ pb: hasUnsavedOverviewChanges ? 10 : 0 }}>
              <SectionCard
                title="Details"
                description="Name, purpose and the model this agent runs on."
                action={!editingDetails && <Button size="small" onClick={() => setEditingDetails(true)}>Edit</Button>}
              >
                {editingDetails ? (
                  <Stack spacing={2}>
                    <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth required error={!name.trim()} />
                    <TextField select label="Type" value={type} onChange={(e) => setType(e.target.value as AssistantType)} fullWidth>
                      {GENERATABLE_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
                    </TextField>
                    <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} fullWidth multiline minRows={2} />
                    <TextField
                      select
                      label="Model"
                      value={modelConfigurationId}
                      onChange={(e) => setModelConfigurationId(e.target.value)}
                      fullWidth
                      helperText="Leave on the tenant default to use whichever model is marked default."
                      slotProps={{ inputLabel: { shrink: true }, select: { displayEmpty: true } }}
                    >
                      <MenuItem value="">Use tenant default</MenuItem>
                      {activeModels.map((m) => (
                        <MenuItem key={m.id} value={m.id}>{m.name} ({m.provider}){m.isDefault ? ' · Default' : ''}</MenuItem>
                      ))}
                    </TextField>
                  </Stack>
                ) : (
                  <Box component="dl" sx={{ m: 0, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '140px 1fr' }, rowGap: 1, columnGap: 2 }}>
                    <Typography component="dt" variant="body2" color="text.secondary">Name</Typography>
                    <Typography component="dd" variant="body2" sx={{ m: 0 }}>{name}</Typography>
                    <Typography component="dt" variant="body2" color="text.secondary">Type</Typography>
                    <Typography component="dd" variant="body2" sx={{ m: 0 }}>{type}</Typography>
                    <Typography component="dt" variant="body2" color="text.secondary">Description</Typography>
                    <Typography component="dd" variant="body2" sx={{ m: 0, color: description ? 'text.primary' : 'text.secondary' }}>
                      {description || 'No description yet'}
                    </Typography>
                    <Typography component="dt" variant="body2" color="text.secondary">Model</Typography>
                    <Typography component="dd" variant="body2" sx={{ m: 0 }}>{modelName}</Typography>
                  </Box>
                )}
              </SectionCard>

              <SectionCard
                title="Instructions"
                description="How the agent should behave, what it should and shouldn't do."
                action={!editingInstructions && <Button size="small" onClick={() => setEditingInstructions(true)}>Edit</Button>}
              >
                {editingInstructions ? (
                  <TextField
                    label="Instructions"
                    value={systemPrompt}
                    onChange={(e) => setSystemPrompt(e.target.value)}
                    fullWidth
                    multiline
                    minRows={8}
                    autoFocus
                    error={placeholderCheck.isWarning}
                    helperText={`${systemPrompt.length.toLocaleString()} characters. ${placeholderCheck.message}`}
                  />
                ) : systemPrompt ? (
                  <Box>
                    <Typography
                      variant="body2"
                      sx={{
                        whiteSpace: 'pre-wrap',
                        fontFamily: 'ui-monospace, SFMono-Regular, Consolas, monospace',
                        fontSize: 13,
                        ...(instructionsExpanded ? {} : { display: '-webkit-box', WebkitLineClamp: 6, WebkitBoxOrient: 'vertical', overflow: 'hidden' }),
                      }}
                    >
                      {systemPrompt}
                    </Typography>
                    {systemPrompt.split('\n').length > 6 || systemPrompt.length > 480 ? (
                      <Button size="small" sx={{ mt: 1, px: 0 }} onClick={() => setInstructionsExpanded((v) => !v)}>
                        {instructionsExpanded ? 'Show less' : 'Show more'}
                      </Button>
                    ) : null}
                  </Box>
                ) : (
                  <Typography variant="body2" color="text.secondary">
                    No instructions yet. Tell the agent what it is for and how to answer.
                  </Typography>
                )}
              </SectionCard>

              <SectionCard
                title="Knowledge"
                description="Where the agent finds answers. It cites these sources."
                action={<Button size="small" onClick={() => setTab('Knowledge')}>{assistant.knowledgeBaseId ? 'Change' : '+ Add knowledge'}</Button>}
              >
                {assistant.knowledgeBaseId ? (
                  <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>{linkedKb?.name ?? 'Linked knowledge base'}</Typography>
                      {linkedKb && <Typography variant="caption" color="text.secondary">{linkedKb.documentCount} documents</Typography>}
                    </Box>
                    {linkedKb && <Chip label={linkedKb.status} size="small" variant="outlined" />}
                  </Stack>
                ) : (
                  <Typography variant="body2" color="text.secondary">
                    No knowledge yet. Add a knowledge base so the agent answers from your documents instead of general knowledge.
                  </Typography>
                )}
              </SectionCard>

              <SectionCard
                title="Tools"
                description="Actions the agent can take in your connected systems. Risky ones ask for confirmation or approval."
                action={<Button size="small" onClick={() => setTab('Tools')}>{enabledToolCount > 0 ? 'Manage' : '+ Add tool'}</Button>}
              >
                <Typography variant="body2" color={enabledToolCount > 0 ? 'text.primary' : 'text.secondary'}>
                  {enabledToolCount > 0
                    ? `${enabledToolCount} tool${enabledToolCount === 1 ? '' : 's'} enabled.`
                    : 'No tools yet. Without tools the agent can only answer questions.'}
                </Typography>
              </SectionCard>
            </Stack>
          )}

          {tab === 'Knowledge' && (
            <SectionCard title="Knowledge" description="An agent uses one knowledge base at a time.">
              <KnowledgeTab assistant={assistant} />
            </SectionCard>
          )}

          {tab === 'Tools' && (
            <Stack spacing={3}>
              <CapabilitiesTab assistantId={assistant.id} toolsJson={assistant.tools} />
              <SectionCard
                title="Delegates to other agents"
                description="Real multi-agent delegation — this agent can hand part of a request to another specialist agent instead of answering it directly."
              >
                <DelegatesTab assistantId={assistant.id} delegatableAssistantIds={assistant.delegatableAssistantIds} />
              </SectionCard>
            </Stack>
          )}

          {tab === 'Channels' && <AgentChannelsTab assistant={assistant} />}

          {tab === 'Versions' && (
            <Paper variant="outlined" sx={{ p: 2, maxWidth: 700 }}>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' }, mb: 1.5 }}>
                <Typography variant="caption" color="text.secondary">
                  Snapshots of this agent's configuration, newest first.
                </Typography>
                <Button size="small" onClick={() => setVersionDialogOpen(true)}>
                  Create Version
                </Button>
              </Stack>
              {versionsQuery.isLoading ? (
                <LoadingSkeleton variant="text" count={3} height={48} />
              ) : versionsQuery.isError ? (
                <ErrorState title="Unable to load agent versions" onRetry={() => void versionsQuery.refetch()} />
              ) : (
                <List dense>
                  {(versionsQuery.data ?? [])
                    .slice()
                    .sort((a, b) => b.versionNumber - a.versionNumber)
                    .map((v) => (
                      <ListItem
                        key={v.id}
                        divider
                        secondaryAction={
                          !v.isPublished && (
                            <Button size="small" onClick={() => rollbackVersionMutation.mutate(v.id)} disabled={rollbackVersionMutation.isPending} loading={rollbackVersionMutation.isPending}>
                              Roll back
                            </Button>
                          )
                        }
                      >
                        <ListItemText
                          primary={
                            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                              <span>Version {v.versionNumber}</span>
                              {v.isPublished && <Chip label="Published" size="small" color="default" variant="outlined" />}
                            </Stack>
                          }
                          secondary={`${v.note || 'No note'} · ${new Date(v.createdAt).toLocaleString()}`}
                        />
                      </ListItem>
                    ))}
                </List>
              )}
              {!versionsQuery.isLoading && !versionsQuery.isError && (versionsQuery.data ?? []).length === 0 && (
                <EmptyState title="No versions yet" />
              )}

              <Dialog open={versionDialogOpen} onClose={() => setVersionDialogOpen(false)} maxWidth="sm" fullWidth aria-label="Create Version">
                <DialogTitle>Create Version</DialogTitle>
                <DialogContent>
                  <Stack spacing={2} sx={{ pt: 1 }}>
                    <TextField
                      label="Note"
                      value={versionNote}
                      onChange={(e) => setVersionNote(e.target.value)}
                      fullWidth
                      multiline
                      minRows={2}
                    />
                    <FormControlLabel
                      control={<Checkbox checked={versionPublish} onChange={(e) => setVersionPublish(e.target.checked)} />}
                      label="Publish immediately"
                    />
                  </Stack>
                </DialogContent>
                <DialogActions sx={{ px: 3, pb: 2 }}>
                  <Button onClick={() => setVersionDialogOpen(false)}>Cancel</Button>
                  <Button
                    variant="contained"
                    onClick={() => createVersionMutation.mutate()}
                    disabled={createVersionMutation.isPending}
                   loading={createVersionMutation.isPending}>
                    Create
                  </Button>
                </DialogActions>
              </Dialog>
            </Paper>
          )}

          {tab === 'Analytics' && (
            <SectionCard title="Analytics" description="Usage of this agent across every channel.">
              <Box component="dl" sx={{ m: 0, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '180px 1fr' }, rowGap: 1, columnGap: 2 }}>
                <Typography component="dt" variant="body2" color="text.secondary">Conversations</Typography>
                <Typography component="dd" variant="body2" sx={{ m: 0 }}>{assistant.usageCount.toLocaleString()}</Typography>
                <Typography component="dt" variant="body2" color="text.secondary">Status</Typography>
                <Typography component="dd" variant="body2" sx={{ m: 0 }}>{statusLabel}</Typography>
                <Typography component="dt" variant="body2" color="text.secondary">Created</Typography>
                <Typography component="dd" variant="body2" sx={{ m: 0 }}>{new Date(assistant.createdAt).toLocaleString()}</Typography>
              </Box>
              <Button component={RouterLink} to="/runs" size="small" sx={{ mt: 2, px: 0 }}>
                See every run in Activity
              </Button>
            </SectionCard>
          )}
        </Box>

        {paneDocked && testOpen && (
          <Box
            sx={{
              width: TEST_PANE_WIDTH,
              flexShrink: 0,
              position: 'sticky',
              top: 72,
              height: 'calc(100dvh - 96px)',
            }}
          >
            {testPane}
          </Box>
        )}
      </Box>

      {!paneDocked && (
        <Drawer
          anchor="right"
          open={overlayTestOpen}
          onClose={() => setOverlayTestOpen(false)}
          slotProps={{ paper: { sx: { width: { xs: '100%', sm: 420 }, p: 1 } } }}
        >
          {testPane}
        </Drawer>
      )}

      {hasUnsavedOverviewChanges && tab === 'Overview' && (
        <Paper
          elevation={6}
          role="region"
          aria-label="Unsaved changes"
          sx={{
            position: 'sticky',
            bottom: 16,
            mt: 2,
            px: 2.5,
            py: 1.5,
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            zIndex: 2,
          }}
        >
          <Typography variant="body2" sx={{ flex: 1, fontWeight: 600 }}>You have unsaved changes</Typography>
          <Button
            color="inherit"
            onClick={() => {
              resetOverview(assistant)
              setEditingDetails(false)
              setEditingInstructions(false)
            }}
          >
            Discard
          </Button>
          <Button variant="contained" disabled={saveOverview.isPending || !name.trim()} onClick={() => saveOverview.mutate()} loading={saveOverview.isPending}>
            {saveOverview.isPending ? 'Saving…' : 'Save'}
          </Button>
        </Paper>
      )}

      {settingsOpen && (
        <AgentSettingsDrawer
          open={settingsOpen}
          onClose={() => setSettingsOpen(false)}
          assistant={assistant}
          onDelete={() => {
            setSettingsOpen(false)
            setDeleteOpen(true)
          }}
        />
      )}

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
        title="Delete agent"
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
  const kbQuery = useQuery(knowledgeBasesQuery)

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
            slotProps={{ input: { 'aria-label': `Use ${kb.name}` } }}
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
