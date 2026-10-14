import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, Box, Button, FormControlLabel, Switch, Tooltip } from '@mui/material'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { NameCell } from '../../../components/data/NameCell'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import BuildOutlined from '@mui/icons-material/BuildOutlined'
import { FilterBar, type FilterBarField, type FilterBarOption } from '../../../components/FilterBar'
import { PageHeader } from '../../../components/PageHeader'
import { StatusBadge } from '../../../components/StatusBadge'
import { getRunsFilters, listRuns } from '../api'
import { RunInspectorDrawer } from '../components/RunInspectorDrawer'
import type { RunItem } from '../types'

const LIVE_POLL_MS = 10_000
const PAGE_SIZE = 25
const TYPE_OPTIONS: FilterBarOption[] = [
  { value: 'all', label: 'All types' },
  { value: 'Automation', label: 'Automation' },
  { value: 'Assistant', label: 'Agent' },
  { value: 'ToolCall', label: 'Tool call' },
]
const STATUS_OPTIONS: FilterBarOption[] = [
  { value: 'all', label: 'Any status' },
  ...['Running', 'Completed', 'Failed', 'Cancelled', 'Started', 'Active'].map((value) => ({ value, label: value })),
  // Tool-call (ToolExecution ledger) outcomes.
  { value: 'Succeeded', label: 'Succeeded' },
  { value: 'Denied', label: 'Denied' },
  { value: 'AwaitingApproval', label: 'Awaiting approval' },
  { value: 'Unknown', label: 'Unknown outcome' },
]
const DATE_RANGES = [
  { value: 'all', label: 'Any time' },
  { value: '24h', label: 'Last 24 hours' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
]

function durationLabel(startedAt: string, completedAt: string | null): string {
  if (!completedAt) return '—'
  const seconds = Math.max(0, Math.round((Date.parse(completedAt) - Date.parse(startedAt)) / 1000))
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  return `${minutes}m ${seconds % 60}s`
}

function dateRangeStart(range: string): string | undefined {
  const ageMs = range === '24h' ? 24 * 60 * 60 * 1000
    : range === '7d' ? 7 * 24 * 60 * 60 * 1000
      : range === '30d' ? 30 * 24 * 60 * 60 * 1000
        : 0
  return ageMs ? new Date(Date.now() - ageMs).toISOString() : undefined
}

export function RunsPage() {
  const [page, setPage] = useState(1)
  const [live, setLive] = useState(false)
  const [selected, setSelected] = useState<RunItem | null>(null)
  const [typeFilter, setTypeFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [applicationFilter, setApplicationFilter] = useState('all')
  const [userFilter, setUserFilter] = useState('all')
  const [assistantFilter, setAssistantFilter] = useState('all')
  const [dateFilter, setDateFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const from = useMemo(() => dateRangeStart(dateFilter), [dateFilter])

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search.trim()), 300)
    return () => window.clearTimeout(timeout)
  }, [search])

  const filtersQuery = useQuery({
    queryKey: ['runs', 'filters'],
    queryFn: getRunsFilters,
    staleTime: 5 * 60 * 1000,
  })

  const query = useQuery({
    queryKey: ['runs', page, PAGE_SIZE, typeFilter, statusFilter, applicationFilter, userFilter, assistantFilter, from, debouncedSearch],
    queryFn: () => listRuns({
      page,
      pageSize: PAGE_SIZE,
      type: typeFilter === 'all' ? undefined : typeFilter,
      status: statusFilter === 'all' ? undefined : statusFilter,
      applicationId: applicationFilter === 'all' ? undefined : applicationFilter,
      userId: userFilter === 'all' ? undefined : userFilter,
      assistantId: assistantFilter === 'all' ? undefined : assistantFilter,
      from,
      search: debouncedSearch || undefined,
    }),
    refetchInterval: live ? LIVE_POLL_MS : false,
  })

  const applications = filtersQuery.data?.applications ?? []
  const users = filtersQuery.data?.users ?? []
  const assistants = filtersQuery.data?.assistants ?? []
  const filters: FilterBarField[] = [
    { key: 'type', label: 'Type', value: typeFilter, options: TYPE_OPTIONS, onChange: (value) => { setTypeFilter(value); setPage(1) } },
    { key: 'status', label: 'Status', value: statusFilter, options: STATUS_OPTIONS, onChange: (value) => { setStatusFilter(value); setPage(1) } },
    { key: 'application', label: 'Application', value: applicationFilter, options: [{ value: 'all', label: 'Any application' }, ...applications.map((option) => ({ value: option.id, label: option.label }))], onChange: (value) => { setApplicationFilter(value); setPage(1) } },
    { key: 'assistant', label: 'Agent', value: assistantFilter, options: [{ value: 'all', label: 'Any agent' }, ...assistants.map((option) => ({ value: option.id, label: option.label }))], onChange: (value) => { setAssistantFilter(value); setPage(1) } },
    { key: 'user', label: 'User', value: userFilter, options: [{ value: 'all', label: 'Any user' }, ...users.map((option) => ({ value: option.id, label: option.label }))], onChange: (value) => { setUserFilter(value); setPage(1) } },
    { key: 'date', label: 'Started', value: dateFilter, options: DATE_RANGES, onChange: (value) => { setDateFilter(value); setPage(1) } },
  ]

  const columns: GridColDef<RunItem>[] = [
    {
      field: 'name',
      headerName: 'Name',
      flex: 1,
      minWidth: 240,
      renderCell: ({ row }) => (
        <Tooltip title={`Run ${row.id}`} placement="top-start" describeChild>
          <Box sx={{ height: '100%', minWidth: 0 }}>
            <NameCell
              icon={row.type === 'Automation' ? AccountTreeOutlined : row.type === 'ToolCall' ? BuildOutlined : SmartToyOutlined}
              name={row.name}
              secondary={`${row.type === 'ToolCall' ? 'Tool call' : row.type === 'Assistant' ? 'Agent' : row.type} · ${row.id.slice(0, 8)}`}
            />
          </Box>
        </Tooltip>
      ),
    },
    { field: 'applicationName', headerName: 'Application', width: 160, valueFormatter: (value: string | null) => value ?? '—' },
    { field: 'linkedName', headerName: 'Agent / tool', width: 180, valueFormatter: (value: string | null) => value ?? '—' },
    { field: 'status', headerName: 'Status', width: 130, renderCell: (params) => <StatusBadge status={params.value} /> },
    { field: 'startedAt', headerName: 'Started', width: 180, valueFormatter: (value: string) => new Date(value).toLocaleString() },
    { field: 'duration', headerName: 'Duration', width: 100, sortable: false, valueGetter: (_value, row) => durationLabel(row.startedAt, row.completedAt) },
    { field: 'userName', headerName: 'User', width: 150, valueFormatter: (value: string | null) => value ?? '—' },
  ]

  return (
    <Box>
      <PageHeader
        title="Activity"
        description="Every agent conversation, automation run and tool call, with what happened and who approved it."
        actions={<FormControlLabel control={<Switch checked={live} onChange={(e) => setLive(e.target.checked)} />} label="Live" />}
      />

      <FilterBar filters={filters} />

      {filtersQuery.isError && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={<Button color="inherit" size="small" onClick={() => void filtersQuery.refetch()}>Retry</Button>}
        >
          Filter options could not be loaded. You can still search runs and use the available filters.
        </Alert>
      )}

      <DataTable
        rows={query.data?.items ?? []}
        columns={columns}
        loading={query.isLoading}
        error={query.error}
        onRetry={() => void query.refetch()}
        rowCount={query.data?.totalCount ?? 0}
        page={page}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
        search={search}
        onSearchChange={(value) => { setSearch(value); setPage(1) }}
        searchPlaceholder="Search runs, agents, applications, users, correlation IDs…"
        onRowClick={(id) => setSelected(query.data?.items.find((run) => run.id === id) ?? null)}
      />

      <RunInspectorDrawer run={selected} onClose={() => setSelected(null)} />
    </Box>
  )
}
