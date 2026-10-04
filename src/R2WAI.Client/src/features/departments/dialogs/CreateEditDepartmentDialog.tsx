import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { TextField } from '@mui/material'
import { FormDialog } from '../../../components/dialogs/FormDialog'
import type { DepartmentDto } from '../types'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  code: z.string().min(1, 'Code is required').max(50),
  description: z.string().max(2000).optional(),
})

export type DepartmentFormValues = z.infer<typeof schema>

interface CreateEditDepartmentDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: DepartmentFormValues) => void | Promise<void>
  department?: DepartmentDto | null
  isSubmitting?: boolean
}

export function CreateEditDepartmentDialog({
  open,
  onClose,
  onSubmit,
  department,
  isSubmitting,
}: CreateEditDepartmentDialogProps) {
  const isEdit = !!department
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<DepartmentFormValues>({
    resolver: zodResolver(schema),
    values: {
      name: department?.name ?? '',
      code: department?.code ?? '',
      description: department?.description ?? '',
    },
  })

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={isEdit ? 'Edit Department' : 'New Department'}
      onSubmit={(e) => void handleSubmit(onSubmit)(e)}
      isSubmitting={isSubmitting}
      submitLabel={isEdit ? 'Save changes' : 'Create'}
    >
      <TextField
        label="Name"
        fullWidth
        autoFocus
        error={!!errors.name}
        helperText={errors.name?.message}
        {...register('name')}
      />
      <TextField
        label="Code"
        fullWidth
        disabled={isEdit}
        error={!!errors.code}
        helperText={errors.code?.message ?? (isEdit ? 'Code cannot be changed after creation' : undefined)}
        {...register('code')}
      />
      <TextField
        label="Description"
        fullWidth
        multiline
        minRows={2}
        error={!!errors.description}
        helperText={errors.description?.message}
        {...register('description')}
      />
    </FormDialog>
  )
}
