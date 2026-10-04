import { Dialog, DialogContent, DialogTitle, Stack, Typography } from '@mui/material'
import { TooltipIconButton as IconButton } from './TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'

interface KeyboardShortcutsHelpProps {
  open: boolean
  onClose: () => void
}

const SHORTCUTS: { keys: string; description: string }[] = [
  { keys: 'Ctrl K', description: 'Open command palette (search & actions)' },
  { keys: '?', description: 'Show this keyboard shortcuts panel' },
  { keys: 'Esc', description: 'Close the open dialog, drawer, or panel' },
  { keys: 'Enter', description: 'Send the current message in a chat input' },
]

export function KeyboardShortcutsHelp({ open, onClose }: KeyboardShortcutsHelpProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        Keyboard shortcuts
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={1.5} sx={{ pb: 1 }}>
          {SHORTCUTS.map((s) => (
            <Stack key={s.keys} direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">
                {s.description}
              </Typography>
              <Stack direction="row" spacing={0.5}>
                {s.keys.split(' ').map((k) => (
                  <Typography
                    key={k}
                    component="kbd"
                    variant="caption"
                    sx={{
                      fontWeight: 600,
                      border: '1px solid',
                      borderColor: 'divider',
                      borderRadius: 1,
                      px: 0.8,
                      py: 0.2,
                      bgcolor: 'action.hover',
                    }}
                  >
                    {k}
                  </Typography>
                ))}
              </Stack>
            </Stack>
          ))}
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
