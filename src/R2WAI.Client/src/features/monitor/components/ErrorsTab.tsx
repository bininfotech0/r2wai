import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Chip, FormControl, InputLabel, List, ListItem, ListItemText, MenuItem, Select, Stack, TextField, Typography } from '@mui/material'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { getErrorLogs } from '../api'

const LEVEL_COLOR: Record<string, 'warning' | 'error' | 'default'> = { Warning: 'warning', Error: 'error', Critical: 'error' }

export function ErrorsTab() {
  const [level, setLevel] = useState('')
  const [correlationId, setCorrelationId] = useState('')

  const query = useQuery({
    queryKey: ['monitor', 'errors', level, correlationId],
    queryFn: () => getErrorLogs(level || undefined, correlationId || undefined),
  })

  return (
    <Stack spacing={2}>
      <Typography variant="caption" color="text.secondary">
        Parsed from the API's own log files (most recent first) — read-only, no persisted error table.
      </Typography>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <FormControl size="small" sx={{ minWidth: { sm: 140 } }}>
          <InputLabel id="error-level-label">Level</InputLabel>
          <Select labelId="error-level-label" label="Level" value={level} onChange={(e) => setLevel(e.target.value)}>
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Warning">Warning</MenuItem>
            <MenuItem value="Error">Error</MenuItem>
            <MenuItem value="Critical">Critical</MenuItem>
          </Select>
        </FormControl>
        <TextField
          size="small"
          label="Correlation ID contains"
          value={correlationId}
          onChange={(e) => setCorrelationId(e.target.value)}
          fullWidth
          sx={{ minWidth: { sm: 240 } }}
        />
      </Stack>

      {query.isLoading ? (
        <LoadingSkeleton variant="text" count={5} height={48} />
      ) : query.isError ? (
        <ErrorState
          title="Unable to load error logs"
          description="The log service did not return a result. Try again to check for matching entries."
          onRetry={() => void query.refetch()}
        />
      ) : (
        <List dense>
          {(query.data?.items ?? []).map((e, i) => (
            <ListItem key={`${e.timestamp}-${i}`} divider alignItems="flex-start">
              <ListItemText
                primary={
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 0.5, sm: 1 }} sx={{ alignItems: { xs: 'flex-start', sm: 'center' } }}>
                    <Chip label={e.level} size="small" color={LEVEL_COLOR[e.level] ?? 'default'} />
                    <Typography variant="caption" color="text.secondary">
                      {new Date(e.timestamp).toLocaleString()} · {e.source}
                    </Typography>
                  </Stack>
                }
                secondary={e.message}
                slotProps={{ secondary: { sx: { whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontFamily: 'monospace', fontSize: '0.75rem' } } }}
              />
            </ListItem>
          ))}
          {(query.data?.items ?? []).length === 0 && (
            <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
              No matching log lines.
            </Typography>
          )}
        </List>
      )}
    </Stack>
  )
}
