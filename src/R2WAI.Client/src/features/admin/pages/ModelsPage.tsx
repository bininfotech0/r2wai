import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Chip, Paper, Stack, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { PageHeader } from '../../../components/PageHeader'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createModel, deleteModel, listModelsPage, testModelConnection, updateModel } from '../api'
import { CreateEditModelDialog } from '../dialogs/CreateEditModelDialog'
import { MODEL_PROVIDERS, type ModelConfigDto, type ModelFormInput } from '../types'

const CLASSIFICATION_COLOR: Record<string, 'default' | 'warning' | 'error'> = {
  Public: 'default',
  Internal: 'default',
  Confidential: 'warning',
  Restricted: 'error',
}

export function ModelsPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<ModelConfigDto | null>(null)
  const [deleting, setDeleting] = useState<ModelConfigDto | null>(null)
  const [testResult, setTestResult] = useState<{ id: string; success: boolean; message: string } | null>(null)

  const query = useQuery({
    queryKey: ['admin-models', page, pageSize, search],
    queryFn: () => listModelsPage(page, pageSize, search),
  })
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin-models'] })

  const createMutation = useMutation({
    mutationFn: (v: ModelFormInput) => createModel(v),
    onSuccess: () => {
      notify('Model created', 'success')
      setDialogOpen(false)
      void invalidate()
    },
    onError: () => notify('Failed to create model', 'error'),
  })
  const updateMutation = useMutation({
    mutationFn: (input: { id: string; values: ModelFormInput }) => updateModel(input.id, input.values),
    onSuccess: () => {
      notify('Model updated', 'success')
      setDialogOpen(false)
      setEditing(null)
      void invalidate()
    },
    onError: () => notify('Failed to update model', 'error'),
  })
  const deleteMutation = useMutation({
    mutationFn: deleteModel,
    onSuccess: () => {
      notify('Model deleted', 'success')
      setDeleting(null)
      void invalidate()
    },
    onError: () => notify('Failed to delete model', 'error'),
  })
  const testMutation = useMutation({
    mutationFn: testModelConnection,
    onSuccess: (result, id) => setTestResult({ id, ...result }),
    onError: (err, id) =>
      setTestResult({ id, success: false, message: err instanceof Error ? err.message : 'Test request failed.' }),
  })

  const columns: GridColDef<ModelConfigDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'provider', headerName: 'Provider', width: 130 },
    { field: 'modelId', headerName: 'Model ID', flex: 1 },
    {
      field: 'dataClassification',
      headerName: 'Classification',
      width: 140,
      renderCell: (params) => <Chip label={params.value} size="small" color={CLASSIFICATION_COLOR[params.value] ?? 'default'} variant="outlined" />,
    },
    {
      field: 'isDefault',
      headerName: 'Default',
      width: 90,
      renderCell: (params) => (params.value ? <Chip label="Default" size="small" color="primary" variant="outlined" /> : null),
    },
    {
      field: 'actions',
      headerName: '',
      width: 130,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <IconButton size="small" aria-label="Test connection" onClick={(e) => { e.stopPropagation(); testMutation.mutate(params.row.id) }}>
            <PlayArrowIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" aria-label="Edit" onClick={(e) => { e.stopPropagation(); setEditing(params.row); setDialogOpen(true) }}>
            <EditIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" aria-label="Delete" onClick={(e) => { e.stopPropagation(); setDeleting(params.row) }}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Stack>
      ),
    },
  ]

  return (
    <Box>
      <PageHeader
        title="AI Models"
        description="Model configurations assistants and automations reference — each is a single provider + model, not a profile with fallback/routing."
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditing(null); setDialogOpen(true) }}>
            New Model
          </Button>
        }
      />

      {testResult && (
        <Alert severity={testResult.success ? 'success' : 'warning'} sx={{ mb: 2 }} onClose={() => setTestResult(null)}>
          {testResult.message}
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
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={(size) => {
          setPageSize(size)
          setPage(1)
        }}
        search={search}
        onSearchChange={(value) => {
          setSearch(value)
          setPage(1)
        }}
        searchPlaceholder="Search model names…"
        emptyState={
          search.trim() ? (
            <EmptyState
              title="No models match this search"
              description="Try another model name or clear the search to see all configured models."
              actionLabel="Clear search"
              onAction={() => {
                setSearch('')
                setPage(1)
              }}
            />
          ) : (
            <EmptyState
              title="No AI models configured"
              description="Add a provider and model configuration before assigning a model to an assistant or automation."
              actionLabel="Add model"
              onAction={() => {
                setEditing(null)
                setDialogOpen(true)
              }}
            />
          )
        }
      />

      <Paper variant="outlined" sx={{ p: 2, mt: 2 }}>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          Known Providers
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          A static reference — there's no live provider-health endpoint, this is what "Test connection" knows how to
          reach.
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
          {MODEL_PROVIDERS.map((p) => (
            <Chip key={p} label={p} size="small" variant="outlined" />
          ))}
        </Stack>
      </Paper>

      <CreateEditModelDialog
        open={dialogOpen}
        onClose={() => { setDialogOpen(false); setEditing(null) }}
        onSubmit={(values) => {
          if (editing) {
            updateMutation.mutate({ id: editing.id, values })
          } else {
            createMutation.mutate(values)
          }
        }}
        model={editing}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete Model"
        message={`Delete "${deleting?.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
