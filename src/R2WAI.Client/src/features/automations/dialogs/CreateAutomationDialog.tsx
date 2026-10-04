import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Box,
  Button,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  List,
  ListItemButton,
  ListItemText,
  TextField,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'
import { AiDraftCreatePrompt } from '../../../components/AiDraftCreatePrompt'
import { createWorkflow, draftWorkflow, getWorkflowTemplates } from '../api'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import type { WorkflowStep } from '../types'

interface CreateAutomationDialogProps {
  open: boolean
  onClose: () => void
  onCreated: (workflowId: string) => void
  initialDescription?: string
}

const EXAMPLES = [
  'When a new employee joins, validate their details, create onboarding tasks, send an email and notify HR.',
  'Route invoice approvals by amount, then process payment.',
  'When a leave request is submitted, notify the manager for approval.',
]

type View = 'choose' | 'template'

export function CreateAutomationDialog({ open, onClose, onCreated, initialDescription }: CreateAutomationDialogProps) {
  const { notify } = useSnackbar()
  const [view, setView] = useState<View>('choose')
  const [isGenerating, setIsGenerating] = useState(false)
  const [manualName, setManualName] = useState('')
  const [manualMode, setManualMode] = useState(false)
  const [isCreating, setIsCreating] = useState(false)

  const templatesQuery = useQuery({
    queryKey: ['workflow-templates'],
    queryFn: getWorkflowTemplates,
    enabled: view === 'template',
  })

  function reset() {
    setView('choose')
    setManualMode(false)
    setManualName('')
  }

  async function handleGenerate(description: string) {
    setIsGenerating(true)
    try {
      const draft = await draftWorkflow(description)
      const steps: WorkflowStep[] = draft.actions.map((action, i) => ({
        order: i,
        name: action,
        action,
        type: 'Action',
      }))
      const created = await createWorkflow({
        name: draft.name ?? description.slice(0, 60),
        trigger: draft.trigger ?? undefined,
        steps: JSON.stringify(steps),
      })
      notify('Automation drafted', 'success')
      reset()
      onCreated(created.id)
    } catch {
      notify('Failed to draft automation — try a template or build manually', 'error')
    } finally {
      setIsGenerating(false)
    }
  }

  async function handleTemplateSelect(templateId: string) {
    const template = templatesQuery.data?.items.find((t) => t.id === templateId)
    if (!template) return
    setIsCreating(true)
    try {
      const steps: WorkflowStep[] = template.steps.map((s) => ({
        order: s.order,
        name: s.name,
        action: s.action,
        assignedRole: s.assignedRole,
        type: 'Action',
      }))
      const created = await createWorkflow({ name: template.name, type: template.type, steps: JSON.stringify(steps) })
      notify('Automation created from template', 'success')
      reset()
      onCreated(created.id)
    } catch {
      notify('Failed to create automation from template', 'error')
    } finally {
      setIsCreating(false)
    }
  }

  async function handleManualCreate() {
    if (!manualName.trim()) return
    setIsCreating(true)
    try {
      const created = await createWorkflow({ name: manualName.trim() })
      notify('Automation created', 'success')
      reset()
      onCreated(created.id)
    } catch {
      notify('Failed to create automation', 'error')
    } finally {
      setIsCreating(false)
    }
  }

  return (
    <Dialog
      open={open}
      onClose={() => {
        reset()
        onClose()
      }}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        Create Automation
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        {view === 'template' ? (
          <Box>
            <Button size="small" onClick={() => setView('choose')} sx={{ mb: 1 }}>
              Back
            </Button>
            <List>
              {templatesQuery.data?.items.map((t) => (
                <ListItemButton key={t.id} disabled={isCreating} onClick={() => void handleTemplateSelect(t.id)}>
                  <ListItemText primary={t.name} secondary={t.description} />
                </ListItemButton>
              ))}
            </List>
          </Box>
        ) : manualMode ? (
          <Box sx={{ py: 1 }}>
            <TextField
              label="Name"
              autoFocus
              fullWidth
              value={manualName}
              onChange={(e) => setManualName(e.target.value)}
              sx={{ mb: 2 }}
            />
            <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
              <Button onClick={() => setManualMode(false)}>Back</Button>
              <Button variant="contained" disabled={!manualName.trim() || isCreating} onClick={() => void handleManualCreate()}>
                {isCreating ? 'Creating…' : 'Create'}
              </Button>
            </Box>
          </Box>
        ) : (
          <Box>
            <AiDraftCreatePrompt
              heading="What do you want to automate?"
              placeholder="When a new employee joins, create onboarding tasks, notify HR and send the welcome email."
              examples={EXAMPLES}
              initialValue={initialDescription}
              onGenerate={handleGenerate}
              onManual={() => setManualMode(true)}
              isGenerating={isGenerating}
            />
            <Divider sx={{ my: 2 }} />
            <Typography variant="body2" align="center">
              <Button size="small" onClick={() => setView('template')}>
                Or start from a template
              </Button>
            </Typography>
          </Box>
        )}
      </DialogContent>
    </Dialog>
  )
}
