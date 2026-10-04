import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  InputAdornment,
  Pagination,
  Skeleton,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import SearchIcon from '@mui/icons-material/Search'
import ClearIcon from '@mui/icons-material/Close'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import ExtensionIcon from '@mui/icons-material/Extension'
import DeleteIcon from '@mui/icons-material/Delete'
import EditIcon from '@mui/icons-material/Edit'
import TuneIcon from '@mui/icons-material/Tune'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import ErrorIcon from '@mui/icons-material/Error'
import HelpOutlineIcon from '@mui/icons-material/HelpOutlineOutlined'
import IconButton from '@mui/material/IconButton'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { PageHeader } from '../../../components/PageHeader'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { ApiRequestError } from '../../../lib/api/fetchJson'
import {
  createIntegration,
  deleteIntegration,
  getIntegrationCatalog,
  listIntegrations,
  testIntegration,
  toggleIntegration,
  updateIntegration,
} from '../api'
import { CreateEditIntegrationDialog, type IntegrationFormValues } from '../dialogs/CreateEditIntegrationDialog'
import { ImportOpenApiDialog } from '../dialogs/ImportOpenApiDialog'
import { TOOL_TYPES, TOOL_TYPE_LABELS, type AuthType, type IntegrationCatalogEntryDto, type IntegrationDto } from '../types'

function parseAuthType(configuration: string | null): AuthType | null {
  if (!configuration) return null
  try {
    const parsed = JSON.parse(configuration) as { AuthType?: AuthType }
    return parsed.AuthType ?? null
  } catch {
    return null
  }
}

function StatusChip({ integration }: { integration: IntegrationDto }) {
  if (!integration.isActive) {
    return <Chip size="small" label="Disabled" variant="outlined" />
  }
  if (integration.lastTestStatus === 'Connected') {
    return <Chip size="small" color="success" icon={<CheckCircleIcon />} label="Connected" />
  }
  if (integration.lastTestStatus === 'Error') {
    return <Chip size="small" color="error" icon={<ErrorIcon />} label="Error" />
  }
  return <Chip size="small" variant="outlined" icon={<HelpOutlineIcon />} label="Not tested" />
}

function AdvancedRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start', justifyContent: 'space-between' }}>
      <Typography variant="caption" color="text.secondary" sx={{ minWidth: 110, pt: 0.25 }}>
        {label}
      </Typography>
      <Typography variant="body2" sx={{ textAlign: 'right', wordBreak: 'break-all' }}>
        {value}
      </Typography>
    </Stack>
  )
}

const QUERY_KEY = 'integrations'
const PAGE_SIZE = 12

