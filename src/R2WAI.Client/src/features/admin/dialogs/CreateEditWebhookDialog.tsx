import { useEffect, useState } from 'react'
import AutorenewOutlinedIcon from '@mui/icons-material/AutorenewOutlined'
import { useQuery } from '@tanstack/react-query'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, InputAdornment, MenuItem, Stack, TextField, Tooltip } from '@mui/material'
import { listWorkflows } from '../../automations/api'
import type { WebhookDto } from '../types'

interface CreateEditWebhookDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: { name: string; triggerType: string; workflowId: string; secret: string; slug?: string }) => void | Promise<void>
  webhook?: WebhookDto | null
  isSubmitting?: boolean
}

export function CreateEditWebhookDialog({ open, onClose, onSubmit, webhook, isSubmitting }: CreateEditWebhookDialogProps) {
  const isEdit = !!webhook
  const [name, setName] = useState('')
  const [workflowId, setWorkflowId] = useState('')
  const [secret, setSecret] = useState('')
  const workflowsQuery = useQuery({
    queryKey: ['webhook-workflow-options'],
    queryFn: () => listWorkflows(1, 100, ''),
    enabled: open,
  })
  const activeWorkflows = workflowsQuery.data?.items.filter((workflow) => workflow.isActive) ?? []

  useEffect(() => {
    if (!open) return
    setName(webhook?.name ?? '')
    setWorkflowId(webhook?.workflowId ?? '')
    setSecret('')
  }, [open, webhook])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({ name, triggerType: 'Workflow', workflowId, secret, slug: webhook?.endpointUrl.split('/').pop() })
  }

  function generateSecret() {
    const bytes = crypto.getRandomValues(new Uint8Array(32))
    setSecret(Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join(''))
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth aria-label={isEdit ? 'Edit Webhook' : 'New Webhook'}>
      <DialogTitle>{isEdit ? 'Edit Webhook' : 'New Webhook'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField
              label="Automation"
              select
              fullWidth
              required
              value={workflowId}
              onChange={(e) => setWorkflowId(e.target.value)}
              disabled={workflowsQuery.isLoading || workflowsQuery.isError}
              error={workflowsQuery.isError}
              helperText={workflowsQuery.isLoading
                ? 'Loading automations…'
                : workflowsQuery.isError
                  ? 'Could not load automations. Close and retry.'
                  : activeWorkflows.length === 0
                    ? 'Publish an automation before creating a webhook.'
                    : 'The published automation this webhook starts.'}
            >
              {activeWorkflows.map((workflow) => (
                <MenuItem key={workflow.id} value={workflow.id}>{workflow.name}</MenuItem>
              ))}
            </TextField>
            <TextField
              label={isEdit ? 'New secret (optional)' : 'Secret'}
              fullWidth
              required={!isEdit}
              type="text"
              value={secret}
              onChange={(e) => setSecret(e.target.value)}
              helperText={isEdit ? 'Leave blank to keep the current secret.' : 'Use at least 32 characters. Copy it before saving; it cannot be retrieved later.'}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <Tooltip title="Generate a strong secret">
                        <IconButton aria-label="Generate a strong webhook secret" onClick={generateSecret} edge="end">
                          <AutorenewOutlinedIcon />
                        </IconButton>
                      </Tooltip>
                    </InputAdornment>
                  ),
                },
              }}
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name || !workflowId || workflowsQuery.isLoading || workflowsQuery.isError || activeWorkflows.length === 0 || (secret.length > 0 && secret.length < 32) || (!isEdit && secret.length < 32)} loading={isSubmitting}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
