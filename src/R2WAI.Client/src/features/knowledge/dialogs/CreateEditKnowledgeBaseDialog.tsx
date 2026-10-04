import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { MenuItem, TextField, Typography } from '@mui/material'
import { FormDialog } from '../../../components/dialogs/FormDialog'
import { DATA_CLASSIFICATIONS, type KnowledgeBaseDto } from '../types'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(2000).optional(),
  dataClassification: z.enum(['Public', 'Internal', 'Confidential', 'Restricted']),
})

export type KnowledgeBaseFormValues = z.infer<typeof schema>

interface CreateEditKnowledgeBaseDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: KnowledgeBaseFormValues) => void | Promise<void>
  knowledgeBase?: KnowledgeBaseDto | null
  isSubmitting?: boolean
}

export function CreateEditKnowledgeBaseDialog({
  open,
  onClose,
  onSubmit,
  knowledgeBase,
  isSubmitting,
}: CreateEditKnowledgeBaseDialogProps) {
  const isEdit = !!knowledgeBase
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<KnowledgeBaseFormValues>({
    resolver: zodResolver(schema),
    values: {
      name: knowledgeBase?.name ?? '',
      description: knowledgeBase?.description ?? '',
      dataClassification: knowledgeBase?.dataClassification ?? 'Internal',
    },
  })

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={isEdit ? 'Edit Knowledge Base' : 'New Knowledge Base'}
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
        label="Description"
        fullWidth
        multiline
        minRows={2}
        error={!!errors.description}
        helperText={errors.description?.message}
        {...register('description')}
      />
      <TextField
        select
        label="Data classification"
        fullWidth
        error={!!errors.dataClassification}
        helperText={errors.dataClassification?.message}
        {...register('dataClassification')}
      >
        {DATA_CLASSIFICATIONS.map((c) => (
          <MenuItem key={c} value={c}>
            {c}
          </MenuItem>
        ))}
      </TextField>
      <Typography variant="caption" color="text.secondary">
        Confidential or Restricted knowledge bases are excluded from AI answers whenever a tenant's
        Knowledge policy sets a lower ceiling.
      </Typography>
    </FormDialog>
  )
}
