import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'

interface RevealKeyDialogProps {
  open: boolean
  onClose: () => void
  apiKey: string | null
}

export function RevealKeyDialog({ open, onClose, apiKey }: RevealKeyDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>API Key Created</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <Alert severity="warning">Store this key securely — it cannot be shown again.</Alert>
          <TextField
            value={apiKey ?? ''}
            fullWidth
            slotProps={{ input: { readOnly: true, sx: { fontFamily: 'monospace' } } }}
            onFocus={(e) => e.target.select()}
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button variant="contained" onClick={onClose}>
          Done
        </Button>
      </DialogActions>
    </Dialog>
  )
}
