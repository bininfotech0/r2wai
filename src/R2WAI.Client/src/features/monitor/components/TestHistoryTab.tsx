import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Box, Chip } from '@mui/material'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { getTestRun, listTestRuns } from '../api'
import type { TestRunDto } from '../types'
import { TestRunDetailDialog } from './TestRunDetailDialog'

const STATUS_COLOR: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Completed: 'success',
  Running: 'warning',
  Failed: 'error',
}

export function TestHistoryTab() {
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const query = useQuery({ queryKey: ['monitor-testruns', page], queryFn: () => listTestRuns(page, 20) })

  // The list endpoint doesn't include each run's Results (no .Include on the
  // repo query, reasonable for a list view) — fetch the full detail
  // separately when a row is opened rather than reusing the summary row.
  const detailQuery = useQuery({
    queryKey: ['monitor-testrun-detail', selectedId],
    queryFn: () => getTestRun(selectedId!),
    enabled: !!selectedId,
  })

  const columns: GridColDef<TestRunDto>[] = [
    { field: 'startedAt', headerName: 'Started', width: 180, valueFormatter: (v: string) => new Date(v).toLocaleString() },
    {
      field: 'status',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <Chip label={params.value} size="small" color={STATUS_COLOR[params.value] ?? 'default'} variant="outlined" />,
    },
    { field: 'passedCount', headerName: 'Passed', width: 90 },
    { field: 'failedCount', headerName: 'Failed', width: 90 },
    { field: 'warningCount', headerName: 'Warnings', width: 100 },
  ]

  return (
    <Box>
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
        onRowClick={(id) => setSelectedId(id)}
      />

      <TestRunDetailDialog run={selectedId ? (detailQuery.data ?? null) : null} onClose={() => setSelectedId(null)} />
    </Box>
  )
}
