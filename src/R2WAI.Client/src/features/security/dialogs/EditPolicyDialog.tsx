import { useEffect, useState } from 'react'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Switch, TextField, Typography } from '@mui/material'
import type { GlobalPolicyDto, PolicyType } from '../types'

interface EditPolicyDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: { name: string; content: string; isActive: boolean }) => void | Promise<void>
  type: PolicyType | null
  policy: GlobalPolicyDto | null
  isSubmitting?: boolean
}

// AgentRuntimePolicyService.GetRuntimeAsync does a bare Enum.TryParse<AgentRuntimeKind> on Content —
// unlike every other policy type here, this one isn't JSON, so it needs its own plain-language hint
// rather than the generic multiline field standing alone.
const CONTENT_HELPER_TEXT: Partial<Record<PolicyType, string>> = {
  AgentRuntime:
    'Content must be exactly "SemanticKernel" or "AgentFramework" (case-insensitive) — selects which agent ' +
    'runtime this tenant’s assistants use. Anything else, or no active policy, falls back to SemanticKernel. ' +
    'AgentFramework is a narrow first cut: OpenAI provider only, read-only tools only.',
}

export function EditPolicyDialog({ open, onClose, onSubmit, type, policy, isSubmitting }: EditPolicyDialogProps) {
  const [name, setName] = useState('')
  const [content, setContent] = useState('')
  const [isActive, setIsActive] = useState(true)

  useEffect(() => {
    if (!open) return
    setName(policy?.name ?? type ?? '')
    setContent(policy?.content ?? '')
    setIsActive(policy?.isActive ?? true)
  }, [open, policy, type])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({ name, content, isActive })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{type} Policy</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField
              label="Content"
              fullWidth
              multiline
              minRows={4}
              value={content}
              onChange={(e) => setContent(e.target.value)}
              helperText={type ? CONTENT_HELPER_TEXT[type] : undefined}
            />
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              <Typography variant="body2">Active</Typography>
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name}>
            {isSubmitting ? 'Saving…' : 'Save'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