export function IntegrationsLibraryPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [view, setView] = useState<'mine' | 'catalog'>('mine')
  const [tab, setTab] = useState<string>('All')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<IntegrationDto | null>(null)
  const [connectFrom, setConnectFrom] = useState<IntegrationCatalogEntryDto | null>(null)
  const [deleting, setDeleting] = useState<IntegrationDto | null>(null)
  const [importOpen, setImportOpen] = useState(false)
  const [advancedFor, setAdvancedFor] = useState<IntegrationDto | null>(null)
  const [testingId, setTestingId] = useState<string | null>(null)
  const [testResult, setTestResult] = useState<{ id: string; success: boolean; message: string } | null>(null)

  const query = useQuery({
    queryKey: [QUERY_KEY, page, PAGE_SIZE, tab, search],
    queryFn: () => listIntegrations(page, PAGE_SIZE, search, tab === 'All' ? undefined : tab),
    enabled: view === 'mine',
  })
  const integrationCount = query.data?.totalCount ?? 0
  const pageCount = Math.ceil(integrationCount / PAGE_SIZE)

  const catalogQuery = useQuery({
    queryKey: ['integration-catalog'],
    queryFn: getIntegrationCatalog,
    enabled: view === 'catalog',
  })

  useEffect(() => {
    if (!query.isSuccess) return
    const pageCount = Math.ceil((query.data?.totalCount ?? 0) / PAGE_SIZE)
    if (page > pageCount) setPage(Math.max(1, pageCount))
  }, [page, query.data?.totalCount, query.isSuccess])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })

  const createMutation = useMutation({
    mutationFn: createIntegration,
    onSuccess: () => {
      notify('Integration created', 'success')
      setDialogOpen(false)
      setPage(1)
      if (connectFrom) {
        setConnectFrom(null)
        setView('mine') // so the admin immediately sees what they just connected
      }
      void invalidate()
    },
    onError: () => notify('Failed to create integration', 'error'),
  })

  const updateMutation = useMutation({
    mutationFn: (input: { id: string; values: IntegrationFormValues }) => updateIntegration(input.id, input.values),
    onSuccess: () => {
      notify('Integration updated', 'success')
      setDialogOpen(false)
      setEditing(null)
      void invalidate()
    },
    onError: () => notify('Failed to update integration', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteIntegration,
    onSuccess: () => {
      notify('Integration deleted', 'success')
      setDeleting(null)
      void invalidate()
    },
    onError: () => notify('Failed to delete integration', 'error'),
  })

  const toggleMutation = useMutation({
    mutationFn: toggleIntegration,
    onSuccess: () => void invalidate(),
    onError: () => notify('Failed to toggle integration', 'error'),
  })

  const testMutation = useMutation({
    mutationFn: testIntegration,
    onMutate: (id: string) => {
      setTestingId(id)
      setTestResult(null)
    },
    onSuccess: (result, id) => {
      setTestResult({ id, success: result.success, message: result.message })
      void invalidate()
    },
    onError: (err, id) => {
      const message = err instanceof ApiRequestError ? err.message : 'Connection test failed.'
      setTestResult({ id, success: false, message })
      void invalidate()
    },
    onSettled: () => setTestingId(null),
  })

  function handleSubmit(values: IntegrationFormValues) {
    if (editing) {
      updateMutation.mutate({ id: editing.id, values })
    } else {
      createMutation.mutate(values)
    }
  }

  return (
    <Box>
      <PageHeader
        title="Integrations"
        description="Connect R2WAI to external systems assistants and automations can call."
        actions={
          <>
            <Button variant="outlined" startIcon={<UploadFileIcon />} onClick={() => setImportOpen(true)}>
              Import from OpenAPI
            </Button>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() => {
                setEditing(null)
                setDialogOpen(true)
              }}
            >
              New Integration
            </Button>
          </>
        }
      />

      <Tabs
        value={view}
        onChange={(_, v) => setView(v)}
        variant="scrollable"
        allowScrollButtonsMobile
        aria-label="Integration views"
        sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Tab label="My Integrations" value="mine" />
        <Tab label="Catalog" value="catalog" />
      </Tabs>

      {view === 'catalog' ? (
        <Grid container spacing={2}>
          {catalogQuery.isLoading ? (
            Array.from({ length: 6 }).map((_, i) => (
              <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }}>
                <Skeleton variant="rounded" height={140} />
              </Grid>
            ))
          ) : catalogQuery.isError ? (
            <Grid size={12}><ErrorState title="Unable to load the integration catalog" onRetry={() => void catalogQuery.refetch()} /></Grid>
          ) : catalogQuery.data?.length === 0 ? (
            <Grid size={12}><EmptyState icon={ExtensionIcon} title="Catalog is empty" description="You can still create an integration manually." /></Grid>
          ) : (
            (catalogQuery.data ?? []).map((entry) => (
              <Grid key={entry.id} size={{ xs: 12, sm: 6, md: 4 }}>
                <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
                  <CardContent sx={{ flexGrow: 1 }}>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                      <ExtensionIcon color="primary" />
                      <Typography variant="subtitle1" sx={{ fontWeight: 600, flexGrow: 1 }} noWrap>
                        {entry.name}
                      </Typography>
                    </Stack>
                    <Chip label={entry.category} size="small" variant="outlined" sx={{ mb: 1 }} />
                    <Typography variant="body2" color="text.secondary">
                      {entry.description}
                    </Typography>
                  </CardContent>
                  <Stack sx={{ px: 1.5, py: 1, borderTop: '1px solid', borderColor: 'divider' }}>
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => {
                        setEditing(null)
                        setConnectFrom(entry)
                        setDialogOpen(true)
                      }}
                    >
                      Connect
                    </Button>
                  </Stack>
                </Card>
              </Grid>
            ))
          )}
        </Grid>
      ) : (
      <>
      <Stack direction="row" spacing={2} sx={{ mb: 1, alignItems: 'center', flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Search integrations…"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
          slotProps={{
            input: {
              startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment>,
              endAdornment: search ? (
                <InputAdornment position="end">
                  <IconButton aria-label="Clear search" size="small" edge="end" onClick={() => { setSearch(''); setPage(1) }}>
                    <ClearIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ) : undefined,
            },
            htmlInput: { 'aria-label': 'Search integrations' },
          }}
          sx={{ width: { xs: '100%', sm: 320 } }}
        />
      </Stack>

      <Tabs
        value={tab}
        onChange={(_, v) => {
          setTab(v)
          setPage(1)
        }}
        variant="scrollable"
        allowScrollButtonsMobile
        aria-label="Filter integrations by type"
        sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Tab label="All" value="All" />
        {TOOL_TYPES.map((t) => (
          <Tab key={t} label={TOOL_TYPE_LABELS[t]} value={t} />
        ))}
      </Tabs>

      {query.isLoading ? (
        <Grid container spacing={2}>
          {Array.from({ length: 6 }).map((_, i) => (
            <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                    <Skeleton variant="circular" width={24} height={24} />
                    <Skeleton variant="text" width="60%" />
                  </Stack>
                  <Skeleton variant="text" sx={{ mb: 1 }} />
                  <Skeleton variant="rounded" width={70} height={22} sx={{ borderRadius: 999 }} />
                </CardContent>
                <Stack direction="row" sx={{ justifyContent: 'space-between', px: 1.5, py: 0.5, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Skeleton variant="rounded" width={54} height={20} sx={{ borderRadius: 999 }} />
                  <Skeleton variant="circular" width={20} height={20} />
                </Stack>
              </Card>
            </Grid>
          ))}
        </Grid>
      ) : query.isError ? (
        <ErrorState title="Unable to load integrations" description="Your integrations are unchanged. Try again to refresh the list." onRetry={() => void query.refetch()} />
      ) : (
      <>
      <Grid container spacing={2}>
        {(query.data?.items ?? []).map((integration) => (
          <Grid key={integration.id} size={{ xs: 12, sm: 6, md: 4 }}>
            <Card variant="outlined">
              <CardContent>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                  <ExtensionIcon color="primary" />
                  <Tooltip title={integration.name}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 600, flexGrow: 1 }} noWrap>
                      {integration.name}
                    </Typography>
                  </Tooltip>
                  <StatusChip integration={integration} />
                </Stack>
                <Typography variant="body2" color="text.secondary" noWrap sx={{ minHeight: 20, mb: 1 }}>
                  {integration.endpointUrl || integration.description || 'No endpoint configured'}
                </Typography>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <Chip
                    label={TOOL_TYPE_LABELS[integration.type as keyof typeof TOOL_TYPE_LABELS] || integration.type || 'Tool'}
                    size="small"
                    variant="outlined"
                  />
                  {integration.lastTestedAt && (
                    <Tooltip title={new Date(integration.lastTestedAt).toLocaleString()}>
                      <Typography variant="caption" color="text.secondary">
                        tested {new Date(integration.lastTestedAt).toLocaleDateString()}
                      </Typography>
                    </Tooltip>
                  )}
                </Stack>
                {testResult?.id === integration.id && (
                  <Alert severity={testResult.success ? 'success' : 'warning'} sx={{ mt: 1, py: 0 }} onClose={() => setTestResult(null)}>
                    {testResult.message}
                  </Alert>
                )}
              </CardContent>
              <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 1.5, py: 0.5, borderTop: '1px solid', borderColor: 'divider' }}>
                <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                  <Switch
                    size="small"
                    checked={integration.isActive}
                    onChange={() => toggleMutation.mutate(integration.id)}
                  />
                  <Typography variant="caption" color="text.secondary">
                    {integration.isActive ? 'Active' : 'Inactive'}
                  </Typography>
                </Stack>
                <Stack direction="row" spacing={0.5}>
                  <Tooltip title="Test connection">
                    <span>
                      <Button
                        size="small"
                        disabled={testingId === integration.id}
                        onClick={() => testMutation.mutate(integration.id)}
                      >
                        {testingId === integration.id ? 'Testing…' : 'Test'}
                      </Button>
                    </span>
                  </Tooltip>
                  <Tooltip title="Edit">
                    <IconButton
                      size="small"
                      aria-label="Edit"
                      onClick={() => {
                        setEditing(integration)
                        setDialogOpen(true)
                      }}
                    >
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Advanced">
                    <IconButton size="small" aria-label="Advanced" onClick={() => setAdvancedFor(integration)}>
                      <TuneIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton size="small" aria-label="Delete" onClick={() => setDeleting(integration)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Stack>
              </Stack>
            </Card>
          </Grid>
        ))}
        {query.data?.items.length === 0 && (
          <Grid size={12}>
            <EmptyState
              icon={ExtensionIcon}
              title={search || tab !== 'All' ? 'No integrations match your filters' : 'No integrations yet'}
              description={search || tab !== 'All' ? undefined : 'Connect your first external system to get started.'}
              actionLabel={search || tab !== 'All' ? undefined : 'New Integration'}
              onAction={
                search || tab !== 'All'
                  ? undefined
                  : () => {
                      setEditing(null)
                      setDialogOpen(true)
                    }
              }
            />
          </Grid>
        )}
      </Grid>
      {pageCount > 1 && (
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1.5}
          sx={{ alignItems: 'center', justifyContent: 'space-between', mt: 3 }}
        >
          <Typography variant="body2" color="text.secondary">
            Showing {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, integrationCount)} of {integrationCount} integrations
          </Typography>
          <Pagination
            count={pageCount}
            page={page}
            onChange={(_, nextPage) => setPage(nextPage)}
            color="primary"
            size="small"
            aria-label="Integration pages"
          />
        </Stack>
      )}
      </>
      )}
      </>
      )}

      <CreateEditIntegrationDialog
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false)
          setEditing(null)
          setConnectFrom(null)
        }}
        onSubmit={handleSubmit}
        integration={editing}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
        initialValues={
          connectFrom
            ? {
                name: connectFrom.name,
                type: connectFrom.suggestedType,
                description: connectFrom.description,
                endpointUrl: connectFrom.suggestedEndpointUrl ?? undefined,
                authType: connectFrom.suggestedAuthType,
              }
            : undefined
        }
      />

      <ImportOpenApiDialog open={importOpen} onClose={() => setImportOpen(false)} />

      <Dialog open={!!advancedFor} onClose={() => setAdvancedFor(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Advanced — {advancedFor?.name}</DialogTitle>
        <DialogContent>
          {advancedFor && (
            <Stack divider={<Divider />} spacing={1.25} sx={{ pb: 1 }}>
              <AdvancedRow label="ID" value={advancedFor.id} />
              <AdvancedRow label="Type" value={TOOL_TYPE_LABELS[advancedFor.type as keyof typeof TOOL_TYPE_LABELS] || advancedFor.type} />
              <AdvancedRow label="Endpoint URL" value={advancedFor.endpointUrl || '—'} />
              <AdvancedRow label="Auth type" value={parseAuthType(advancedFor.configuration) ?? 'None'} />
              <AdvancedRow label="Last tested" value={advancedFor.lastTestedAt ? new Date(advancedFor.lastTestedAt).toLocaleString() : 'Never'} />
              <AdvancedRow label="Last test result" value={advancedFor.lastTestStatus ?? 'Not tested'} />
              <AdvancedRow label="Created" value={new Date(advancedFor.createdAt).toLocaleString()} />
            </Stack>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete Integration"
        message={`Delete "${deleting?.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
