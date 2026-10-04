import { useState } from 'react'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'
import type { CreateApiKeyInput } from '../types'

interface CreateApiKeyDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateApiKeyInput) => void | Promise<void>
  isSubmitting?: boolean
}

export function CreateApiKeyDialog({ open, onClose, onSubmit, isSubmitting }: CreateApiKeyDialogProps) {
  const [name, setName] = useState('')
  const [scopes, setScopes] = useState('')
  const [roles, setRoles] = useState('')

  function handleClose() {
    setName('')
    setScopes('')
    setRoles('')
    onClose()
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name,
      scopes: scopes ? scopes.split(',').map((s) => s.trim()).filter(Boolean) : undefined,
      roles: roles ? roles.split(',').map((s) => s.trim()).filter(Boolean) : undefined,
    })
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>New API Key</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField label="Scopes (comma-separated)" fullWidth value={scopes} onChange={(e) => setScopes(e.target.value)} />
            <TextField label="Roles (comma-separated)" fullWidth value={roles} onChange={(e) => setRoles(e.target.value)} />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name}>
            {isSubmitting ? 'Creating…' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
