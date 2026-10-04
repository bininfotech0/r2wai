import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Chip, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import type { GridColDef } from '@mui/x-data-grid'
import { useNavigate } from 'react-router-dom'
import { DataTable } from '../../../components/data/DataTable'
import { EmptyState } from '../../../components/EmptyState'
import { PageHeader } from '../../../components/PageHeader'
import { StatusBadge } from '../../../components/StatusBadge'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createCapability, listCapabilities } from '../api'
import { CreateEditCapabilityDialog } from '../dialogs/CreateEditCapabilityDialog'
import type { CapabilityDto } from '../types'

const RISK_COLOR: Record<string, 'default' | 'warning' | 'error'> = { Low: 'default', Medium: 'default', High: 'warning', Critical: 'error' }

export function ToolsListPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)

  const query = useQuery({ queryKey: ['capabilities', page, search], queryFn: () => listCapabilities(page, 20, search) })

  const createMutation = useMutation({
    mutationFn: createCapability,
    onSuccess: (tool) => {
      notify('Tool created', 'success')
      setCreateOpen(false)
      void queryClient.invalidateQueries({ queryKey: ['capabilities'] })
      navigate(`/tools/${tool.id}`)
    },
    onError: () => notify('Failed to create tool', 'error'),
  })

  const columns: GridColDef<CapabilityDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'description', headerName: 'Purpose', flex: 1.2, valueGetter: (value) => value || '—' },
    { field: 'httpMethod', headerName: 'Method', width: 90 },
    { field: 'endpointPath', headerName: 'Path', flex: 1 },
    {
      field: 'applicationApiName',
      headerName: 'Integration',
      flex: 0.8,
      renderCell: (params) =>
        params.value ? (
          <Chip label={params.value} size="small" variant="outlined" />
        ) : (
          <Typography variant="body2" color="text.secondary">
            Standalone
          </Typography>
        ),
    },
    {
      field: 'riskLevel',
      headerName: 'Risk',
      width: 110,
      renderCell: (params) => <Chip label={params.value} size="small" color={RISK_COLOR[params.value] ?? 'default'} variant="outlined" />,
    },
    {
      field: 'isActive',
      headerName: 'Status',
      width: 100,
      renderCell: (params) => <StatusBadge status={params.value ? 'Active' : 'Inactive'} />,
    },
  ]

  return (
    <Box>
      <PageHeader
        title="Tools & APIs"
        description="Governed operations assistants can call — risk level, approval, and audit are enforced server-side."
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            New Tool
          </Button>
        }
      />

      <DataTable
        rows={query.data?.items ?? []}
        columns={columns}
        loading={query.isLoading}
        error={query.error}
        onRetry={() => void query.refetch()}
        rowCount={query.data?.totalCount ?? 0}
        page={page}
        pageSize={20}
        onPageChange={setPage}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1) }}
        searchPlaceholder="Search tools…"
        onRowClick={(id) => navigate(`/tools/${id}`)}
        emptyState={
          search.trim() ? (
            <EmptyState
              title="No tools match this search"
              description="Try another term, or clear the search to see all tools."
              actionLabel="Clear search"
              onAction={() => {
                setSearch('')
                setPage(1)
              }}
            />
          ) : (
            <EmptyState
              title="No tools yet"
              description="Create a tool to expose a governed operation to assistants."
              actionLabel="Create tool"
              onAction={() => setCreateOpen(true)}
            />
          )
        }
      />

      <CreateEditCapabilityDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(values) => createMutation.mutate(values)}
        isSubmitting={createMutation.isPending}
      />
    </Box>
  )
}
