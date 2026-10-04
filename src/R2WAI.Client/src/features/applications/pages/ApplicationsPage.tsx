import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Chip } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import HubOutlinedIcon from '@mui/icons-material/HubOutlined'
import type { GridColDef } from '@mui/x-data-grid'
import { useNavigate } from 'react-router-dom'
import { DataTable } from '../../../components/data/DataTable'
import { EmptyState } from '../../../components/EmptyState'
import { PageHeader } from '../../../components/PageHeader'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createApplication, listApplications } from '../api'
import { CreateApplicationDialog } from '../dialogs/CreateApplicationDialog'
import type { ApplicationDto } from '../types'

const QUERY_KEY = 'applications'

const STATUS_COLOR: Record<string, 'success' | 'warning' | 'default' | 'error'> = {
  Published: 'success',
  Testing: 'warning',
  Configuring: 'warning',
  Discovering: 'warning',
  Draft: 'default',
  Disabled: 'error',
  Archived: 'default',
}

export function ApplicationsPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)

  const query = useQuery({
    queryKey: [QUERY_KEY, page, pageSize, search],
    queryFn: () => listApplications(page, pageSize, search),
  })

  const createMutation = useMutation({
    mutationFn: createApplication,
    onSuccess: (app) => {
      notify('Connected system added', 'success')
      setCreateOpen(false)
      void queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
      navigate(`/workspaces/${app.id}`)
    },
    onError: () => notify('Failed to add connected system', 'error'),
  })

  const columns: GridColDef<ApplicationDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'code', headerName: 'Code', width: 120 },
    { field: 'environment', headerName: 'Environment', width: 130 },
    {
      field: 'status',
      headerName: 'Status',
      width: 130,
      renderCell: (params) => (
        <Chip label={params.value} size="small" color={STATUS_COLOR[params.value] ?? 'default'} variant="outlined" />
      ),
    },
  ]

  return (
    <Box>
      <PageHeader
        title="Connections"
        description="Connect external systems, then configure their APIs and authentication. Attach each connection to the agents and tools that need it."
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            Add Connected System
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
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        search={search}
        onSearchChange={(v) => {
          setSearch(v)
          setPage(1)
        }}
        searchPlaceholder="Search connected systems…"
        emptyState={
          <EmptyState
            icon={HubOutlinedIcon}
            title={search ? 'No connections match your search' : 'No connected systems yet'}
            description={search ? 'Try a different name or code.' : 'Add a connected system to configure its APIs and credentials for your agents and tools.'}
            actionLabel={search ? undefined : 'Add Connected System'}
            onAction={search ? undefined : () => setCreateOpen(true)}
          />
        }
        onRowClick={(id) => navigate(`/workspaces/${id}`)}
      />

      <CreateApplicationDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(values) => createMutation.mutate(values)}
        isSubmitting={createMutation.isPending}
      />
    </Box>
  )
}
