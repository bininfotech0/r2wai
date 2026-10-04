import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Checkbox,
  Chip,
  FormControlLabel,
  List,
  ListItem,
  ListItemText,
  Menu,
  MenuItem,
  Paper,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import EditIcon from '@mui/icons-material/Edit'
import RestoreIcon from '@mui/icons-material/Restore'
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown'
import HubOutlinedIcon from '@mui/icons-material/HubOutlined'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { ErrorState } from '../../../components/ErrorState'
import { EmptyState } from '../../../components/EmptyState'
import { describeApiError } from '../../../lib/api/fetchJson'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import { listAssistants } from '../../assistants/api'
import { listKnowledgeBases } from '../../knowledge/api'
import { listWorkflows } from '../../automations/api'
import { listCapabilities } from '../../tools/api'
import {
  changeApplicationStatus,
  createApplicationApi,
  createApplicationVersion,
  deleteApplication,
  deleteApplicationApi,
  getApplication,
  getApplicationConfiguration,
  listApplicationApis,
  listApplicationVersions,
  rollbackApplicationVersion,
  updateApplication,
  updateApplicationApi,
  updateApplicationConfiguration,
} from '../api'
import { CreateEditApplicationApiDialog } from '../dialogs/CreateEditApplicationApiDialog'
import { DiscoverApplicationDialog } from '../dialogs/DiscoverApplicationDialog'
import { APPLICATION_ACTIONS, APPLICATION_ENVIRONMENTS, type ApplicationApiDto, type ApplicationEnvironment } from '../types'

const STATUS_COLOR: Record<string, 'success' | 'warning' | 'default' | 'error'> = {
  Published: 'success',
  Testing: 'warning',
  Configuring: 'warning',
  Discovering: 'warning',
  Draft: 'default',
  Disabled: 'error',
  Archived: 'default',
}

const TABS = ['General', 'APIs', 'Configuration', 'Versions', 'Ecosystem'] as const
type Tab = (typeof TABS)[number]

