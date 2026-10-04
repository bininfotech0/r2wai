import { useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import ApiIcon from '@mui/icons-material/Api'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlined'
import ErrorOutlineIcon from '@mui/icons-material/ReportProblemOutlined'
import HubOutlinedIcon from '@mui/icons-material/DeviceHubOutlined'
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined'
import ScheduleOutlinedIcon from '@mui/icons-material/ScheduleOutlined'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { ApiRequestError } from '../../../lib/api/fetchJson'
import {
  createCapabilityVersion,
  deleteCapability,
  getCapability,
  listCapabilityUsage,
  listCapabilityVersions,
  rollbackCapabilityVersion,
  setCapabilityStatus,
  testCapability,
  updateCapability,
} from '../api'
import { CreateEditCapabilityDialog } from '../dialogs/CreateEditCapabilityDialog'
import type { CapabilityFormInput } from '../types'

const TABS = ['Overview', 'Schema', 'Security', 'Test', 'Usage', 'Versions'] as const
type Tab = (typeof TABS)[number]

const RISK_COLOR: Record<string, 'default' | 'warning' | 'error'> = { Low: 'default', Medium: 'default', High: 'warning', Critical: 'error' }

export function ToolDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [tab, setTab] = useState<Tab>('Overview')
  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [versionDialogOpen, setVersionDialogOpen] = useState(false)
  const [versionNote, setVersionNote] = useState('')
  const [versionPublish, setVersionPublish] = useState(true)

  const query = useQuery({ queryKey: ['capability', id], queryFn: () => getCapability(id!), enabled: !!id })
  const usageQuery = useQuery({
    queryKey: ['capability-usage', id],
    queryFn: () => listCapabilityUsage(id!),
    enabled: !!id && tab === 'Usage',
  })
  const versionsQuery = useQuery({
    queryKey: ['capability-versions', id],
    queryFn: () => listCapabilityVersions(id!),
    enabled: !!id && tab === 'Versions',
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['capability', id] })

  const updateMutation = useMutation({
    mutationFn: (values: CapabilityFormInput) => updateCapability(id!, values),
    onSuccess: () => {
      notify('Tool updated', 'success')
      setEditOpen(false)
      void invalidate()
    },
    onError: () => notify('Failed to update tool', 'error'),
  })
  const statusMutation = useMutation({
    mutationFn: (isActive: boolean) => setCapabilityStatus(id!, isActive),
    onSuccess: () => {
      notify('Status updated', 'success')
      void invalidate()
    },
    onError: () => notify('Failed to update status', 'error'),
  })
  const deleteMutation = useMutation({
    mutationFn: () => deleteCapability(id!),
    onSuccess: () => {
      notify('Tool deleted', 'success')
      navigate('/tools')
    },
    onError: () => notify('Failed to delete tool', 'error'),
  })
  const testMutation = useMutation({ mutationFn: () => testCapability(id!) })
  const createVersionMutation = useMutation({
    mutationFn: () => createCapabilityVersion(id!, versionNote, versionPublish),
    onSuccess: () => {
      notify('Version created', 'success')
      setVersionDialogOpen(false)
      setVersionNote('')
      void queryClient.invalidateQueries({ queryKey: ['capability-versions', id] })
    },
    onError: () => notify('Failed to create version', 'error'),
  })
  const rollbackVersionMutation = useMutation({
    mutationFn: (versionId: string) => rollbackCapabilityVersion(id!, versionId),
    onSuccess: () => {
      notify('Rolled back', 'success')
      void queryClient.invalidateQueries({ queryKey: ['capability-versions', id] })
    },
    onError: () => notify('Failed to roll back', 'error'),
  })

  if (query.isError) {
    return <ErrorState title="Unable to load tool" description="The tool details could not be retrieved." onRetry={() => void query.refetch()} />
  }

  if (query.isLoading || !query.data) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    )
  }
  const tool = query.data
  const formattedDate = (value: string) => new Date(value).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
  const hasEndpoint = Boolean(tool.httpMethod || tool.endpointPath)

  return (
    <Box sx={{ maxWidth: 1440, mx: 'auto', pb: 4 }}>
      <Button component={RouterLink} to="/tools" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
        All tools
      </Button>

      <Paper variant="outlined" sx={{ overflow: 'hidden', mb: 3, borderRadius: 3 }}>
        <Box sx={{ p: { xs: 2.5, md: 3.5 }, background: (theme) => `linear-gradient(115deg, ${theme.palette.primary.main}12 0%, ${theme.palette.background.paper} 62%)` }}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={3} sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', md: 'center' } }}>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start', minWidth: 0 }}>
              <Box sx={{ display: 'grid', placeItems: 'center', flex: '0 0 auto', width: 52, height: 52, borderRadius: 2.5, color: 'primary.main', bgcolor: 'primary.main', backgroundColor: 'action.selected' }}>
                <ApiIcon />
              </Box>
              <Box sx={{ minWidth: 0 }}>
                <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: 1.2 }}>TOOL WORKSPACE</Typography>
                <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', mb: 0.5, flexWrap: 'wrap' }}>
                  <Typography variant="h4" sx={{ fontWeight: 750, letterSpacing: '-0.035em', overflowWrap: 'anywhere' }}>{tool.name}</Typography>
                  <Chip label={`${tool.riskLevel} risk`} size="small" color={RISK_COLOR[tool.riskLevel] ?? 'default'} variant="outlined" />
                </Stack>
                <Typography color="text.secondary" sx={{ maxWidth: 760 }}>
                  {tool.description || 'No description has been added for this tool yet.'}
                </Typography>
              </Box>
            </Stack>

            <Stack direction={{ xs: 'row', md: 'column' }} spacing={1} sx={{ alignItems: { xs: 'center', md: 'stretch' }, justifyContent: 'space-between', flex: '0 0 auto' }}>
              <FormControlLabel
                sx={{ m: 0, mr: { xs: 'auto', md: 0 }, pl: 1.25, pr: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 2, bgcolor: 'background.paper' }}
                control={<Switch checked={tool.isActive} onChange={(e) => statusMutation.mutate(e.target.checked)} disabled={statusMutation.isPending} size="small" />}
                label={<Typography variant="body2" sx={{ fontWeight: 650 }}>{tool.isActive ? 'Active' : 'Inactive'}</Typography>}
              />
              <Stack direction="row" spacing={1}>
                <Button variant="contained" onClick={() => setEditOpen(true)}>Edit tool</Button>
                <Button color="error" variant="outlined" onClick={() => setDeleteOpen(true)}>Delete</Button>
              </Stack>
            </Stack>
          </Stack>
        </Box>

        <Box sx={{ px: { xs: 2.5, md: 3.5 }, py: 1.5, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.default' }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 1, sm: 4 }} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              {tool.lastTestStatus === 'Connected' ? <CheckCircleOutlineIcon color="success" fontSize="small" /> : tool.lastTestStatus === 'Error' ? <ErrorOutlineIcon color="error" fontSize="small" /> : <ApiIcon color="disabled" fontSize="small" />}
              <Typography variant="body2" color="text.secondary">Connection</Typography>
              <Typography variant="body2" sx={{ fontWeight: 650 }}>{tool.lastTestStatus ?? 'Not tested'}</Typography>
            </Stack>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <HubOutlinedIcon color="action" fontSize="small" />
              <Typography variant="body2" color="text.secondary">Integration</Typography>
              <Typography variant="body2" sx={{ fontWeight: 650 }}>{tool.applicationApiName ?? 'Standalone'}</Typography>
            </Stack>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <ScheduleOutlinedIcon color="action" fontSize="small" />
              <Typography variant="body2" color="text.secondary">Last checked</Typography>
              <Typography variant="body2" sx={{ fontWeight: 650 }}>{tool.lastTestedAt ? formattedDate(tool.lastTestedAt) : 'No test recorded'}</Typography>
            </Stack>
          </Stack>
        </Box>
      </Paper>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" sx={{ mb: 3, borderBottom: '1px solid', borderColor: 'divider', '& .MuiTab-root': { minHeight: 54, fontWeight: 650 } }}>
        {TABS.map((t) => (
          <Tab key={t} label={t} value={t} />
        ))}
      </Tabs>

      {tab === 'Overview' && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1.55fr) minmax(300px, 1fr)' }, gap: 2.5 }}>
          <Stack spacing={2.5}>
            <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 3 }}>
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 2.5 }}>
                <ApiIcon color="primary" />
                <Box>
                  <Typography variant="h6" sx={{ fontWeight: 700 }}>Endpoint</Typography>
                  <Typography variant="body2" color="text.secondary">The API route this tool is configured to call.</Typography>
                </Box>
              </Stack>
              {hasEndpoint ? (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 2, borderRadius: 2, bgcolor: 'action.hover', border: '1px solid', borderColor: 'divider', flexWrap: 'wrap' }}>
                  <Chip label={tool.httpMethod ?? 'Method missing'} size="small" color="primary" />
                  <Typography component="code" sx={{ fontFamily: 'monospace', fontSize: '0.95rem', overflowWrap: 'anywhere' }}>{tool.endpointPath ?? 'Path not configured'}</Typography>
                </Box>
              ) : (
                <Alert severity="warning" variant="outlined">No method or path is configured yet. Edit this tool to complete its endpoint before testing it.</Alert>
              )}
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ mt: 2.5 }}>
                <Box><Typography variant="caption" color="text.secondary">HTTP method</Typography><Typography sx={{ fontWeight: 650 }}>{tool.httpMethod ?? 'Not configured'}</Typography></Box>
                <Box><Typography variant="caption" color="text.secondary">Endpoint path</Typography><Typography sx={{ fontWeight: 650, overflowWrap: 'anywhere' }}>{tool.endpointPath ?? 'Not configured'}</Typography></Box>
              </Stack>
            </Paper>

            <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 3 }}>
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 2 }}>
                <HubOutlinedIcon color="primary" />
                <Box><Typography variant="h6" sx={{ fontWeight: 700 }}>Connected service</Typography><Typography variant="body2" color="text.secondary">Where this tool gets its API access.</Typography></Box>
              </Stack>
              <Typography variant="body1" sx={{ fontWeight: 650 }}>{tool.applicationApiName ?? 'Standalone tool'}</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{tool.applicationApiName ? 'Managed through the linked integration.' : 'This tool is not linked to an integration.'}</Typography>
            </Paper>
          </Stack>

          <Stack spacing={2.5}>
            <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 3 }}>
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 2 }}>
                <ShieldOutlinedIcon color="primary" />
                <Box><Typography variant="h6" sx={{ fontWeight: 700 }}>Governance</Typography><Typography variant="body2" color="text.secondary">Controls applied when the tool runs.</Typography></Box>
              </Stack>
              <Stack divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
                {[
                  ['Risk level', tool.riskLevel],
                  ['Required role', tool.requiredRole ?? 'No role required'],
                  ['Confirmation', tool.confirmationRequired ? 'Required' : 'Not required'],
                  ['Approval', tool.approvalRequired ? 'Required' : 'Not required'],
                  ['Audit logging', tool.auditRequired ? 'Enabled' : 'Disabled'],
                ].map(([label, value]) => <Stack key={label} direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', py: 1.2, gap: 2 }}><Typography variant="body2" color="text.secondary">{label}</Typography><Typography variant="body2" sx={{ fontWeight: 650, textAlign: 'right' }}>{value}</Typography></Stack>)}
              </Stack>
            </Paper>

            <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>Record</Typography>
              <Stack spacing={1.25}>
                <Box><Typography variant="caption" color="text.secondary">Created</Typography><Typography variant="body2" sx={{ fontWeight: 600 }}>{formattedDate(tool.createdAt)}</Typography></Box>
                <Box><Typography variant="caption" color="text.secondary">Last modified</Typography><Typography variant="body2" sx={{ fontWeight: 600 }}>{tool.modifiedAt ? formattedDate(tool.modifiedAt) : 'No changes recorded'}</Typography></Box>
              </Stack>
            </Paper>
          </Stack>
        </Box>
      )}

      {tab === 'Schema' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 600 }}>
          <Alert severity="info" sx={{ mb: 2 }}>
            There's no input/output JSON-schema field on this resource yet — the backend only tracks the HTTP
            method and path shown below.
          </Alert>
          <Stack spacing={1}>
            <Typography variant="body2">
              <strong>Method:</strong> {tool.httpMethod ?? '—'}
            </Typography>
            <Typography variant="body2">
              <strong>Path:</strong> {tool.endpointPath ?? '—'}
            </Typography>
          </Stack>
        </Paper>
      )}

      {tab === 'Security' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 600 }}>
          <Stack spacing={1}>
            <Typography variant="body2">
              <strong>Risk level:</strong> {tool.riskLevel}
            </Typography>
            <Typography variant="body2">
              <strong>Required role:</strong> {tool.requiredRole ?? 'None'}
            </Typography>
            <Typography variant="body2">
              <strong>Confirmation required:</strong> {tool.confirmationRequired ? 'Yes' : 'No'}
            </Typography>
            <Typography variant="body2">
              <strong>Approval required:</strong> {tool.approvalRequired ? 'Yes' : 'No'}
            </Typography>
            <Typography variant="body2">
              <strong>Audit every call:</strong> {tool.auditRequired ? 'Yes' : 'No'}
            </Typography>
          </Stack>
        </Paper>
      )}

      {tab === 'Test' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 600 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
            Sends a real request to the configured endpoint, the same call path an AI agent takes when it invokes
            this tool during a live chat.
          </Typography>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            <Button variant="contained" onClick={() => testMutation.mutate()} disabled={testMutation.isPending}>
              {testMutation.isPending ? 'Testing…' : 'Run Test'}
            </Button>
            {testMutation.data && (
              <Alert severity={testMutation.data.success ? 'success' : 'warning'} sx={{ py: 0, flexGrow: 1 }}>
                {testMutation.data.message}
              </Alert>
            )}
            {testMutation.isError && (
              <Alert severity="error" sx={{ py: 0, flexGrow: 1 }}>
                {testMutation.error instanceof ApiRequestError ? testMutation.error.message : 'Test request failed.'}
              </Alert>
            )}
          </Stack>
        </Paper>
      )}

      {tab === 'Usage' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 700 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Every governed invocation of this tool — success, failure, and denials (missing role, approval required,
            risk ceiling) — from the AI function-call audit trail.
          </Typography>
          {usageQuery.isLoading ? (
            <LoadingSkeleton variant="text" count={4} height={44} />
          ) : usageQuery.isError ? (
            <ErrorState title="Unable to load tool usage" onRetry={() => void usageQuery.refetch()} />
          ) : (usageQuery.data?.items ?? []).length === 0 ? (
            <EmptyState title="No recorded calls yet" />
          ) : (
            <List dense>
              {(usageQuery.data?.items ?? []).map((u) => (
                <ListItem key={u.id} divider>
                  <ListItemText primary={`${u.action} · ${u.userName ?? 'System'}`} secondary={new Date(u.timestamp).toLocaleString()} />
                </ListItem>
              ))}
            </List>
          )}
        </Paper>
      )}

      {tab === 'Versions' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 700 }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' }, mb: 1.5 }}>
            <Typography variant="caption" color="text.secondary">
              Snapshots of this tool's configuration, newest first.
            </Typography>
            <Button size="small" onClick={() => setVersionDialogOpen(true)}>
              Create Version
            </Button>
          </Stack>
          {versionsQuery.isLoading ? (
            <LoadingSkeleton variant="text" count={3} height={48} />
          ) : versionsQuery.isError ? (
            <ErrorState title="Unable to load tool versions" onRetry={() => void versionsQuery.refetch()} />
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
                      <Button size="small" onClick={() => rollbackVersionMutation.mutate(v.id)} disabled={rollbackVersionMutation.isPending}>
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

          <Dialog open={versionDialogOpen} onClose={() => setVersionDialogOpen(false)} maxWidth="sm" fullWidth>
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
              >
                Create
              </Button>
            </DialogActions>
          </Dialog>
        </Paper>
      )}

      <CreateEditCapabilityDialog
        open={editOpen}
        onClose={() => setEditOpen(false)}
        onSubmit={(values) => updateMutation.mutate(values)}
        tool={tool}
        isSubmitting={updateMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
        title="Delete Tool"
        message={`Delete "${tool.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
