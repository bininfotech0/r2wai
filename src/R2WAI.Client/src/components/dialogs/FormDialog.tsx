import type { ReactNode } from 'react'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack } from '@mui/material'

interface FormDialogProps {
  open: boolean
  onClose: () => void
  title: string
  onSubmit: (e: React.FormEvent) => void
  isSubmitting?: boolean
  submitLabel?: string
  children: ReactNode
  maxWidth?: 'xs' | 'sm' | 'md'
}

/**
 * Modal chrome for simple create/edit forms — the "simple forms = modal"
 * half of the interaction rule (Departments/Roles/API Keys/... ; medium
 * config uses a drawer instead, see later phases). Fields are passed as
 * children; this component owns the Cancel/Submit buttons and the <form>
 * wiring so callers only worry about their fields.
 */
export function FormDialog({
  open,
  onClose,
  title,
  onSubmit,
  isSubmitting,
  submitLabel = 'Save',
  children,
  maxWidth = 'sm',
}: FormDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth={maxWidth} fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <Box component="form" onSubmit={onSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>{children}</Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            {isSubmitting ? 'Saving…' : submitLabel}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
