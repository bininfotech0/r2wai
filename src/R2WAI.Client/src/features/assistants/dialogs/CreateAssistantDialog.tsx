import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Dialog, DialogContent, DialogTitle, TextField, MenuItem, Stack, Button } from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'
import { AiDraftCreatePrompt } from '../../../components/AiDraftCreatePrompt'
import { createAssistant, generateAssistantConfig, updateAssistant } from '../api'
import { GENERATABLE_TYPES, type AssistantType } from '../types'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { listModels } from '../../admin/api'

interface CreateAssistantDialogProps {
  open: boolean
  onClose: () => void
  onCreated: (assistantId: string) => void
  initialDescription?: string
}

const EXAMPLES = [
  'Help employees answer HR questions, check leave balances, and create leave requests.',
  'IT helpdesk assistant for password resets and software access.',
  'Answers questions about expense reports and reimbursement policy.',
]

export function CreateAssistantDialog({ open, onClose, onCreated, initialDescription }: CreateAssistantDialogProps) {
  const { notify } = useSnackbar()
  const [isGenerating, setIsGenerating] = useState(false)
  const [manualMode, setManualMode] = useState(false)
  const [manualName, setManualName] = useState('')
  const [manualType, setManualType] = useState<AssistantType>('General')
  const [manualModelId, setManualModelId] = useState('')
  const [isCreating, setIsCreating] = useState(false)

  const modelsQuery = useQuery({ queryKey: ['admin', 'models'], queryFn: listModels, enabled: manualMode })
  const activeModels = (modelsQuery.data?.items ?? []).filter((m) => m.isActive)

  async function handleGenerate(description: string) {
    setIsGenerating(true)
    try {
      const config = await generateAssistantConfig(description)
      const created = await createAssistant({ name: config.name, type: config.type })
      await updateAssistant(created.id, { name: config.name, description: config.description, systemPrompt: config.systemPrompt })
      notify('Assistant drafted', 'success')
      onCreated(created.id)
    } catch {
      notify('Failed to generate assistant — try configuring manually', 'error')
    } finally {
      setIsGenerating(false)
    }
  }

  async function handleManualCreate() {
    if (!manualName.trim()) return
    setIsCreating(true)
    try {
      const created = await createAssistant({
        name: manualName.trim(),
        type: manualType,
        modelConfigurationId: manualModelId || undefined,
      })
      notify('Assistant created', 'success')
      onCreated(created.id)
    } catch {
      notify('Failed to create assistant', 'error')
    } finally {
      setIsCreating(false)
    }
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        Create AI Assistant
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        {manualMode ? (
          <Stack spacing={2} sx={{ py: 1 }}>
            <TextField
              label="Name"
              autoFocus
              fullWidth
              value={manualName}
              onChange={(e) => setManualName(e.target.value)}
            />
            <TextField
              select
              label="Type"
              fullWidth
              value={manualType}
              onChange={(e) => setManualType(e.target.value as AssistantType)}
            >
              {GENERATABLE_TYPES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label="Base Model"
              fullWidth
              value={manualModelId}
              onChange={(e) => setManualModelId(e.target.value)}
              helperText="Leave on tenant default to use whichever model is marked default."
              slotProps={{ inputLabel: { shrink: true }, select: { displayEmpty: true } }}
            >
              <MenuItem value="">Use tenant default</MenuItem>
              {activeModels.map((m) => (
                <MenuItem key={m.id} value={m.id}>
                  {m.name} ({m.provider}){m.isDefault ? ' · Default' : ''}
                </MenuItem>
              ))}
            </TextField>
            <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
              <Button onClick={() => setManualMode(false)}>Back</Button>
              <Button variant="contained" disabled={!manualName.trim() || isCreating} onClick={() => void handleManualCreate()}>
                {isCreating ? 'Creating…' : 'Create'}
              </Button>
            </Stack>
          </Stack>
        ) : (
          <AiDraftCreatePrompt
            heading="What should this assistant do?"
            placeholder="Help employees answer HR questions, check leave balances, and create leave requests."
            examples={EXAMPLES}
            initialValue={initialDescription}
            onGenerate={handleGenerate}
            onManual={() => setManualMode(true)}
            isGenerating={isGenerating}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
