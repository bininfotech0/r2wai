import { queryKeys } from '../../../lib/api/queryKeys'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import { listAssistants } from '../../assistants/api'
import { listKnowledgeBases } from '../../knowledge/api'
import type { CreateChatbotInput } from '../types'

interface CreateChatbotDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateChatbotInput) => void | Promise<void>
  isSubmitting?: boolean
}

export function CreateChatbotDialog({ open, onClose, onSubmit, isSubmitting }: CreateChatbotDialogProps) {
  const [name, setName] = useState('')
  const [assistantId, setAssistantId] = useState('')
  const [knowledgeBaseId, setKnowledgeBaseId] = useState('')
  const [voiceEnabled, setVoiceEnabled] = useState(false)

  const assistantsQuery = useQuery({
    queryKey: queryKeys.assistants.forChatbot,
    queryFn: () => listAssistants(1, 100, ''),
    enabled: open,
  })
  const kbQuery = useQuery({
    queryKey: ['knowledgebases-for-chatbot'],
    queryFn: () => listKnowledgeBases(1, 100, ''),
    enabled: open,
  })

  function handleClose() {
    setName('')
    setAssistantId('')
    setKnowledgeBaseId('')
    setVoiceEnabled(false)
    onClose()
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name,
      assistantId: assistantId || undefined,
      knowledgeBaseId: knowledgeBaseId || undefined,
      voiceEnabled,
    })
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>New Chatbot</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <FormControl size="small" fullWidth>
              <InputLabel id="chatbot-assistant-label">Assistant (optional)</InputLabel>
              <Select
                labelId="chatbot-assistant-label"
                label="Assistant (optional)"
                value={assistantId}
                onChange={(e) => setAssistantId(e.target.value)}
              >
                <MenuItem value="">None</MenuItem>
                {(assistantsQuery.data?.items ?? []).map((a) => (
                  <MenuItem key={a.id} value={a.id}>
                    {a.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" fullWidth>
              <InputLabel id="chatbot-kb-label">Knowledge Base (optional)</InputLabel>
              <Select
                labelId="chatbot-kb-label"
                label="Knowledge Base (optional)"
                value={knowledgeBaseId}
                onChange={(e) => setKnowledgeBaseId(e.target.value)}
              >
                <MenuItem value="">None</MenuItem>
                {(kbQuery.data?.items ?? []).map((kb) => (
                  <MenuItem key={kb.id} value={kb.id}>
                    {kb.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={voiceEnabled} onChange={(e) => setVoiceEnabled(e.target.checked)} />
              <Typography variant="body2">Voice enabled</Typography>
            </Stack>
            <Typography variant="caption" color="text.secondary">
              Assistant and Knowledge Base can only be set here — the backend has no re-link endpoint after creation.
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name}>
            {isSubmitting ? 'Creating…' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
