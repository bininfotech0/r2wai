import {
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material'
import CheckCircleOutlineOutlined from '@mui/icons-material/CheckCircleOutlineOutlined'
import ErrorOutlineOutlined from '@mui/icons-material/ErrorOutlineOutlined'
import WarningAmberOutlined from '@mui/icons-material/WarningAmberOutlined'
import type { TestRunDto } from '../types'

const STATUS_ICON: Record<string, React.ReactNode> = {
  Passed: <CheckCircleOutlineOutlined fontSize="small" color="success" />,
  Failed: <ErrorOutlineOutlined fontSize="small" color="error" />,
  Warning: <WarningAmberOutlined fontSize="small" color="warning" />,
}

interface TestRunDetailDialogProps {
  run: TestRunDto | null
  onClose: () => void
}

// No TestRunDetail.razor page exists in the Blazor precedent either — its
// "View Details" opens a dialog, not a route. Matched here rather than
// inventing a new /monitor/test-runs/:id route with no real UI precedent.
export function TestRunDetailDialog({ run, onClose }: TestRunDetailDialogProps) {
  return (
    <Dialog open={!!run} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        Test Run{' '}
        {run && (
          <Typography component="span" variant="body2" color="text.secondary">
            {new Date(run.startedAt).toLocaleString()}
          </Typography>
        )}
      </DialogTitle>
      <DialogContent>
        {run && (
          <Stack spacing={2}>
            <Stack direction="row" spacing={1}>
              <Chip label={`${run.passedCount} passed`} size="small" color="success" variant="outlined" />
              <Chip label={`${run.failedCount} failed`} size="small" color="error" variant="outlined" />
              {run.warningCount > 0 && <Chip label={`${run.warningCount} warnings`} size="small" color="warning" variant="outlined" />}
            </Stack>
            <List dense>
              {run.results.map((r) => (
                <ListItem key={r.id} divider alignItems="flex-start">
                  <ListItemIcon sx={{ minWidth: 32, mt: 0.5 }}>{STATUS_ICON[r.status] ?? null}</ListItemIcon>
                  <ListItemText
                    primary={r.testCaseName}
                    secondary={
                      <>
                        <Typography variant="caption" color="text.secondary" component="span" sx={{ display: 'block' }}>
                          {r.durationMs}ms
                        </Typography>
                        {r.actualResponse && (
                          <Typography variant="body2" component="span" sx={{ display: 'block', whiteSpace: 'pre-wrap' }}>
                            {r.actualResponse}
                          </Typography>
                        )}
                        {r.errorMessage && (
                          <Typography variant="body2" color="error" component="span" sx={{ display: 'block' }}>
                            {r.errorMessage}
                          </Typography>
                        )}
                      </>
                    }
                  />
                </ListItem>
              ))}
            </List>
          </Stack>
        )}
      </DialogContent>
    </Dialog>
  )
}
