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
      // A compact wrapping row (Copilot Studio list pages), not full-width boxes.
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 1,
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
          sx={{ width: { xs: 'calc(50% - 4px)', sm: 180 } }}
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
