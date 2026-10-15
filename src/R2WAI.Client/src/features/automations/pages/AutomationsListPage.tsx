import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Stack, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import { useNavigate } from 'react-router-dom'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { NameCell } from '../../../components/data/NameCell'
import { RowActionsMenu } from '../../../components/data/RowActionsMenu'
import { EmptyState } from '../../../components/EmptyState'
import { StatusBadge } from '../../../components/StatusBadge'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { fetchJson } from '../../../lib/api/fetchJson'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { deleteWorkflow, listWorkflows } from '../api'
import { CreateAutomationDialog } from '../dialogs/CreateAutomationDialog'
import type { WorkflowDto } from '../types'
import { describeTrigger } from '../trigger'
import type { MetricsDto } from '../../dashboard/types'
import { PageHeader } from '../../../components/PageHeader'

export function AutomationsListPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [deleting, setDeleting] = useState<WorkflowDto | null>(null)

  const query = useQuery({
    queryKey: ['workflows', page, pageSize, search],
    queryFn: () => listWorkflows(page, pageSize, search),
  })

  // A one-line summary instead of stat tiles (Copilot Studio list pages carry none); the full
  // figures live on Activity → Monitor.
  const metricsQuery = useQuery({
    queryKey: ['operations', 'metrics'],
    queryFn: () => fetchJson<MetricsDto>('/operations/metrics'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWorkflow(id),
    onSuccess: () => {
      notify('Automation deleted', 'success')
      setDeleting(null)
      void queryClient.invalidateQueries({ queryKey: ['workflows'] })
    },
    onError: () => notify('Failed to delete automation', 'error'),
  })

  const columns: GridColDef<WorkflowDto>[] = [
    {
      field: 'name',
      headerName: 'Name',
      flex: 1,
      minWidth: 240,
      renderCell: ({ row }) => <NameCell icon={AccountTreeOutlined} name={row.name} secondary={row.description || 'No description'} />,
    },
    {
      field: 'versionStatus',
      headerName: 'Status',
      width: 130,
      renderCell: (params) => <StatusBadge status={params.value} />,
    },
    { field: 'trigger', headerName: 'Trigger', width: 200, valueGetter: (_, row) => describeTrigger(row.trigger) },
    { field: 'version', headerName: 'Version', width: 100, valueFormatter: (v: number) => `v${v}` },
    {
      field: 'modifiedAt',
      headerName: 'Modified',
      width: 130,
      valueGetter: (_, row) => row.modifiedAt ?? row.createdAt,
      valueFormatter: (v: string) => new Date(v).toLocaleDateString(),
    },
    {
      field: 'actions',
      headerName: '',
      width: 56,
      sortable: false,
      disableColumnMenu: true,
      renderCell: ({ row }) => (
        <RowActionsMenu
          label={row.name}
          actions={[
            { label: 'Open', onSelect: () => navigate(`/automations/${row.id}`) },
            { label: 'Open builder', onSelect: () => navigate(`/automations/${row.id}/builder`) },
            { label: 'Delete', destructive: true, onSelect: () => setDeleting(row) },
          ]}
        />
      ),
    },
  ]

  const metrics = metricsQuery.data

  return (
    <Box>
      <PageHeader
        title="Automations"
        description="Multi-step flows that run on a trigger or when an agent calls them, across your connected systems."
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            New Automation
          </Button>
        }
      />

      {metrics && (
        <Stack direction="row" spacing={2} sx={{ mb: 1.5, color: 'text.secondary', flexWrap: 'wrap' }} aria-label="Automation summary">
          <Typography variant="body2"><strong>{metrics.totalWorkflows}</strong> total</Typography>
          <Typography variant="body2"><strong>{metrics.activeWorkflows}</strong> runs in progress</Typography>
          <Typography variant="body2"><strong>{metrics.completedToday}</strong> runs today</Typography>
        </Stack>
      )}

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
        onPageSizeChange={(nextPageSize) => {
          setPageSize(nextPageSize)
          setPage(1)
        }}
        search={search}
        onSearchChange={(v) => {
          setSearch(v)
          setPage(1)
        }}
        searchPlaceholder="Search automations…"
        emptyState={
          search.trim() ? (
            <EmptyState
              icon={AccountTreeOutlined}
              title="No automations match this search"
              description="Try another name or clear the search to see all automations."
              actionLabel="Clear search"
              onAction={() => {
                setSearch('')
                setPage(1)
              }}
            />
          ) : (
            <EmptyState
              icon={AccountTreeOutlined}
              title="No automations yet"
              description="Build an automation to coordinate repeatable work across your connected systems."
              actionLabel="Create automation"
              onAction={() => setCreateOpen(true)}
            />
          )
        }
        onRowClick={(id) => navigate(`/automations/${id}`)}
      />

      <CreateAutomationDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(id) => {
          setCreateOpen(false)
          navigate(`/automations/${id}`)
        }}
      />

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete automation"
        message={`Delete "${deleting?.name}"? Its run history stays in Activity. This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
