import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material'
import DownloadIcon from '@mui/icons-material/Download'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { downloadAuditLogsExport, listAuditLogs, type AuditLogFilters } from '../api'
import { downloadBlob } from '../downloadBlob'
import type { AuditLogDto } from '../types'

const ACTIONS = ['Create', 'Update', 'Delete', 'Login', 'Logout', 'Export', 'View', 'Execute']

interface AuditLogsTabProps {
  initialEntityType?: string
  initialEntityId?: string
}

export function AuditLogsTab({ initialEntityType, initialEntityId }: AuditLogsTabProps) {
  const [entityType, setEntityType] = useState(initialEntityType ?? '')
  const [action, setAction] = useState('')
  const [page, setPage] = useState(1)

  useEffect(() => {
    if (initialEntityType) setEntityType(initialEntityType)
  }, [initialEntityType])

  const filters: AuditLogFilters = { entityType: entityType || undefined, action: action || undefined }
  const query = useQuery({
    queryKey: ['monitor', 'audit-logs', filters, page],
    queryFn: () => listAuditLogs(filters, page, 25),
  })

  const rows = initialEntityId
    ? (query.data?.items ?? []).filter((a) => a.entityId === initialEntityId)
    : (query.data?.items ?? [])

  async function handleExport(format: 'csv' | 'json') {
    const blob = await downloadAuditLogsExport(format, filters)
    downloadBlob(blob, `audit-logs.${format}`)
  }

  const columns: GridColDef<AuditLogDto>[] = [
    { field: 'timestamp', headerName: 'Time', width: 180, valueFormatter: (v: string) => new Date(v).toLocaleString() },
    { field: 'action', headerName: 'Action', width: 100 },
    { field: 'entityType', headerName: 'Entity', width: 160 },
    { field: 'entityId', headerName: 'Entity ID', width: 120, valueFormatter: (v: string | null) => (v ? `${v.slice(0, 8)}…` : '—') },
    { field: 'userName', headerName: 'User', flex: 1, valueGetter: (v: string | null) => v ?? 'System' },
    { field: 'correlationId', headerName: 'Correlation ID', width: 130, valueFormatter: (v: string | null) => (v ? `${v.slice(0, 8)}…` : '—') },
  ]

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
        <TextField
          size="small"
          label="Entity type"
          placeholder="e.g. WorkflowInstance"
          value={entityType}
          onChange={(e) => {
            setEntityType(e.target.value)
            setPage(1)
          }}
          sx={{ minWidth: 200 }}
        />
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="audit-action-label">Action</InputLabel>
          <Select
            labelId="audit-action-label"
            label="Action"
            value={action}
            onChange={(e) => {
              setAction(e.target.value)
              setPage(1)
            }}
          >
            <MenuItem value="">All</MenuItem>
            {ACTIONS.map((a) => (
              <MenuItem key={a} value={a}>
                {a}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <Stack direction="row" spacing={1} sx={{ ml: 'auto' }}>
          <Button size="small" startIcon={<DownloadIcon />} onClick={() => void handleExport('csv')}>
            CSV
          </Button>
          <Button size="small" startIcon={<DownloadIcon />} onClick={() => void handleExport('json')}>
            JSON
          </Button>
        </Stack>
      </Stack>

      <DataTable
        rows={rows}
        columns={columns}
        loading={query.isLoading}
        error={query.error}
        onRetry={() => void query.refetch()}
        rowCount={initialEntityId ? rows.length : (query.data?.totalCount ?? 0)}
        page={page}
        pageSize={25}
        onPageChange={setPage}
      />
    </Stack>
  )
}
