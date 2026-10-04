import { useEffect, useState } from 'react'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'
import type { CreateUserInput, UpdateUserInput, UserDto } from '../types'

interface CreateEditUserDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateUserInput | UpdateUserInput) => void | Promise<void>
  user?: UserDto | null
  isSubmitting?: boolean
}

export function CreateEditUserDialog({ open, onClose, onSubmit, user, isSubmitting }: CreateEditUserDialogProps) {
  const isEdit = !!user
  const [externalId, setExternalId] = useState('')
  const [email, setEmail] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [mobileNumber, setMobileNumber] = useState('')
  const emailIsValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())

  useEffect(() => {
    if (!open) return
    setExternalId('')
    setEmail('')
    setFirstName(user?.firstName ?? '')
    setLastName(user?.lastName ?? '')
    setMobileNumber(user?.mobileNumber ?? '')
  }, [open, user])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!firstName.trim() || !lastName.trim()) return
    if (isEdit) {
      void onSubmit({ firstName: firstName.trim(), lastName: lastName.trim(), mobileNumber: mobileNumber.trim() || undefined })
    } else {
      if (!externalId.trim() || !emailIsValid) return
      void onSubmit({ externalId: externalId.trim(), email: email.trim(), firstName: firstName.trim(), lastName: lastName.trim() })
    }
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{isEdit ? 'Edit User' : 'New User'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            {!isEdit && (
              <>
                <TextField
                  label="External ID"
                  fullWidth
                  autoFocus
                  required
                  helperText="A unique identifier for this user (e.g. employee ID or username)"
                  value={externalId}
                  onChange={(e) => setExternalId(e.target.value)}
                />
                <TextField
                  label="Email"
                  type="email"
                  autoComplete="email"
                  inputMode="email"
                  fullWidth
                  required
                  error={!!email.trim() && !emailIsValid}
                  helperText={email.trim() && !emailIsValid ? 'Enter a valid email address.' : undefined}
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </>
            )}
            <TextField label="First name" fullWidth required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            <TextField label="Last name" fullWidth required value={lastName} onChange={(e) => setLastName(e.target.value)} />
            {isEdit && (
              <TextField label="Mobile number" fullWidth value={mobileNumber} onChange={(e) => setMobileNumber(e.target.value)} />
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={isSubmitting || !firstName.trim() || !lastName.trim() || (!isEdit && (!externalId.trim() || !emailIsValid))}
          >
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
