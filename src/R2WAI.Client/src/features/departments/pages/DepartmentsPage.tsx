import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Stack } from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { PageHeader } from '../../../components/PageHeader'
import { StatusBadge } from '../../../components/StatusBadge'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createDepartment, deleteDepartment, listDepartments, updateDepartment } from '../api'
import { CreateEditDepartmentDialog, type DepartmentFormValues } from '../dialogs/CreateEditDepartmentDialog'
import type { DepartmentDto } from '../types'

const QUERY_KEY = 'departments'

export function DepartmentsPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<DepartmentDto | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [deleting, setDeleting] = useState<DepartmentDto | null>(null)

  const query = useQuery({
    queryKey: [QUERY_KEY, page, pageSize, search],
    queryFn: () => listDepartments(page, pageSize, search),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })

  const createMutation = useMutation({
    mutationFn: createDepartment,
    onSuccess: () => {
      notify('Department created', 'success')
      setDialogOpen(false)
      void invalidate()
    },
    onError: () => notify('Failed to create department', 'error'),
  })

  const updateMutation = useMutation({
    mutationFn: (input: { id: string; values: DepartmentFormValues }) => updateDepartment(input.id, input.values),
    onSuccess: () => {
      notify('Department updated', 'success')
      setDialogOpen(false)
      setEditing(null)
      void invalidate()
    },
    onError: () => notify('Failed to update department', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteDepartment,
    onSuccess: () => {
      notify('Department deleted', 'success')
      setDeleting(null)
      void invalidate()
    },
    onError: () => notify('Failed to delete department', 'error'),
  })

  function handleSubmit(values: DepartmentFormValues) {
    if (editing) {
      updateMutation.mutate({ id: editing.id, values })
    } else {
      createMutation.mutate(values)
    }
  }

  const columns: GridColDef<DepartmentDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'code', headerName: 'Code', width: 120 },
    { field: 'description', headerName: 'Description', flex: 1.5 },
    {
      field: 'isActive',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <StatusBadge status={params.value ? 'Active' : 'Inactive'} />,
    },
    {
      field: 'actions',
      headerName: '',
      width: 100,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <IconButton
            size="small"
            aria-label="Edit"
            onClick={(e) => {
              e.stopPropagation()
              setEditing(params.row)
              setDialogOpen(true)
            }}
          >
            <EditIcon fontSize="small" />
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
      <PageHeader
        title="Departments"
        description="Optional organizational labels for grouping connected systems. New connections do not require a department."
        actions={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => {
              setEditing(null)
              setDialogOpen(true)
            }}
          >
            New Department
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
        searchPlaceholder="Search departments…"
        emptyState={
          search.trim() ? (
            <EmptyState
              title="No departments match this search"
              description="Try a different term, or clear the search to see all departments."
              actionLabel="Clear search"
              onAction={() => {
                setSearch('')
                setPage(1)
              }}
            />
          ) : (
            <EmptyState
              title="No departments yet"
              description="You can add an optional department label when organizing existing connected systems."
              actionLabel="Create department"
              onAction={() => {
                setEditing(null)
                setDialogOpen(true)
              }}
            />
          )
        }
      />

      <CreateEditDepartmentDialog
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false)
          setEditing(null)
        }}
        onSubmit={handleSubmit}
        department={editing}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete Department"
        message={`Delete "${deleting?.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
