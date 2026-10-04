import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material'

interface ConfirmDialogProps {
  open: boolean
  onClose: () => void
  onConfirm: () => void
  title: string
  /** Body copy. Say what will change, and what will be kept. */
  message: string
  confirmLabel?: string
  cancelLabel?: string
  /** Renders the confirm button in the error colour for irreversible actions. */
  destructive?: boolean
  /** Shows a progress state and blocks dismissal while the mutation is in flight. */
  busy?: boolean
}

/**
 * Generic confirmation for consequential but non-delete actions — publish, unpublish,
 * revoke, disconnect. Complements ConfirmDeleteDialog, which is deliberately hardwired
 * to a red "Delete" button and so is the wrong control for "are you sure you want to
 * publish this?".
 *
 * While `busy` is true the dialog cannot be dismissed, because closing it mid-request
 * leaves the user unsure whether the action went through.
 */
export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
  busy = false,
}: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={busy ? undefined : onClose}
      maxWidth="xs"
      fullWidth
      aria-labelledby="confirm-dialog-title"
    >
      <DialogTitle id="confirm-dialog-title">{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>
          {cancelLabel}
        </Button>
        <Button
          onClick={onConfirm}
          color={destructive ? 'error' : 'primary'}
          variant="contained"
          disabled={busy}
          autoFocus
        >
          {busy ? 'Working…' : confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
