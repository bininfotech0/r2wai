import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { TextField } from '@mui/material'
import { FormDialog } from '../../../components/dialogs/FormDialog'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(2000).optional(),
})

export type CreateCapabilityFormValues = z.infer<typeof schema>

interface CreateCapabilityDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateCapabilityFormValues) => void | Promise<void>
  isSubmitting?: boolean
}

/** Quick create — name/description only. Linking Tools/Knowledge/Workflows happens in the detail dialog right after. */
export function CreateCapabilityDialog({ open, onClose, onSubmit, isSubmitting }: CreateCapabilityDialogProps) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreateCapabilityFormValues>({
    resolver: zodResolver(schema),
    values: { name: '', description: '' },
  })

  return (
    <FormDialog
      open={open}
      onClose={() => {
        reset()
        onClose()
      }}
      title="Add Capability"
      onSubmit={(e) => void handleSubmit(onSubmit)(e)}
      isSubmitting={isSubmitting}
      submitLabel="Create"
    >
      <TextField
        label="Name"
        placeholder="e.g. Invoice Management"
        fullWidth
        autoFocus
        error={!!errors.name}
        helperText={errors.name?.message}
        {...register('name')}
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
