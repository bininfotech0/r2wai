import { queryKeys } from '../../../lib/api/queryKeys'
import { useEffect, useState } from 'react'
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
  TextField,
} from '@mui/material'
import { listAssistants } from '../../assistants/api'
import type { CreateTestCaseInput, TestCaseDto, UpdateTestCaseInput } from '../types'

interface CreateEditTestCaseDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateTestCaseInput) => void | Promise<void>
  testCase?: TestCaseDto | null
  isSubmitting?: boolean
}

export function CreateEditTestCaseDialog({ open, onClose, onSubmit, testCase, isSubmitting }: CreateEditTestCaseDialogProps) {
  const isEdit = !!testCase
  const [assistantId, setAssistantId] = useState('')
  const [name, setName] = useState('')
  const [question, setQuestion] = useState('')
  const [expectedResponseContains, setExpectedResponseContains] = useState('')
  const [expectedCapabilityCalled, setExpectedCapabilityCalled] = useState('')

  const assistantsQuery = useQuery({
    queryKey: queryKeys.assistants.forTestCase,
    queryFn: () => listAssistants(1, 100, ''),
    enabled: open,
  })

  useEffect(() => {
    if (!open) return
    setAssistantId(testCase?.assistantId ?? '')
    setName(testCase?.name ?? '')
    setQuestion(testCase?.question ?? '')
    setExpectedResponseContains(testCase?.expectedResponseContains ?? '')
    setExpectedCapabilityCalled(testCase?.expectedCapabilityCalled ?? '')
  }, [open, testCase])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const values: CreateTestCaseInput & UpdateTestCaseInput = {
      assistantId,
      name,
      question,
      expectedResponseContains: expectedResponseContains || undefined,
      expectedCapabilityCalled: expectedCapabilityCalled || undefined,
    }
    void onSubmit(values)
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Test Case' : 'New Test Case'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <FormControl size="small" fullWidth required disabled={isEdit}>
              <InputLabel id="testcase-assistant-label">Assistant</InputLabel>
              <Select
                labelId="testcase-assistant-label"
                label="Assistant"
                value={assistantId}
                onChange={(e) => setAssistantId(e.target.value)}
              >
                {(assistantsQuery.data?.items ?? []).map((a) => (
                  <MenuItem key={a.id} value={a.id}>
                    {a.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField label="Name" fullWidth required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField
              label="Question"
              fullWidth
              required
              multiline
              minRows={2}
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
            />
            <TextField
              label="Expected response contains (optional)"
              fullWidth
              value={expectedResponseContains}
              onChange={(e) => setExpectedResponseContains(e.target.value)}
            />
            <TextField
              label="Expected tool called (optional)"
              fullWidth
              value={expectedCapabilityCalled}
              onChange={(e) => setExpectedCapabilityCalled(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !assistantId || !name || !question}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