export function ApplicationWorkspacePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [tab, setTab] = useState<Tab>('General')
  const [statusMenuAnchor, setStatusMenuAnchor] = useState<HTMLElement | null>(null)
  const [discoverOpen, setDiscoverOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [apiDialogOpen, setApiDialogOpen] = useState(false)
  const [editingApi, setEditingApi] = useState<ApplicationApiDto | null>(null)
  const [deletingApi, setDeletingApi] = useState<ApplicationApiDto | null>(null)
  const [versionNote, setVersionNote] = useState('')
  const [versionPublish, setVersionPublish] = useState(false)

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [baseUrl, setBaseUrl] = useState('')
  const [environment, setEnvironment] = useState<ApplicationEnvironment>('Development')

  const [timeoutSeconds, setTimeoutSeconds] = useState(30)
  const [maxRetries, setMaxRetries] = useState(3)
  const [ragThreshold, setRagThreshold] = useState(0.7)
  const [modelId, setModelId] = useState('')
  const [systemPromptTemplate, setSystemPromptTemplate] = useState('')

  const query = useQuery({
    queryKey: ['applications', id],
    queryFn: () => getApplication(id!),
    enabled: !!id,
  })
  const app = query.data

  useEffect(() => {
    if (!app) return
    setName(app.name)
    setDescription(app.description ?? '')
    setBaseUrl(app.baseUrl ?? '')
    setEnvironment(app.environment)
  }, [app])

  const apisQuery = useQuery({
    queryKey: ['application-apis', id],
    queryFn: () => listApplicationApis(id!),
    enabled: !!id && tab === 'APIs',
  })

  const configQuery = useQuery({
    queryKey: ['application-configuration', id],
    queryFn: () => getApplicationConfiguration(id!),
    enabled: !!id && tab === 'Configuration',
  })
  useEffect(() => {
    if (!configQuery.data) return
    setTimeoutSeconds(configQuery.data.timeoutSeconds)
    setMaxRetries(configQuery.data.maxRetries)
    setRagThreshold(configQuery.data.ragThreshold)
    setModelId(configQuery.data.modelId ?? '')
    setSystemPromptTemplate(configQuery.data.systemPromptTemplate ?? '')
  }, [configQuery.data])

  const versionsQuery = useQuery({
    queryKey: ['application-versions', id],
    queryFn: () => listApplicationVersions(id!),
    enabled: !!id && tab === 'Versions',
  })

  const assistantsQuery = useQuery({
    queryKey: queryKeys.assistants.forResources,
    queryFn: () => listAssistants(1, 100, ''),
    enabled: !!id && tab === 'Ecosystem',
  })
  const workflowsQuery = useQuery({
    queryKey: ['workflows-for-resources'],
    queryFn: () => listWorkflows(1, 100, ''),
    enabled: !!id && tab === 'Ecosystem',
  })
  const knowledgeBasesQuery = useQuery({
    queryKey: ['knowledgebases-for-resources', id],
    queryFn: () => listKnowledgeBases(1, 100, '', id),
    enabled: !!id && tab === 'Ecosystem',
  })
  const capabilitiesQuery = useQuery({
    queryKey: ['capabilities-for-resources', id],
    queryFn: () => listCapabilities(1, 100, '', id),
    enabled: !!id && tab === 'Ecosystem',
  })

  const invalidateApp = () => queryClient.invalidateQueries({ queryKey: ['applications', id] })

  const saveMutation = useMutation({
    mutationFn: () => updateApplication(id!, { name, description: description || undefined, baseUrl: baseUrl || undefined, environment }),
    onSuccess: () => {
      notify('Connected system saved', 'success')
      void invalidateApp()
    },
    onError: () => notify('Failed to save connected system', 'error'),
  })

  const statusMutation = useMutation({
    mutationFn: (action: Parameters<typeof changeApplicationStatus>[1]) => changeApplicationStatus(id!, action),
    onSuccess: () => {
      notify('Status updated', 'success')
      setStatusMenuAnchor(null)
      void invalidateApp()
    },
    onError: () => {
      notify('That status change was rejected by the backend', 'error')
      setStatusMenuAnchor(null)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => deleteApplication(id!),
    onSuccess: () => {
      notify('Connected system deleted', 'success')
      navigate('/workspaces')
    },
    onError: () => notify('Failed to delete connected system', 'error'),
  })

  const createApiMutation = useMutation({
    mutationFn: (values: Parameters<typeof createApplicationApi>[1]) => createApplicationApi(id!, values),
    onSuccess: () => {
      notify('API added', 'success')
      setApiDialogOpen(false)
      void queryClient.invalidateQueries({ queryKey: ['application-apis', id] })
    },
    onError: () => notify('Failed to add API', 'error'),
  })

  const updateApiMutation = useMutation({
    mutationFn: (input: { apiId: string; values: Parameters<typeof updateApplicationApi>[2] }) =>
      updateApplicationApi(id!, input.apiId, input.values),
    onSuccess: () => {
      notify('API updated', 'success')
      setApiDialogOpen(false)
      setEditingApi(null)
      void queryClient.invalidateQueries({ queryKey: ['application-apis', id] })
    },
    onError: () => notify('Failed to update API', 'error'),
  })

  const deleteApiMutation = useMutation({
    mutationFn: (apiId: string) => deleteApplicationApi(id!, apiId),
    onSuccess: () => {
      notify('API removed', 'success')
      setDeletingApi(null)
      void queryClient.invalidateQueries({ queryKey: ['application-apis', id] })
    },
    onError: () => notify('Failed to remove API', 'error'),
  })

  const saveConfigMutation = useMutation({
    mutationFn: () =>
      updateApplicationConfiguration(id!, {
        timeoutSeconds,
        maxRetries,
        ragThreshold,
        modelId: modelId || null,
        systemPromptTemplate: systemPromptTemplate || null,
      }),
    onSuccess: () => notify('Configuration saved', 'success'),
    onError: () => notify('Failed to save configuration', 'error'),
  })

  const createVersionMutation = useMutation({
    mutationFn: () => createApplicationVersion(id!, versionNote, versionPublish),
    onSuccess: () => {
      notify('Version created', 'success')
      setVersionNote('')
      setVersionPublish(false)
      void queryClient.invalidateQueries({ queryKey: ['application-versions', id] })
      void invalidateApp()
    },
    onError: () => notify('Failed to create version', 'error'),
  })

  const rollbackMutation = useMutation({
    mutationFn: (versionId: string) => rollbackApplicationVersion(id!, versionId),
    onSuccess: () => {
      notify('Rolled back — a new version was created', 'success')
      void queryClient.invalidateQueries({ queryKey: ['application-versions', id] })
      void queryClient.invalidateQueries({ queryKey: ['application-apis', id] })
      void invalidateApp()
    },
    onError: () => notify('Failed to roll back', 'error'),
  })

  if (query.isLoading) {
    return (
      <Stack spacing={2} sx={{ maxWidth: 800 }} role="status" aria-label="Loading connected system">
        <Button component={RouterLink} to="/workspaces" startIcon={<ArrowBackIcon />} size="small" sx={{ alignSelf: 'flex-start' }}>
          Connected Systems
        </Button>
        <Skeleton variant="text" width="45%" height={44} />
        <Skeleton variant="rounded" height={48} />
        <Skeleton variant="rounded" height={240} />
      </Stack>
    )
  }

  if (query.isError) {
    return (
      <Stack spacing={2}>
        <Button component={RouterLink} to="/workspaces" startIcon={<ArrowBackIcon />} size="small" sx={{ alignSelf: 'flex-start' }}>
          Connected Systems
        </Button>
        <ErrorState
          {...describeApiError(query.error, "Couldn't load this connected system")}
          onRetry={() => void query.refetch()}
        />
      </Stack>
    )
  }

  if (!app) {
    return (
      <EmptyState
        title="Connected system not found"
        description="It may have been removed, or you may not have access to it."
        actionLabel="Back to connected systems"
        onAction={() => navigate('/workspaces')}
      />
    )
  }

  const linkedAssistants = (assistantsQuery.data?.items ?? []).filter((a) => a.applicationId === id)
  const linkedWorkflows = (workflowsQuery.data?.items ?? []).filter((w) => w.applicationId === id)
  const linkedKnowledgeBases = knowledgeBasesQuery.data?.items ?? []
  const linkedTools = capabilitiesQuery.data?.items ?? []

  return (
    <Box sx={{ maxWidth: 1520, mx: 'auto', pb: 3 }}>
      <Button component={RouterLink} to="/workspaces" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
        All connections
      </Button>
      <Paper variant="outlined" sx={{ overflow: 'hidden', mb: 3, borderRadius: 3 }}>
        <Box sx={{ p: { xs: 2.5, md: 3.5 }, background: (theme) => `linear-gradient(115deg, ${theme.palette.primary.main}10 0%, ${theme.palette.background.paper} 66%)` }}>
          <Stack direction={{ xs: 'column', lg: 'row' }} spacing={2.5} sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', lg: 'center' } }}>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start', minWidth: 0 }}>
              <Box sx={{ display: 'grid', placeItems: 'center', flex: '0 0 auto', width: 52, height: 52, borderRadius: 2.5, color: 'primary.main', bgcolor: 'action.selected' }}>
                <HubOutlinedIcon />
              </Box>
              <Box sx={{ minWidth: 0 }}>
                <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: 1.1 }}>CONNECTED SYSTEM</Typography>
                <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', mb: 0.5, flexWrap: 'wrap' }}>
                  <Typography variant="h4" sx={{ fontWeight: 750, letterSpacing: '-0.035em', overflowWrap: 'anywhere' }}>{app.name}</Typography>
                  <Chip label={app.status} size="small" color={STATUS_COLOR[app.status] ?? 'default'} variant="outlined" />
                </Stack>
                <Typography color="text.secondary" sx={{ maxWidth: 760 }}>{app.description || 'Manage the APIs and access settings for this connected system.'}</Typography>
              </Box>
            </Stack>
            <Stack direction="row" useFlexGap sx={{ flexWrap: 'wrap', gap: 1, alignItems: 'center' }}>
              <Button variant="outlined" onClick={() => setDiscoverOpen(true)}>Discover from OpenAPI</Button>
              <Button variant="contained" endIcon={<ArrowDropDownIcon />} onClick={(e) => setStatusMenuAnchor(e.currentTarget)}>
                Change status
              </Button>
              <Menu anchorEl={statusMenuAnchor} open={!!statusMenuAnchor} onClose={() => setStatusMenuAnchor(null)}>
                {APPLICATION_ACTIONS.map((action) => (
                  <MenuItem key={action} onClick={() => statusMutation.mutate(action)}>
                    {action}
                  </MenuItem>
                ))}
              </Menu>
              <Button color="error" variant="outlined" onClick={() => setDeleteOpen(true)}>Delete</Button>
            </Stack>
          </Stack>
        </Box>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 1, sm: 4 }} useFlexGap sx={{ flexWrap: 'wrap', px: { xs: 2.5, md: 3.5 }, py: 1.5, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.default' }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Chip label={app.code} size="small" variant="outlined" />
            <Typography variant="body2" color="text.secondary">System code</Typography>
          </Stack>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Typography variant="body2" color="text.secondary">Environment</Typography>
            <Typography variant="body2" sx={{ fontWeight: 650 }}>{app.environment}</Typography>
          </Stack>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
            <Typography variant="body2" color="text.secondary">Base URL</Typography>
            <Typography variant="body2" sx={{ fontWeight: 650, overflowWrap: 'anywhere' }}>{app.baseUrl || 'Not configured'}</Typography>
          </Stack>
        </Stack>
      </Paper>

      <Tabs
        value={tab}
        onChange={(_, v) => setTab(v)}
        variant="scrollable"
        allowScrollButtonsMobile
        aria-label="Connected system sections"
        sx={{ mb: 3, borderBottom: '1px solid', borderColor: 'divider', '& .MuiTab-root': { minHeight: 54, fontWeight: 650 } }}
      >
        {TABS.map((t) => (
          <Tab key={t} label={t} value={t} />
        ))}
      </Tabs>

      {tab === 'General' && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(280px, 0.8fr) minmax(0, 1.5fr)' }, gap: 2.5 }}>
          <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 3, alignSelf: 'start' }}>
            <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>System overview</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>This connection’s identity and current target.</Typography>
            <Stack spacing={2}>
              <Box><Typography variant="caption" color="text.secondary">Name</Typography><Typography variant="body1" sx={{ fontWeight: 650, overflowWrap: 'anywhere' }}>{name || 'Untitled system'}</Typography></Box>
              <Box><Typography variant="caption" color="text.secondary">System code</Typography><Typography variant="body2" sx={{ fontWeight: 650 }}>{app.code}</Typography></Box>
              <Box><Typography variant="caption" color="text.secondary">Environment</Typography><Typography variant="body2" sx={{ fontWeight: 650 }}>{environment}</Typography></Box>
              <Box><Typography variant="caption" color="text.secondary">Base URL</Typography><Typography variant="body2" sx={{ fontWeight: 650, overflowWrap: 'anywhere' }}>{baseUrl || 'Not configured'}</Typography></Box>
              <Box><Typography variant="caption" color="text.secondary">Created</Typography><Typography variant="body2" sx={{ fontWeight: 650 }}>{new Date(app.createdAt).toLocaleDateString(undefined, { dateStyle: 'medium' })}</Typography></Box>
            </Stack>
          </Paper>
          <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 3 }}>
            <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>System settings</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>Update the connection details used by linked APIs and tools.</Typography>
            <Stack spacing={2}>
              <TextField label="Name" fullWidth value={name} onChange={(e) => setName(e.target.value)} />
              <TextField label="Description" fullWidth multiline minRows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
              <TextField label="Base URL" fullWidth value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} />
              <TextField
                select
                label="Environment"
                fullWidth
                value={environment}
                onChange={(e) => setEnvironment(e.target.value as ApplicationEnvironment)}
              >
                {APPLICATION_ENVIRONMENTS.map((env) => (
                  <MenuItem key={env} value={env}>{env}</MenuItem>
                ))}
              </TextField>
              <Stack direction="row" sx={{ justifyContent: 'flex-end', pt: 0.5 }}>
                <Button variant="contained" disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
                  {saveMutation.isPending ? 'Saving…' : 'Save changes'}
                </Button>
              </Stack>
            </Stack>
          </Paper>
        </Box>
      )}

      {tab === 'APIs' && (
        <Box>
          <Box sx={{ mb: 1.5 }}>
            <Button
              size="small"
              startIcon={<AddIcon />}
              onClick={() => {
                setEditingApi(null)
                setApiDialogOpen(true)
              }}
            >
              Add API
            </Button>
          </Box>
          {apisQuery.isLoading ? (
            <Stack spacing={1} sx={{ py: 2 }} role="status" aria-label="Loading APIs">
              {[0, 1, 2].map((i) => <Skeleton key={i} variant="rounded" height={56} />)}
            </Stack>
          ) : apisQuery.isError ? (
            <ErrorState
              {...describeApiError(apisQuery.error, "Couldn't load APIs")}
              onRetry={() => void apisQuery.refetch()}
            />
          ) : (apisQuery.data ?? []).length === 0 ? (
            <EmptyState title="No APIs registered for this connected system yet" description="Add an API to make its operations available to assistants and automations." />
          ) : (
            <List>
              {(apisQuery.data ?? []).map((a) => (
                <ListItem
                  key={a.id}
                  divider
                  secondaryAction={
                    <Stack direction="row">
                      <IconButton
                        size="small"
                        aria-label="Edit"
                        onClick={() => {
                          setEditingApi(a)
                          setApiDialogOpen(true)
                        }}
                      >
                        <EditIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" aria-label="Delete" onClick={() => setDeletingApi(a)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                  }
                >
                  <ListItemText
                    primary={
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <span>{a.name}</span>
                        <Chip label={a.authScheme} size="small" variant="outlined" />
                        {!a.isActive && <Chip label="Inactive" size="small" color="default" variant="outlined" />}
                      </Stack>
                    }
                    secondary={a.baseUrl}
                  />
                </ListItem>
              ))}
            </List>
          )}
        </Box>
      )}

      {tab === 'Configuration' && configQuery.isLoading && (
        <Stack spacing={1.5} sx={{ maxWidth: 600 }} role="status" aria-label="Loading configuration">
          {[0, 1, 2, 3].map((i) => <Skeleton key={i} variant="rounded" height={56} />)}
        </Stack>
      )}

      {tab === 'Configuration' && configQuery.isError && (
        <ErrorState
          {...describeApiError(configQuery.error, "Couldn't load configuration")}
          onRetry={() => void configQuery.refetch()}
        />
      )}

      {tab === 'Configuration' && !configQuery.isLoading && !configQuery.isError && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 600 }}>
          <Stack spacing={2}>
            <TextField
              label="Timeout (seconds)"
              type="number"
              fullWidth
              value={timeoutSeconds}
              onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
            />
            <TextField
              label="Max retries"
              type="number"
              fullWidth
              value={maxRetries}
              onChange={(e) => setMaxRetries(Number(e.target.value))}
            />
            <TextField
              label="RAG relevance threshold"
              type="number"
              fullWidth
              slotProps={{ htmlInput: { step: 0.05, min: 0, max: 1 } }}
              value={ragThreshold}
              onChange={(e) => setRagThreshold(Number(e.target.value))}
            />
            <TextField label="Model ID (optional)" fullWidth value={modelId} onChange={(e) => setModelId(e.target.value)} />
            <TextField
              label="System prompt template"
              fullWidth
              multiline
              minRows={3}
              value={systemPromptTemplate}
              onChange={(e) => setSystemPromptTemplate(e.target.value)}
            />
            <Box>
              <Button variant="contained" disabled={saveConfigMutation.isPending} onClick={() => saveConfigMutation.mutate()}>
                {saveConfigMutation.isPending ? 'Saving…' : 'Save'}
              </Button>
            </Box>
          </Stack>
        </Paper>
      )}

      {tab === 'Versions' && (
        <Box>
          <Paper variant="outlined" sx={{ p: 2, mb: 2, maxWidth: 600 }}>
            <Stack spacing={1.5}>
              <TextField
                label="Note (optional)"
                fullWidth
                size="small"
                value={versionNote}
                onChange={(e) => setVersionNote(e.target.value)}
              />
              <FormControlLabel
                control={<Checkbox checked={versionPublish} onChange={(e) => setVersionPublish(e.target.checked)} />}
                label="Publish this version"
              />
              <Box>
                <Button
                  size="small"
                  variant="contained"
                  disabled={createVersionMutation.isPending}
                  onClick={() => createVersionMutation.mutate()}
                >
                  {createVersionMutation.isPending ? 'Creating…' : 'Create Version'}
                </Button>
              </Box>
            </Stack>
          </Paper>
          {versionsQuery.isLoading ? (
            <Stack spacing={1} sx={{ py: 2 }} role="status" aria-label="Loading versions">
              {[0, 1, 2].map((i) => <Skeleton key={i} variant="rounded" height={56} />)}
            </Stack>
          ) : versionsQuery.isError ? (
            <ErrorState
              {...describeApiError(versionsQuery.error, "Couldn't load versions")}
              onRetry={() => void versionsQuery.refetch()}
            />
          ) : (versionsQuery.data ?? []).length === 0 ? (
            <EmptyState title="No versions yet" description="Create a version to record a recoverable snapshot of this connected system." />
          ) : (
            <List>
              {(versionsQuery.data ?? []).map((v) => (
                <ListItem
                  key={v.id}
                  divider
                  secondaryAction={
                    <IconButton size="small" aria-label="Rollback" onClick={() => rollbackMutation.mutate(v.id)}>
                      <RestoreIcon fontSize="small" />
                    </IconButton>
                  }
                >
                  <ListItemText
                    primary={
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <span>v{v.versionNumber}</span>
                        {v.isPublished && <Chip label="Published" size="small" color="success" variant="outlined" />}
                      </Stack>
                    }
                    secondary={v.note || new Date(v.createdAt).toLocaleString()}
                  />
                </ListItem>
              ))}
            </List>
          )}
        </Box>
      )}

      {tab === 'Ecosystem' && (
        assistantsQuery.isLoading || workflowsQuery.isLoading || knowledgeBasesQuery.isLoading || capabilitiesQuery.isLoading ? (
        <Stack spacing={1.5} role="status" aria-label="Loading connected resources">
          {[0, 1, 2, 3].map((i) => <Skeleton key={i} variant="rounded" height={88} />)}
        </Stack>
        ) : assistantsQuery.isError || workflowsQuery.isError || knowledgeBasesQuery.isError || capabilitiesQuery.isError ? (
          <ErrorState
            title="Couldn't load connected resources"
            description="Retry to refresh the assistants, automations, knowledge bases, and tools linked to this system."
            onRetry={() => void Promise.all([
              assistantsQuery.refetch(),
              workflowsQuery.refetch(),
              knowledgeBasesQuery.refetch(),
              capabilitiesQuery.refetch(),
            ])}
          />
        ) : (
        <Stack spacing={2}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Assistants ({linkedAssistants.length})
            </Typography>
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
              {linkedAssistants.map((a) => (
                <Chip key={a.id} label={a.name} size="small" variant="outlined" onClick={() => navigate(`/assistants/${a.id}`)} />
              ))}
              {linkedAssistants.length === 0 && (
                <Typography variant="body2" color="text.secondary">
                  None linked
                </Typography>
              )}
            </Stack>
          </Paper>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Automations ({linkedWorkflows.length})
            </Typography>
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
              {linkedWorkflows.map((w) => (
                <Chip key={w.id} label={w.name} size="small" variant="outlined" onClick={() => navigate(`/automations/${w.id}`)} />
              ))}
              {linkedWorkflows.length === 0 && (
                <Typography variant="body2" color="text.secondary">
                  None linked
                </Typography>
              )}
            </Stack>
          </Paper>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Knowledge Bases ({linkedKnowledgeBases.length})
            </Typography>
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
              {linkedKnowledgeBases.map((kb) => (
                <Chip key={kb.id} label={kb.name} size="small" variant="outlined" onClick={() => navigate(`/knowledge/${kb.id}`)} />
              ))}
              {linkedKnowledgeBases.length === 0 && (
                <Typography variant="body2" color="text.secondary">
                  None linked
                </Typography>
              )}
            </Stack>
          </Paper>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Tools ({linkedTools.length})
            </Typography>
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
              {linkedTools.map((t) => (
                <Chip key={t.id} label={t.name} size="small" variant="outlined" onClick={() => navigate(`/tools/${t.id}`)} />
              ))}
              {linkedTools.length === 0 && (
                <Typography variant="body2" color="text.secondary">
                  None linked
                </Typography>
              )}
            </Stack>
          </Paper>
        </Stack>
        )
      )}

      <CreateEditApplicationApiDialog
        open={apiDialogOpen}
        onClose={() => {
          setApiDialogOpen(false)
          setEditingApi(null)
        }}
        onSubmit={(values) => {
          if (editingApi) {
            updateApiMutation.mutate({ apiId: editingApi.id, values })
          } else {
            createApiMutation.mutate(values)
          }
        }}
        api={editingApi}
        isSubmitting={createApiMutation.isPending || updateApiMutation.isPending}
      />

      <DiscoverApplicationDialog
        open={discoverOpen}
        applicationId={id!}
        onClose={() => setDiscoverOpen(false)}
        onDiscovered={(result) => {
          setDiscoverOpen(false)
          notify(
            `Discovered ${result.operationsDiscovered} operation${result.operationsDiscovered === 1 ? '' : 's'} — ` +
              `${result.capabilitiesCreated} capabilit${result.capabilitiesCreated === 1 ? 'y' : 'ies'} created` +
              (result.capabilitiesSkippedAsExisting > 0 ? `, ${result.capabilitiesSkippedAsExisting} already existed` : ''),
            'success',
          )
          void invalidateApp()
          void queryClient.invalidateQueries({ queryKey: ['application-apis', id] })
        }}
      />

      <ConfirmDeleteDialog
        open={!!deletingApi}
        onClose={() => setDeletingApi(null)}
        onConfirm={() => deletingApi && deleteApiMutation.mutate(deletingApi.id)}
        title="Remove API"
        message={`Remove "${deletingApi?.name}"? This cannot be undone.`}
        isDeleting={deleteApiMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
        title="Delete Connected System"
        message={`Delete "${app.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
