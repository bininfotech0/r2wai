import { useMemo, useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  ButtonBase,
  Chip,
  CircularProgress,
  Menu,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import AddIcon from '@mui/icons-material/Add'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlineOutlined'
import { PublishChecklist } from '../../../components/PublishChecklist'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { useUnsavedChangesGuard } from '../../../lib/useUnsavedChangesGuard'
import { deleteWorkflow, executeWorkflow, getWorkflow, publishWorkflow, updateWorkflow } from '../api'
import { buildFullWorkflowUpdatePayload, type WorkflowStep } from '../types'
import { DynamicStepEditorDrawer } from '../components/DynamicStepEditorDrawer'

const KNOWN_TRIGGERS = ['Application Submitted', 'Application Updated', 'Payment Received', 'Form Submitted', 'Schedule', 'Webhook']

function parseSteps(stepsJson: string | null): WorkflowStep[] {
  if (!stepsJson) return []
  try {
    const parsed = JSON.parse(stepsJson) as WorkflowStep[]
    return Array.isArray(parsed) ? [...parsed].sort((a, b) => a.order - b.order) : []
  } catch {
    return []
  }
}

export function AutomationDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [publishOpen, setPublishOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [editingStep, setEditingStep] = useState<WorkflowStep | null | undefined>(undefined)
  const [menuAnchor, setMenuAnchor] = useState<{ el: HTMLElement; index: number } | null>(null)
  const [localSteps, setLocalSteps] = useState<WorkflowStep[] | null>(null)
  const [trigger, setTrigger] = useState<string | null | undefined>(undefined)
  const [testResult, setTestResult] = useState<string | null>(null)

  const query = useQuery({
    queryKey: ['workflows', id],
    queryFn: () => getWorkflow(id!),
    enabled: !!id,
  })

  const workflow = query.data
  const initialSteps = useMemo(() => (workflow ? parseSteps(workflow.steps) : []), [workflow?.steps])
  const steps = localSteps ?? initialSteps
  const effectiveTrigger = trigger !== undefined ? trigger : workflow?.trigger ?? null
  const isDirty = !!workflow && (
    JSON.stringify(steps) !== JSON.stringify(initialSteps) || effectiveTrigger !== (workflow.trigger ?? null)
  )
  useUnsavedChangesGuard(isDirty)

  const saveMutation = useMutation({
    mutationFn: () =>
      updateWorkflow(
        id!,
        buildFullWorkflowUpdatePayload(workflow!, {
          trigger: effectiveTrigger,
          steps: JSON.stringify(steps.map((s, i) => ({ ...s, order: i }))),
        }),
      ),
    onSuccess: () => {
      notify('Draft saved', 'success')
      setLocalSteps(null)
      setTrigger(undefined)
      void queryClient.invalidateQueries({ queryKey: ['workflows', id] })
    },
    onError: () => notify('Failed to save draft', 'error'),
  })

  const testMutation = useMutation({
    mutationFn: () => executeWorkflow(id!),
    onSuccess: (result) => {
      setTestResult(result.warning ? `Started with a warning: ${result.warning}` : `Run started (${result.status}).`)
      notify('Test run started', 'success')
    },
    onError: () => notify('Failed to start test run', 'error'),
  })

  const publishMutation = useMutation({
    mutationFn: () => publishWorkflow(id!),
    onSuccess: () => {
      notify('Automation published', 'success')
      setPublishOpen(false)
      void queryClient.invalidateQueries({ queryKey: ['workflows', id] })
      void queryClient.invalidateQueries({ queryKey: ['workflows'] })
    },
    onError: () => notify('Failed to publish', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => deleteWorkflow(id!),
    onSuccess: () => {
      notify('Automation deleted', 'success')
      navigate('/automations')
    },
    onError: () => notify('Failed to delete automation', 'error'),
  })

  const otherStepNames = useMemo(
    () => steps.filter((s) => s !== editingStep).map((s) => s.name),
    [steps, editingStep],
  )

  function updateSteps(next: WorkflowStep[]) {
    setLocalSteps(next.map((s, i) => ({ ...s, order: i })))
  }

  function handleStepSave(step: WorkflowStep) {
    if (editingStep) {
      updateSteps(steps.map((s) => (s === editingStep ? step : s)))
    } else {
      updateSteps([...steps, step])
    }
    setEditingStep(undefined)
  }

  function moveStep(index: number, direction: -1 | 1) {
    const next = [...steps]
    const target = index + direction
    if (target < 0 || target >= next.length) return
    ;[next[index], next[target]] = [next[target], next[index]]
    updateSteps(next)
    setMenuAnchor(null)
  }

  function removeStep(index: number) {
    updateSteps(steps.filter((_, i) => i !== index))
    setMenuAnchor(null)
  }

  if (query.isError) {
    return <ErrorState title="Unable to load automation" description="The automation details could not be retrieved." onRetry={() => void query.refetch()} />
  }

  if (query.isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    )
  }

  if (!workflow) {
    return (
      <EmptyState
        title="Automation not found"
        description="It may have been removed, or you may not have access to it."
        actionLabel="Back to automations"
        onAction={() => navigate('/automations')}
      />
    )
  }

  const nextVersion = String(workflow.version + 1)

  return (
    <Box>
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1}
        sx={{ alignItems: { xs: 'stretch', md: 'center' }, justifyContent: 'space-between', mb: 2 }}
      >
        <Button component={RouterLink} to="/automations" startIcon={<ArrowBackIcon />} size="small" sx={{ alignSelf: 'flex-start' }}>
          Automations
        </Button>
        <Stack direction="row" spacing={0} sx={{ flexWrap: 'wrap', gap: 1, justifyContent: { xs: 'flex-start', md: 'flex-end' } }}>
          <Button color="error" onClick={() => setDeleteOpen(true)}>
            Delete
          </Button>
          <Button onClick={() => testMutation.mutate()} disabled={testMutation.isPending || isDirty}>
            {testMutation.isPending ? 'Starting…' : 'Test'}
          </Button>
          <Button variant="outlined" disabled={!isDirty || saveMutation.isPending} onClick={() => saveMutation.mutate()}>
            {saveMutation.isPending ? 'Saving…' : 'Save Draft'}
          </Button>
          <Button variant="contained" onClick={() => setPublishOpen(true)}>
            Publish
          </Button>
        </Stack>
      </Stack>

      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          {workflow.name}
        </Typography>
        <Chip
          label={workflow.versionStatus}
          size="small"
          color={workflow.versionStatus === 'Published' ? 'success' : 'default'}
        />
      </Stack>

      {isDirty && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          You have unsaved changes. Save the draft before testing it. Publishing will also require a saved draft.
        </Alert>
      )}

      {testResult && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setTestResult(null)}>
          {testResult}
        </Alert>
      )}

      <Paper variant="outlined" sx={{ p: 3, mb: 2 }}>
        <Typography variant="overline" color="text.secondary">
          How it starts
        </Typography>
        <TextField
          select
          fullWidth
          size="small"
          label="Trigger"
          value={effectiveTrigger ?? ''}
          onChange={(e) => setTrigger(e.target.value || null)}
          slotProps={{ select: { native: true } }}
          sx={{ mt: 1 }}
        >
          <option value="">No trigger set — run manually or via Test</option>
          {KNOWN_TRIGGERS.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </TextField>
      </Paper>

      <Paper variant="outlined" sx={{ p: 3, mb: 2 }}>
        <Typography variant="overline" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
          What happens
        </Typography>
        <Stack spacing={1}>
          {steps.length === 0 && (
            <EmptyState title="No steps yet" description="Add one below to define what this automation does." />
          )}
          {steps.map((step, i) => (
            <Stack
              key={`${step.name}-${i}`}
              direction="row"
              spacing={1.5}
              sx={{ alignItems: 'center', p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}
            >
              <Typography variant="body2" color="text.secondary" sx={{ width: 20 }}>
                {i + 1}
              </Typography>
              <CheckCircleOutlineIcon fontSize="small" sx={{ color: 'text.disabled' }} />
              <ButtonBase
                onClick={() => setEditingStep(step)}
                aria-label={`Edit step ${step.name}`}
                sx={{ flexGrow: 1, justifyContent: 'flex-start', borderRadius: 1 }}
              >
                <Typography variant="body2">{step.name}</Typography>
              </ButtonBase>
              <Chip label={step.type ?? 'Action'} size="small" variant="outlined" />
              <IconButton size="small" aria-label={`Options for step ${step.name}`} onClick={(e) => setMenuAnchor({ el: e.currentTarget, index: i })}>
                <MoreVertIcon fontSize="small" />
              </IconButton>
            </Stack>
          ))}
        </Stack>
        <Button startIcon={<AddIcon />} size="small" sx={{ mt: 1.5 }} onClick={() => setEditingStep(null)}>
          Add Step
        </Button>
      </Paper>

      <Button
        component={RouterLink}
        to={`/automations/${id}/builder`}
        size="small"
        endIcon={<OpenInNewIcon fontSize="small" />}
      >
        Advanced workflow — Open Builder
      </Button>

      <Menu anchorEl={menuAnchor?.el} open={!!menuAnchor} onClose={() => setMenuAnchor(null)}>
        <MenuItem
          disabled={menuAnchor?.index === 0}
          onClick={() => {
            if (menuAnchor) moveStep(menuAnchor.index, -1)
          }}
        >
          Move up
        </MenuItem>
        <MenuItem
          disabled={menuAnchor !== null && menuAnchor.index === steps.length - 1}
          onClick={() => {
            if (menuAnchor) moveStep(menuAnchor.index, 1)
          }}
        >
          Move down
        </MenuItem>
        <MenuItem
          onClick={() => {
            if (menuAnchor) removeStep(menuAnchor.index)
          }}
        >
          Remove
        </MenuItem>
      </Menu>

      <DynamicStepEditorDrawer
        open={editingStep !== undefined}
        onClose={() => setEditingStep(undefined)}
        step={editingStep ?? null}
        otherStepNames={otherStepNames}
        onSave={handleStepSave}
        onDelete={
          editingStep
            ? () => {
                updateSteps(steps.filter((s) => s !== editingStep))
                setEditingStep(undefined)
              }
            : undefined
        }
      />

      <PublishChecklist
        open={publishOpen}
        onClose={() => setPublishOpen(false)}
        onPublish={() => publishMutation.mutate()}
        title={workflow.name}
        version={nextVersion}
        isPublishing={publishMutation.isPending}
        checks={[
          { label: 'Name configured', ready: !!workflow.name.trim() },
          { label: 'At least one step', ready: steps.length > 0 },
          { label: 'No unsaved changes', ready: !isDirty, detail: isDirty ? 'Save Draft first' : undefined },
        ]}
      />

      <ConfirmDeleteDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
        title="Delete Automation"
        message={`Delete "${workflow.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
