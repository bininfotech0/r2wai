import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import CancelIcon from '@mui/icons-material/Cancel'
import { useState } from 'react'

export interface PublishCheckItem {
  label: string
  ready: boolean
  detail?: string | null
}

interface PublishChecklistProps {
  open: boolean
  onClose: () => void
  onPublish: (changeNote: string) => void | Promise<void>
  title: string
  version: string
  checks: PublishCheckItem[]
  channels?: { label: string; enabled: boolean }[]
  isPublishing?: boolean
  checksLoading?: boolean
  checksError?: boolean
  onRetryChecks?: () => void
}

/**
 * Shared Draft -> Validate -> Test -> Security Check -> Publish surface,
 * reused by Assistants (Phase 6), Automations (Phase 7), and Chatbots
 * (Phase 10) wherever they expose Publish — a modal, not a page, per the
 * UI/UX design system. Publish is disabled until every check is ready.
 */
export function PublishChecklist({
  open,
  onClose,
  onPublish,
  title,
  version,
  checks,
  channels,
  isPublishing,
  checksLoading = false,
  checksError = false,
  onRetryChecks,
}: PublishChecklistProps) {
  const [changeNote, setChangeNote] = useState('')
  const allReady = !checksLoading && !checksError && checks.length > 0 && checks.every((c) => c.ready)

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Publish {title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="overline" color="text.secondary">
              Readiness
            </Typography>
            {checksLoading && <Typography variant="body2" color="text.secondary">Checking publish requirements…</Typography>}
            {checksError && <Alert severity="error" action={onRetryChecks && <Button color="inherit" size="small" onClick={onRetryChecks}>Retry</Button>}>Unable to check publish readiness.</Alert>}
            <Stack spacing={0.75} sx={{ mt: 0.5 }}>
              {!checksLoading && !checksError && checks.map((check) => (
                <Stack key={check.label} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  {check.ready ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                  <Typography variant="body2">{check.label}</Typography>
                  {check.detail && (
                    <Typography variant="caption" color="text.secondary">
                      — {check.detail}
                    </Typography>
                  )}
                </Stack>
              ))}
            </Stack>
          </Box>

          <TextField label="Version" value={version} slotProps={{ input: { readOnly: true } }} size="small" />

          {channels && channels.length > 0 && (
            <Box>
              <Typography variant="overline" color="text.secondary">
                Channels
              </Typography>
              <Stack sx={{ mt: 0.5 }}>
                {channels.map((c) => (
                  <Typography key={c.label} variant="body2" color={c.enabled ? 'text.primary' : 'text.disabled'}>
                    {c.enabled ? '☑' : '☐'} {c.label}
                  </Typography>
                ))}
              </Stack>
            </Box>
          )}

          <TextField
            label="Change note"
            value={changeNote}
            onChange={(e) => setChangeNote(e.target.value)}
            multiline
            minRows={2}
            size="small"
          />

          {!checksLoading && !checksError && !allReady && <Alert severity="warning">Resolve the items above before publishing.</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={isPublishing}>
          Cancel
        </Button>
        <Button
          variant="contained"
          disabled={!allReady || isPublishing}
          onClick={() => void onPublish(changeNote)}
        >
          {isPublishing ? 'Publishing…' : `Publish ${version}`}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
