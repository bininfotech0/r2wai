import { Box, InputAdornment, Paper, Stack, TextField } from '@mui/material'
import { TooltipIconButton as IconButton } from '../TooltipIconButton'
import SearchIcon from '@mui/icons-material/Search'
import ClearIcon from '@mui/icons-material/Close'
import { DataGrid, type GridColDef, type GridRowsProp } from '@mui/x-data-grid'
import { EmptyState } from '../EmptyState'
import { ErrorState } from '../ErrorState'
import { describeApiError } from '../../lib/api/fetchJson'

interface DataTableProps<T extends { id: string }> {
  rows: T[]
  columns: GridColDef<T>[]
  loading?: boolean
  rowCount?: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onPageSizeChange?: (pageSize: number) => void
  search?: string
  onSearchChange?: (search: string) => void
  searchPlaceholder?: string
  toolbarActions?: React.ReactNode
  emptyState?: React.ReactNode
  onRowClick?: (id: string) => void
  /** When set (truthy), renders ErrorState instead of the grid — pass the query's own `error`. */
  error?: unknown
  onRetry?: () => void
  /** Client pagination for screens that merge bounded results from multiple endpoints. */
  paginationMode?: 'server' | 'client'
}

/**
 * Generic list-page table — the reusable shape every Build/Manage list page
 * (Departments here, then Assistants/Automations/Knowledge/... in later
 * phases) is built on. Wraps MUI X DataGrid with the pagination/search
 * conventions the API's PagedResult<T> shape already provides.
 */
export function DataTable<T extends { id: string }>({
  rows,
  columns,
  loading,
  rowCount = rows.length,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  search,
  onSearchChange,
  searchPlaceholder = 'Search…',
  toolbarActions,
  emptyState,
  onRowClick,
  error,
  onRetry,
  paginationMode = 'server',
}: DataTableProps<T>) {
  const isEmpty = !loading && rows.length === 0
  const emptyContent = emptyState ?? (
    <EmptyState
      title="No results"
      description={search ? `Nothing matches "${search}".` : undefined}
    />
  )

  if (error) {
    const { title, description } = describeApiError(error)
    return (
      <Paper variant="outlined">
        <ErrorState title={title} description={description} onRetry={onRetry} />
      </Paper>
    )
  }

  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      {(onSearchChange || toolbarActions) && (
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1.5}
          sx={{ p: { xs: 1.5, sm: 2 }, alignItems: { xs: 'stretch', sm: 'center' }, borderBottom: '1px solid', borderColor: 'divider' }}
        >
          {onSearchChange && (
            <TextField
              size="small"
              type="search"
              aria-label="Search this list"
              placeholder={searchPlaceholder}
              value={search ?? ''}
              onChange={(e) => onSearchChange(e.target.value)}
              slotProps={{ input: {
                startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment>,
                endAdornment: search ? <InputAdornment position="end"><IconButton aria-label="Clear search" size="small" onClick={() => onSearchChange('')} edge="end"><ClearIcon fontSize="small" /></IconButton></InputAdornment> : undefined,
              } }}
              sx={{ flexGrow: 1, width: { xs: '100%', sm: 'auto' }, maxWidth: { sm: 420 } }}
            />
          )}
          <Box sx={{ flexGrow: 1 }} />
          {toolbarActions}
        </Stack>
      )}
      {isEmpty ? (
        <Box sx={{ minHeight: { xs: 210, sm: 300 }, display: 'grid', placeItems: 'center', px: 2 }}>
          {emptyContent}
        </Box>
      ) : (
      <DataGrid
        rows={rows as unknown as GridRowsProp}
        columns={columns as GridColDef[]}
        loading={loading}
        {...(paginationMode === 'server' ? { rowCount } : {})}
        paginationMode={paginationMode}
        paginationModel={{ page: page - 1, pageSize }}
        onPaginationModelChange={(model) => {
          if (model.page !== page - 1) onPageChange(model.page + 1)
          if (onPageSizeChange && model.pageSize !== pageSize) onPageSizeChange(model.pageSize)
        }}
        pageSizeOptions={onPageSizeChange ? [10, 20, 25, 50] : [pageSize]}
        disableRowSelectionOnClick
        onRowClick={onRowClick ? (params) => onRowClick(String(params.id)) : undefined}
        autoHeight
        rowHeight={56}
        columnHeaderHeight={48}
        sx={{
          border: 'none',
          '& .MuiDataGrid-row': {
            cursor: onRowClick ? 'pointer' : 'default',
            transition: 'background-color 120ms ease',
            borderBottom: '1px solid',
            borderColor: 'divider',
          },
          '& .MuiDataGrid-row:hover': { bgcolor: 'action.selected' },
          '& .MuiDataGrid-cell:focus, & .MuiDataGrid-cell:focus-within, & .MuiDataGrid-columnHeader:focus, & .MuiDataGrid-columnHeader:focus-within': { outline: 'none' },
          '& .MuiDataGrid-cell:focus-visible, & .MuiDataGrid-cell:focus-within:focus-visible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: -2 },
          '& .MuiDataGrid-footerContainer': { px: { xs: 1, sm: 2 } },
        }}
        slots={{
          noRowsOverlay: () => (
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%' }}>
              {emptyContent}
            </Box>
          ),
        }}
      />
      )}
    </Paper>
  )
}
