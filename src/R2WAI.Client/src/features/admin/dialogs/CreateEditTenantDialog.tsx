import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Alert, MenuItem, TextField } from '@mui/material'
import { FormDialog } from '../../../components/dialogs/FormDialog'
import type { TenantDto, TenantStatus } from '../types'

const STATUSES: TenantStatus[] = ['Active', 'Suspended', 'Trial', 'Expired']

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  slug: z
    .string()
    .min(1, 'Slug is required')
    .max(100)
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Lowercase letters, numbers, and hyphens only'),
  domain: z.string().max(200).optional(),
  status: z.enum(['Active', 'Suspended', 'Trial', 'Expired']).optional(),
})

export type TenantFormValues = z.infer<typeof schema>

interface CreateEditTenantDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: TenantFormValues) => void | Promise<void>
  tenant?: TenantDto | null
  isSubmitting?: boolean
  // The caller's own current tenant — server-enforced (UpdateTenantCommand/DeleteTenantCommand
  // both reject it), surfaced here too so the admin sees why before submitting, not just after.
  isOwnTenant?: boolean
}

export function CreateEditTenantDialog({ open, onClose, onSubmit, tenant, isSubmitting, isOwnTenant }: CreateEditTenantDialogProps) {
  const isEdit = !!tenant
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<TenantFormValues>({
    resolver: zodResolver(schema),
    values: {
      name: tenant?.name ?? '',
      slug: tenant?.slug ?? '',
      domain: tenant?.domain ?? '',
      status: tenant?.status ?? 'Active',
    },
  })

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      title={isEdit ? 'Edit Organisation' : 'New Organisation'}
      onSubmit={(e) => void handleSubmit(onSubmit)(e)}
      isSubmitting={isSubmitting}
      submitLabel={isEdit ? 'Save changes' : 'Create'}
    >
      <TextField label="Name" fullWidth autoFocus error={!!errors.name} helperText={errors.name?.message} {...register('name')} />
      <TextField
        label="Slug"
        fullWidth
        error={!!errors.slug}
        helperText={errors.slug?.message ?? 'Lowercase, hyphenated, must be unique platform-wide.'}
        {...register('slug')}
      />
      <TextField label="Domain" fullWidth placeholder="acme.example.com" {...register('domain')} />
      {isEdit && (
        <TextField select label="Status" fullWidth disabled={isOwnTenant} {...register('status')}>
          {STATUSES.map((s) => (
            <MenuItem key={s} value={s}>
              {s}
            </MenuItem>
          ))}
        </TextField>
      )}
      {isOwnTenant && (
        <Alert severity="warning" sx={{ fontSize: '0.8125rem' }}>
          This is your own current organisation — its status can&apos;t be changed here, to avoid locking
          yourself (and everyone else in it) out.
        </Alert>
      )}
    </FormDialog>
  )
}
