import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Alert, TextField } from '@mui/material'
import { FormDialog } from '../../../components/dialogs/FormDialog'
import type { RoleDto } from '../types'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  description: z.string().max(500).optional(),
  permissions: z.string().max(4000).optional(),
})

export type RoleFormValues = z.infer<typeof schema>

interface CreateEditRoleDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: RoleFormValues) => void | Promise<void>
  role?: RoleDto | null
  isSubmitting?: boolean
}

export function CreateEditRoleDialog({ open, onClose, onSubmit, role, isSubmitting }: CreateEditRoleDialogProps) {
  const isEdit = !!role
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RoleFormValues>({
    resolver: zodResolver(schema),
    values: { name: role?.name ?? '', description: role?.description ?? '', permissions: role?.permissions ?? '' },
  })

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={isEdit ? 'Edit Role' : 'New Role'}
      onSubmit={(e) => void handleSubmit(onSubmit)(e)}
      isSubmitting={isSubmitting}
      submitLabel={isEdit ? 'Save changes' : 'Create'}
    >
      <TextField
        label="Name"
        fullWidth
        autoFocus
        disabled={role?.isSystem}
        error={!!errors.name}
        helperText={errors.name?.message}
        {...register('name')}
      />
      <TextField label="Description" fullWidth multiline minRows={2} {...register('description')} />
      <TextField
        label="Permissions"
        fullWidth
        multiline
        minRows={3}
        placeholder="One per line, e.g. Assistants.Create"
        {...register('permissions')}
      />
      <Alert severity="info" sx={{ fontSize: '0.8125rem' }}>
        Permission details are descriptive only. Access is enforced by role name.
      </Alert>
    </FormDialog>
  )
}
