import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Box,
  Button,
  Grid,
  InputAdornment,
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
import { DataTable } from '../../../components/data/DataTable'
import { StatusBadge } from '../../../components/StatusBadge'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { AgentCard, type AgentCardAction } from '../../../components/AgentCard'
import { FilterBar, type FilterBarField } from '../../../components/FilterBar'
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

const CARD_PAGE_SIZE = 12

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

  const [view, setView] = useState<'card' | 'list'>('card')
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
      notify('Assistant published')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not publish this assistant.')
      notify(`${described.title} ${described.description ?? ''}`.trim(), 'error')
    },
  })

  const unpublishMutation = useMutation({
    mutationFn: (id: string) => unpublishAssistant(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.assistants.all })
      notify('Assistant moved back to draft', 'info')
    },
    onError: (error) => {
      const described = describeApiError(error, 'Could not unpublish this assistant.')
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
      const described = describeApiError(error, 'Could not duplicate this assistant.')
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

  const filterFields: FilterBarField[] = [
    {
      key: 'status',
      label: 'Status',
      value: status,
      onChange: (v) => {
        setStatus(v as AssistantStatusFilter)
        setPage(1)
      },
      options: [
        { value: 'All', label: `All (${counts.All})` },
        ...ASSISTANT_STATUSES.map((s) => ({ value: s, label: `${s} (${counts[s]})` })),
      ],
    },
    {
      key: 'sort',
      label: 'Sort by',
      value: sort,
      onChange: (v) => {
        setSort(v as AssistantSortKey)
        setPage(1)
      },
      options: ASSISTANT_SORTS.map((s) => ({ value: s.value, label: s.label })),
    },
  ]

  const columns: GridColDef<AssistantDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'type', headerName: 'Type', width: 130 },
    {
      field: 'publishStatus',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <StatusBadge status={params.value} />,
    },
    { field: 'usageCount', headerName: 'Used', width: 90 },
    { field: 'createdAt', headerName: 'Created', width: 150, valueFormatter: (v: string) => new Date(v).toLocaleDateString() },
  ]

  const isFiltered = status !== 'All' || search.trim() !== ''

  return (
    <Box>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { xs: 'stretch', sm: 'center' }, justifyContent: 'space-between', mb: 2 }}
      >
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Agents
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Build assistants that answer questions and perform enterprise tasks.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)} sx={{ alignSelf: { xs: 'flex-start', sm: 'auto' } }}>
          New Assistant
        </Button>
      </Stack>

      <Stack direction="row" spacing={2} sx={{ mb: 2, alignItems: 'center', flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Search assistants…"
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
            htmlInput: { 'aria-label': 'Search assistants' },
          }}
          sx={{ width: { xs: '100%', sm: 320 } }}
        />
        <Box sx={{ flexGrow: 1 }} />
        <ToggleButtonGroup
          size="small"
          exclusive
          value={view}
          onChange={(_, v: 'card' | 'list' | null) => {
            if (!v) return
            setView(v)
            setPage(1)
          }}
          aria-label="Assistant view"
        >
          <ToggleButton value="card" aria-label="Card view">
            <ViewModuleIcon fontSize="small" />
          </ToggleButton>
          <ToggleButton value="list" aria-label="List view">
            <ViewListIcon fontSize="small" />
          </ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      <FilterBar filters={filterFields} />

      {query.isError ? (
        <ErrorState
          {...describeApiError(query.error, 'Could not load your assistants.')}
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
        />
      ) : query.isLoading ? (
        <LoadingSkeleton count={6} />
      ) : visible.length === 0 ? (
        <EmptyState
          icon={SmartToyOutlined}
          title={isFiltered ? 'No assistants match these filters' : 'No assistants yet'}
          description={isFiltered ? 'Try clearing the search or status filter.' : 'Create your first assistant to get started.'}
          actionLabel={isFiltered ? undefined : 'New Assistant'}
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
                Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount} assistants
              </Typography>
              <Pagination
                count={pageCount}
                page={page}
                onChange={(_, nextPage) => setPage(nextPage)}
                color="primary"
                size="small"
                aria-label="Assistant pages"
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
        title="Publish this assistant?"
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
