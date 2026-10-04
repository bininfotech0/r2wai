import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, Box, Button, Chip, Stack, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import PlayCircleOutlineOutlined from '@mui/icons-material/PlayCircleOutlineOutlined'
import TodayOutlined from '@mui/icons-material/TodayOutlined'
import { useNavigate } from 'react-router-dom'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { EmptyState } from '../../../components/EmptyState'
import { StatCard } from '../../../components/StatCard'
import { fetchJson } from '../../../lib/api/fetchJson'
import { listWorkflows } from '../api'
import { CreateAutomationDialog } from '../dialogs/CreateAutomationDialog'
import type { WorkflowDto } from '../types'
import type { MetricsDto } from '../../dashboard/types'

export function AutomationsListPage() {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)

  const query = useQuery({
    queryKey: ['workflows', page, pageSize, search],
    queryFn: () => listWorkflows(page, pageSize, search),
  })

  const metricsQuery = useQuery({
    queryKey: ['operations', 'metrics'],
    queryFn: () => fetchJson<MetricsDto>('/operations/metrics'),
  })

  const columns: GridColDef<WorkflowDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'trigger', headerName: 'Trigger', width: 160, valueGetter: (_, row) => row.trigger ?? '—' },
    {
      field: 'versionStatus',
      headerName: 'Status',
      width: 130,
      renderCell: (params) => (
        <Chip
          label={params.value}
          size="small"
          color={params.value === 'Published' ? 'success' : params.value === 'Archived' ? 'default' : 'warning'}
          variant="outlined"
        />
      ),
    },
    { field: 'version', headerName: 'Version', width: 100 },
  ]

  return (
    <Box>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { xs: 'stretch', sm: 'center' }, justifyContent: 'space-between', mb: 2 }}
      >
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Automations
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Automate repeatable work across your enterprise systems.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)} sx={{ alignSelf: { xs: 'flex-start', sm: 'auto' } }}>
          New Automation
        </Button>
      </Stack>

      {metricsQuery.isError ? (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => void metricsQuery.refetch()}>
              Retry
            </Button>
          }
        >
          Automation metrics could not be loaded.
        </Alert>
      ) : (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(3, minmax(0, 1fr))' },
            gap: 2,
            mb: 2,
          }}
        >
          <StatCard
            label="Total"
            value={metricsQuery.data?.totalWorkflows ?? 0}
            icon={AccountTreeOutlined}
            color="primary"
            loading={metricsQuery.isLoading}
          />
          <StatCard
            label="Active"
            value={metricsQuery.data?.activeWorkflows ?? 0}
            icon={PlayCircleOutlineOutlined}
            color="success"
            loading={metricsQuery.isLoading}
          />
          <StatCard
            label="Runs today"
            value={metricsQuery.data?.completedToday ?? 0}
            icon={TodayOutlined}
            color="info"
            loading={metricsQuery.isLoading}
          />
        </Box>
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
    </Box>
  )
}
