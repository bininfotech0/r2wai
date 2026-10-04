import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Paper } from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import type { GridColDef } from '@mui/x-data-grid'
import { useNavigate } from 'react-router-dom'
import { DataTable } from '../../../components/data/DataTable'
import { StatusBadge } from '../../../components/StatusBadge'
import { PageHeader } from '../../../components/PageHeader'
import { EmptyState } from '../../../components/EmptyState'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { listAssistants } from '../../assistants/api'
import { createKnowledgeBase, deleteKnowledgeBase, listKnowledgeBases } from '../api'
import { CreateEditKnowledgeBaseDialog, type KnowledgeBaseFormValues } from '../dialogs/CreateEditKnowledgeBaseDialog'
import type { KnowledgeBaseDto } from '../types'

const QUERY_KEY = 'knowledgebases'

export function KnowledgeLibraryPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [deleting, setDeleting] = useState<KnowledgeBaseDto | null>(null)

  const query = useQuery({
    queryKey: [QUERY_KEY, page, pageSize, search],
    queryFn: () => listKnowledgeBases(page, pageSize, search),
  })

  // No backend endpoint reports which assistants use a knowledge base — it's
  // a one-directional FK (AssistantDefinition.KnowledgeBaseId) with no
  // reverse index. Derived client-side from the assistants list instead.
  const assistantsQuery = useQuery({
    queryKey: queryKeys.assistants.forUsageCount,
    queryFn: () => listAssistants(1, 200, ''),
  })
  const usageByKbId = new Map<string, number>()
  for (const a of assistantsQuery.data?.items ?? []) {
    if (a.knowledgeBaseId) usageByKbId.set(a.knowledgeBaseId, (usageByKbId.get(a.knowledgeBaseId) ?? 0) + 1)
  }
  const assistantsTotal = assistantsQuery.data?.totalCount ?? 0
  const assistantUsageUnavailable =
    assistantsQuery.isLoading || assistantsQuery.isError || assistantsTotal > (assistantsQuery.data?.items.length ?? 0)

  useEffect(() => {
    if (!query.isSuccess) return
    const pageCount = Math.ceil((query.data?.totalCount ?? 0) / pageSize)
    if (page > pageCount) setPage(Math.max(1, pageCount))
  }, [page, pageSize, query.data?.totalCount, query.isSuccess])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })

  const createMutation = useMutation({
    mutationFn: createKnowledgeBase,
    onSuccess: (kb) => {
      notify('Knowledge base created', 'success')
      setCreateOpen(false)
      void invalidate()
      navigate(`/knowledge/${kb.id}`)
    },
    onError: () => notify('Failed to create knowledge base', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteKnowledgeBase,
    onSuccess: () => {
      notify('Knowledge base deleted', 'success')
      setDeleting(null)
      void invalidate()
    },
    onError: () => notify('Failed to delete knowledge base', 'error'),
  })

  function handleCreate(values: KnowledgeBaseFormValues) {
    createMutation.mutate(values)
  }

  const columns: GridColDef<KnowledgeBaseDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    {
      field: 'sources',
      headerName: 'Sources',
      width: 100,
      valueGetter: (_value, row) => row.sources.length,
    },
    { field: 'documentCount', headerName: 'Items', width: 90 },
    {
      field: 'usedBy',
      headerName: 'Used By',
      width: 130,
      sortable: false,
      description: 'Assistant usage is unavailable if the assistant list could not be loaded completely.',
      valueGetter: (_value, row) => assistantUsageUnavailable ? null : usageByKbId.get(row.id) ?? 0,
      renderCell: (params) =>
        params.value == null
          ? 'Unavailable'
          : params.value > 0
            ? `${params.value} assistant${params.value === 1 ? '' : 's'}`
            : 'None',
    },
    {
      field: 'status',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <StatusBadge status={params.value} toneMap={{ Creating: 'warning' }} />,
    },
    {
      field: 'actions',
      headerName: '',
      width: 70,
      sortable: false,
      renderCell: (params) => (
        <IconButton
          size="small"
          aria-label="Delete"
          onClick={(e) => {
            e.stopPropagation()
            setDeleting(params.row)
          }}
        >
          <DeleteIcon fontSize="small" />
        </IconButton>
      ),
    },
  ]

  return (
    <Box>
      <PageHeader
        title="Knowledge"
        description="Documents and sources assistants search to ground their answers."
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            New Knowledge Base
          </Button>
        }
      />

      {!query.isLoading && !query.isError && !search && (query.data?.totalCount ?? 0) === 0 ? (
        <Paper variant="outlined">
          <EmptyState
            title="No knowledge bases yet"
            description="Create a knowledge base to organize trusted sources for your assistants."
            actionLabel="New Knowledge Base"
            onAction={() => setCreateOpen(true)}
          />
        </Paper>
      ) : (
      <DataTable
        rows={query.data?.items ?? []}
        columns={columns}
        loading={query.isLoading}
        error={query.error}
        onRetry={() => void query.refetch()}
        rowCount={query.data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        search={search}
        onSearchChange={(v) => {
          setSearch(v)
          setPage(1)
        }}
        searchPlaceholder="Search knowledge bases…"
        onRowClick={(id) => navigate(`/knowledge/${id}`)}
      />
      )}

      <CreateEditKnowledgeBaseDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={handleCreate}
        isSubmitting={createMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete Knowledge Base"
        message={`Delete "${deleting?.name}"? This removes all its sources and documents. This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
