import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Box,
  Button,
  Grid,
  InputAdornment,
  MenuItem,
  Pagination,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import AddIcon from '@mui/icons-material/Add'
import SearchIcon from '@mui/icons-material/Search'
import ClearIcon from '@mui/icons-material/Close'
import ViewModuleIcon from '@mui/icons-material/ViewModule'
import ViewListIcon from '@mui/icons-material/ViewList'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import CheckIcon from '@mui/icons-material/Check'
import { DataTable } from '../../../components/data/DataTable'
import { StatusBadge } from '../../../components/StatusBadge'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { AgentCard, type AgentCardAction } from '../../../components/AgentCard'
import { RowActionsMenu } from '../../../components/data/RowActionsMenu'
import { NameCell } from '../../../components/data/NameCell'
import { ConfirmDialog } from '../../../components/dialogs/ConfirmDialog'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { describeApiError } from '../../../lib/api/fetchJson'
import { cloneAssistant, listAssistants, publishAssistant, unpublishAssistant } from '../api'
import { CreateAssistantDialog } from '../dialogs/CreateAssistantDialog'
import {
  ASSISTANT_SORTS,
  ASSISTANT_STATUSES,
  type AssistantSortKey,
  type AssistantStatusFilter,
} from '../assistantView'
import type { AssistantDto } from '../types'
import type { GridColDef } from '@mui/x-data-grid'
import { PageHeader } from '../../../components/PageHeader'

const CARD_PAGE_SIZE = 12

// Table first, as in Copilot Studio; the choice is remembered per browser.
const VIEW_KEY = 'r2wai.agents.view'
function readView(): 'card' | 'list' {
  try {
    return window.localStorage.getItem(VIEW_KEY) === 'card' ? 'card' : 'list'
  } catch {
    return 'list'
  }
}

/**
 * Assistant/agent library.
 *
 * Search, status filtering and sorting are all real server parameters
 * (docs/api/MISSING-BACKEND-ENDPOINTS.md §4, item 7 — closed 2026-09-29); status counts in the
 * filter labels are tenant-wide (scoped by search/applicationId, not narrowed by status itself),
 * not just the loaded page.
 *
 * Duplicate (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.3 #65) always creates a fresh, unpublished
 * draft — cloning a published assistant never silently publishes a second live one, regardless of
 * the source's own status. Version history/rollback (#63/#64) are real, tested backend endpoints
 * (GET/POST .../versions, POST .../versions/{versionId}/rollback) but have no UI here yet — matching
 * this codebase's existing precedent for the identical pattern on KnowledgeBases/Workflows/
 * Capabilities, which are also backend-only with no client surface today.
 */
