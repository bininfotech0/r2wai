import { useEffect, useState } from 'react'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'
import type { WebhookDto } from '../types'

interface CreateEditWebhookDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: { name: string; triggerType: string; workflowId?: string; secret?: string; slug?: string }) => void | Promise<void>
  webhook?: WebhookDto | null
  isSubmitting?: boolean
}

export function CreateEditWebhookDialog({ open, onClose, onSubmit, webhook, isSubmitting }: CreateEditWebhookDialogProps) {
  const isEdit = !!webhook
  const [name, setName] = useState('')
  const [triggerType, setTriggerType] = useState('Workflow')
  const [workflowId, setWorkflowId] = useState('')
  const [secret, setSecret] = useState('')

  useEffect(() => {
    if (!open) return
    setName(webhook?.name ?? '')
    setTriggerType(webhook?.triggerType ?? 'Workflow')
    setWorkflowId(webhook?.workflowId ?? '')
    setSecret('')
  }, [open, webhook])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({ name, triggerType, workflowId: workflowId || undefined, secret: secret || undefined })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Webhook' : 'New Webhook'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField label="Trigger type" fullWidth value={triggerType} onChange={(e) => setTriggerType(e.target.value)} />
            <TextField
              label="Workflow ID (optional)"
              fullWidth
              value={workflowId}
              onChange={(e) => setWorkflowId(e.target.value)}
              helperText="The automation this webhook starts when called"
            />
            <TextField label="Secret (optional)" fullWidth type="password" value={secret} onChange={(e) => setSecret(e.target.value)} />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
