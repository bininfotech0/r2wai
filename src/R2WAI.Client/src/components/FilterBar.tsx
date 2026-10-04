import { Box, MenuItem, TextField } from '@mui/material'

export interface FilterBarOption {
  value: string
  label: string
}

export interface FilterBarField {
  key: string
  label: string
  value: string
  options: FilterBarOption[]
  onChange: (value: string) => void
}

interface FilterBarProps {
  filters: FilterBarField[]
}

/**
 * A row of facet dropdowns (status/application/date/user, ...) meant to sit
 * alongside DataTable's existing single search field — DataTable itself
 * stays free-text-search-only, multi-facet filtering is composed on top via
 * this component wherever a page has real filterable fields.
 */
export function FilterBar({ filters }: FilterBarProps) {
  if (filters.length === 0) return null
  return (
    <Box
      role="group"
      aria-label="Filters"
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(auto-fit, minmax(160px, 1fr))' },
        gap: 1.25,
        mb: 2,
      }}
    >
      {filters.map((f) => (
        <TextField
          key={f.key}
          select
          size="small"
          label={f.label}
          value={f.value}
          onChange={(e) => f.onChange(e.target.value)}
          fullWidth
          slotProps={{ select: { inputProps: { 'aria-label': f.label } } }}
        >
          {f.options.map((o) => (
            <MenuItem key={o.value} value={o.value}>
              {o.label}
            </MenuItem>
          ))}
        </TextField>
      ))}
    </Box>
  )
}