export function AssistantsLibraryPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [view, setView] = useState<'card' | 'list'>(readView)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<AssistantStatusFilter>('All')
  const [sort, setSort] = useState<AssistantSortKey>('recent')
  const [page, setPage] = useState(1)
  const [listPageSize, setListPageSize] = useState(20)
  const pageSize = view === 'card' ? CARD_PAGE_SIZE : listPageSize
  const [createOpen, setCreateOpen] = useState(false)
  const [publishTarget, setPublishTarget] = useState<AssistantDto | null>(null)
  const [unpublishTarget, setUnpublishTarget] = useState<AssistantDto | null>(null)

  const query = useQuery({
    queryKey: queryKeys.assistants.list(page, pageSize, search, status, sort),
    queryFn: () => listAssistants(page, pageSize, search, status, sort),
  })

  const publishMutation = useMutation({
    mutationFn: (id: string) => publishAssistant(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.assistants.all })
      notify('Agent published')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not publish this agent.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  const unpublishMutation = useMutation({
    mutationFn: (id: string) => unpublishAssistant(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.assistants.all })
      notify('Agent moved back to draft', 'info')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not unpublish this agent.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  const cloneMutation = useMutation({
    mutationFn: (id: string) => cloneAssistant(id),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.assistants.all })
      notify(`Created "${created.name}" as a draft copy`)
      navigate(`/assistants/${created.id}`)
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not duplicate this agent.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  // Already filtered/sorted server-side — no client-side re-application needed.
  const visible = useMemo(() => query.data?.items ?? [], [query.data])

  const counts = useMemo(() => {
    const sc = query.data?.statusCounts ?? {}
    const draft = sc.Draft ?? 0
    const published = sc.Published ?? 0
    const archived = sc.Archived ?? 0
    return { All: draft + published + archived, Draft: draft, Published: published, Archived: archived }
  }, [query.data])

  const totalCount = query.data?.totalCount ?? 0
  const pageCount = Math.ceil(totalCount / pageSize)

  useEffect(() => {
    if (pageCount > 0 && page > pageCount) setPage(pageCount)
  }, [page, pageCount])

  function actionsFor(assistant: AssistantDto): AgentCardAction[] {
    return [
      { label: 'Open', onSelect: () => navigate(`/assistants/${assistant.id}`) },
      assistant.publishStatus === 'Published'
        ? { label: 'Unpublish', onSelect: () => setUnpublishTarget(assistant) }
        : { label: 'Publish', onSelect: () => setPublishTarget(assistant) },
      { label: 'Duplicate', onSelect: () => cloneMutation.mutate(assistant.id) },
    ]
  }

  const columns: GridColDef<AssistantDto>[] = [
    {
      field: 'name',
      headerName: 'Name',
      flex: 1,
      minWidth: 240,
      renderCell: ({ row }) => <NameCell icon={SmartToyOutlined} name={row.name} secondary={row.description || 'No description'} />,
    },
    {
      field: 'publishStatus',
      headerName: 'Status',
      width: 150,
      renderCell: ({ row }) => (
        <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', height: '100%' }}>
          <StatusBadge status={row.publishStatus} />
          {row.publishStatus === 'Published' && <Typography variant="caption" color="text.secondary">v{row.publishedVersion}</Typography>}
        </Stack>
      ),
    },
    { field: 'type', headerName: 'Type', width: 120 },
    {
      field: 'knowledgeBaseId',
      headerName: 'Knowledge',
      width: 110,
      sortable: false,
      renderCell: ({ row }) => (
        <Box sx={{ display: 'flex', alignItems: 'center', height: '100%' }}>
          {row.knowledgeBaseId
            ? <CheckIcon fontSize="small" color="success" titleAccess="Knowledge linked" />
            : <Typography variant="caption" color="text.secondary">—</Typography>}
        </Box>
      ),
    },
    { field: 'usageCount', headerName: 'Conversations', width: 130, type: 'number' },
    { field: 'createdAt', headerName: 'Created', width: 130, valueFormatter: (v: string) => new Date(v).toLocaleDateString() },
    {
      field: 'actions',
      headerName: '',
      width: 56,
      sortable: false,
      disableColumnMenu: true,
      renderCell: ({ row }) => <RowActionsMenu label={row.name} actions={actionsFor(row)} />,
    },
  ]

  const isFiltered = status !== 'All' || search.trim() !== ''

  return (
    <Box>
      <PageHeader
        title="Agents"
        description="Agents answer questions from your knowledge and take action in your connected systems."
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            New agent
          </Button>
        }
      />

      <Stack direction="row" spacing={1.5} useFlexGap sx={{ mb: 2, alignItems: 'center', flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Search agents…"
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
                  <IconButton
                    aria-label="Clear search"
                    size="small"
                    edge="end"
                    onClick={() => {
                      setSearch('')
                      setPage(1)
                    }}
                  >
                    <ClearIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ) : undefined,
            },
            htmlInput: { 'aria-label': 'Search agents' },
          }}
          sx={{ width: { xs: '100%', sm: 300 } }}
        />
        <TextField
          select
          size="small"
          label="Status"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as AssistantStatusFilter)
            setPage(1)
          }}
          sx={{ width: 170 }}
        >
          <MenuItem value="All">All ({counts.All})</MenuItem>
          {ASSISTANT_STATUSES.map((s) => <MenuItem key={s} value={s}>{s} ({counts[s]})</MenuItem>)}
        </TextField>
        <TextField
          select
          size="small"
          label="Sort by"
          value={sort}
          onChange={(e) => {
            setSort(e.target.value as AssistantSortKey)
            setPage(1)
          }}
          sx={{ width: 190 }}
        >
          {ASSISTANT_SORTS.map((s) => <MenuItem key={s.value} value={s.value}>{s.label}</MenuItem>)}
        </TextField>
        <Box sx={{ flexGrow: 1 }} />
        <ToggleButtonGroup
          size="small"
          exclusive
          value={view}
          onChange={(_, v: 'card' | 'list' | null) => {
            if (!v) return
            setView(v)
            setPage(1)
            try {
              window.localStorage.setItem(VIEW_KEY, v)
            } catch {
              // Preference only.
            }
          }}
          aria-label="Agent view"
        >
          <ToggleButton value="list" aria-label="List view">
            <ViewListIcon fontSize="small" />
          </ToggleButton>
          <ToggleButton value="card" aria-label="Card view">
            <ViewModuleIcon fontSize="small" />
          </ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      {query.isError ? (
        <ErrorState
          {...describeApiError(query.error, 'Could not load your agents.')}
          onRetry={() => void query.refetch()}
        />
      ) : view === 'list' ? (
        <DataTable
          rows={visible}
          columns={columns}
          loading={query.isLoading}
          error={query.error}
          onRetry={() => void query.refetch()}
          rowCount={query.data?.totalCount ?? 0}
          page={page}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={(nextPageSize) => {
            setListPageSize(nextPageSize)
            setPage(1)
          }}
          onRowClick={(id) => navigate(`/assistants/${id}`)}
          emptyState={
            <EmptyState
              icon={SmartToyOutlined}
              title={isFiltered ? 'No agents match these filters' : 'No agents yet'}
              description={isFiltered ? 'Try clearing the search or status filter.' : 'Create your first agent to get started.'}
              actionLabel={isFiltered ? undefined : 'New agent'}
              onAction={isFiltered ? undefined : () => setCreateOpen(true)}
            />
          }
        />
      ) : query.isLoading ? (
        <LoadingSkeleton count={6} />
      ) : visible.length === 0 ? (
        <EmptyState
          icon={SmartToyOutlined}
          title={isFiltered ? 'No agents match these filters' : 'No agents yet'}
          description={isFiltered ? 'Try clearing the search or status filter.' : 'Create your first agent to get started.'}
          actionLabel={isFiltered ? undefined : 'New agent'}
          onAction={isFiltered ? undefined : () => setCreateOpen(true)}
        />
      ) : (
        <>
          <Grid container spacing={2}>
            {visible.map((assistant) => (
              <Grid key={assistant.id} size={{ xs: 12, sm: 6, md: 4 }}>
                <AgentCard
                  name={assistant.name}
                  description={assistant.description}
                  status={assistant.publishStatus}
                  type={assistant.type}
                  avatarUrl={assistant.avatarUrl}
                  usageCount={assistant.usageCount}
                  publishedVersion={assistant.publishedVersion}
                  hasKnowledge={!!assistant.knowledgeBaseId}
                  actions={actionsFor(assistant)}
                  onOpen={() => navigate(`/assistants/${assistant.id}`)}
                />
              </Grid>
            ))}
          </Grid>
          {pageCount > 1 && (
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              spacing={1.5}
              sx={{ alignItems: 'center', justifyContent: 'space-between', mt: 3 }}
            >
              <Typography variant="body2" color="text.secondary">
                Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount} agents
              </Typography>
              <Pagination
                count={pageCount}
                page={page}
                onChange={(_, nextPage) => setPage(nextPage)}
                color="primary"
                size="small"
                aria-label="Agent pages"
              />
            </Stack>
          )}
        </>
      )}

      <CreateAssistantDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(id) => {
          setCreateOpen(false)
          navigate(`/assistants/${id}`)
        }}
      />

      <ConfirmDialog
        open={!!publishTarget}
        title="Publish this agent?"
        message={
          publishTarget
            ? `“${publishTarget.name}” becomes available to every channel it is deployed to. Anyone who can reach those channels can talk to it.`
            : ''
        }
        confirmLabel="Publish"
        busy={publishMutation.isPending}
        onClose={() => setPublishTarget(null)}
        onConfirm={() => {
          if (publishTarget) publishMutation.mutate(publishTarget.id)
          setPublishTarget(null)
        }}
      />

      <ConfirmDialog
        open={!!unpublishTarget}
        title="Move back to draft?"
        message={
          unpublishTarget
            ? `“${unpublishTarget.name}” will stop serving new conversations on every channel. Existing conversation history is kept.`
            : ''
        }
        confirmLabel="Unpublish"
        busy={unpublishMutation.isPending}
        onClose={() => setUnpublishTarget(null)}
        onConfirm={() => {
          if (unpublishTarget) unpublishMutation.mutate(unpublishTarget.id)
          setUnpublishTarget(null)
        }}
      />
    </Box>
  )
}
