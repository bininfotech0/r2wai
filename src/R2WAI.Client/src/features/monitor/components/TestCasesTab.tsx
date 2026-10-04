import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Chip, Stack } from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import AddIcon from '@mui/icons-material/Add'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import DeleteIcon from '@mui/icons-material/Delete'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createTestCase, deleteTestCase, listTestCases, runAllTestCases, runTestCase, updateTestCase } from '../api'
import type { TestCaseDto } from '../types'
import { CreateEditTestCaseDialog } from './CreateEditTestCaseDialog'

const QUERY_KEY = 'monitor-testcases'

export function TestCasesTab() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [page, setPage] = useState(1)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<TestCaseDto | null>(null)
  const [deleting, setDeleting] = useState<TestCaseDto | null>(null)

  const query = useQuery({ queryKey: [QUERY_KEY, page], queryFn: () => listTestCases(page, 20) })
  const invalidate = () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })

  const createMutation = useMutation({
    mutationFn: createTestCase,
    onSuccess: () => {
      notify('Test case created', 'success')
      setDialogOpen(false)
      void invalidate()
    },
    onError: () => notify('Failed to create test case', 'error'),
  })
  const updateMutation = useMutation({
    mutationFn: (input: { id: string; values: Parameters<typeof updateTestCase>[1] }) => updateTestCase(input.id, input.values),
    onSuccess: () => {
      notify('Test case updated', 'success')
      setDialogOpen(false)
      setEditing(null)
      void invalidate()
    },
    onError: () => notify('Failed to update test case', 'error'),
  })
  const deleteMutation = useMutation({
    mutationFn: deleteTestCase,
    onSuccess: () => {
      notify('Test case deleted', 'success')
      setDeleting(null)
      void invalidate()
    },
    onError: () => notify('Failed to delete test case', 'error'),
  })
  const runMutation = useMutation({
    mutationFn: runTestCase,
    onSuccess: (run) => notify(`Run complete — ${run.passedCount} passed, ${run.failedCount} failed`, run.failedCount > 0 ? 'warning' : 'success'),
    onError: () => notify('Run failed to start', 'error'),
  })
  const runAllMutation = useMutation({
    mutationFn: () => runAllTestCases(),
    onSuccess: (run) => notify(`Batch run complete — ${run.passedCount} passed, ${run.failedCount} failed`, run.failedCount > 0 ? 'warning' : 'success'),
    onError: () => notify('Batch run failed to start', 'error'),
  })

  const columns: GridColDef<TestCaseDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'question', headerName: 'Question', flex: 1.5 },
    {
      field: 'isEnabled',
      headerName: 'Enabled',
      width: 100,
      renderCell: (params) => (
        <Chip label={params.value ? 'Yes' : 'No'} size="small" color={params.value ? 'success' : 'default'} variant="outlined" />
      ),
    },
    {
      field: 'actions',
      headerName: '',
      width: 130,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <IconButton
            size="small"
            aria-label="Run"
            onClick={(e) => {
              e.stopPropagation()
              runMutation.mutate(params.row.id)
            }}
          >
            <PlayArrowIcon fontSize="small" />
          </IconButton>
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
        </Stack>
      ),
    },
  ]

  return (
    <Box>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Button size="small" variant="contained" startIcon={<AddIcon />} onClick={() => { setEditing(null); setDialogOpen(true) }}>
          New Test Case
        </Button>
        <Button size="small" startIcon={<PlayArrowIcon />} onClick={() => runAllMutation.mutate()} disabled={runAllMutation.isPending}>
          {runAllMutation.isPending ? 'Running…' : 'Run All'}
        </Button>
      </Stack>

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
        onRowClick={(id) => {
          const tc = query.data?.items.find((t) => t.id === id)
          if (tc) {
            setEditing(tc)
            setDialogOpen(true)
          }
        }}
      />

      <CreateEditTestCaseDialog
        open={dialogOpen}
        onClose={() => { setDialogOpen(false); setEditing(null) }}
        onSubmit={(values) => {
          if (editing) {
            updateMutation.mutate({ id: editing.id, values })
          } else {
            createMutation.mutate(values)
          }
        }}
        testCase={editing}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete Test Case"
        message={`Delete "${deleting?.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
