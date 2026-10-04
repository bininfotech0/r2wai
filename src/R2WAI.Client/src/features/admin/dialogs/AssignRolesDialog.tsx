import { useEffect, useState } from 'react'
import { Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack } from '@mui/material'
import type { RoleDto, UserDto } from '../types'

interface AssignRolesDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (roleIds: string[]) => void | Promise<void>
  user: UserDto | null
  roles: RoleDto[]
  isSubmitting?: boolean
}

export function AssignRolesDialog({ open, onClose, onSubmit, user, roles, isSubmitting }: AssignRolesDialogProps) {
  const [selected, setSelected] = useState<Set<string>>(new Set())

  useEffect(() => {
    if (!open || !user) return
    setSelected(new Set(roles.filter((r) => user.roles.includes(r.name)).map((r) => r.id)))
  }, [open, user, roles])

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Roles for {user?.fullName}</DialogTitle>
      <DialogContent>
        <Stack spacing={0.5} sx={{ mt: 1 }}>
          {roles.map((r) => (
            <FormControlLabel
              key={r.id}
              control={<Checkbox checked={selected.has(r.id)} onChange={() => toggle(r.id)} />}
              label={r.name}
            />
          ))}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button variant="contained" disabled={isSubmitting} onClick={() => void onSubmit([...selected])}>
          {isSubmitting ? 'Saving…' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
