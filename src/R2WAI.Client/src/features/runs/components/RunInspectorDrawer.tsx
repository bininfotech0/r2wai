import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Drawer,
  Link,
  Stack,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import { useNavigate } from 'react-router-dom'
import { getWorkflow } from '../../automations/api'
import { Timeline } from '../../../components/Timeline'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import {
  getConversation,
  getMessages,
  getWorkflowInstance,
  getWorkflowInstanceSteps,
  listAuditLogsByEntityType,
  retryFailedStep,
} from '../api'
import type { RunItem } from '../types'

interface RunInspectorDrawerProps {
  run: RunItem | null
  onClose: () => void
}

export function RunInspectorDrawer({ run, onClose }: RunInspectorDrawerProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [showTechnical, setShowTechnical] = useState(false)

  const isAutomation = run?.type === 'Automation'

  const instanceQuery = useQuery({
    queryKey: ['run-instance', run?.id],
    queryFn: () => getWorkflowInstance(run!.id),
    enabled: !!run && isAutomation,
  })
  const stepsQuery = useQuery({
    queryKey: ['run-instance-steps', run?.id],
    queryFn: () => getWorkflowInstanceSteps(run!.id),
    enabled: !!run && isAutomation,
  })
  const workflowQuery = useQuery({
    queryKey: ['run-workflow', instanceQuery.data?.workflowId],
    queryFn: () => getWorkflow(instanceQuery.data!.workflowId),
    enabled: !!instanceQuery.data,
  })

  const conversationQuery = useQuery({
    queryKey: ['run-conversation', run?.id],
    queryFn: () => getConversation(run!.id),
    enabled: !!run && !isAutomation,
  })
  const messagesQuery = useQuery({
    queryKey: ['run-messages', run?.id],
    queryFn: () => getMessages(run!.id),
    enabled: !!run && !isAutomation,
  })

  const auditQuery = useQuery({
    queryKey: ['run-audit', run?.type, run?.id],
    queryFn: () => listAuditLogsByEntityType(isAutomation ? 'WorkflowInstance' : 'Conversation'),
    enabled: !!run,
  })
  const correlationId = auditQuery.data?.items.find((a) => a.entityId === run?.id)?.correlationId ?? null

  const retryMutation = useMutation({
    mutationFn: () => retryFailedStep(run!.id),
    onSuccess: () => {
      notify('Failed step retried', 'success')
      void queryClient.invalidateQueries({ queryKey: ['run-instance', run?.id] })
      void queryClient.invalidateQueries({ queryKey: ['run-instance-steps', run?.id] })
    },
    onError: () => notify('Retry failed', 'error'),
  })

  const hasFailedStep = (stepsQuery.data?.items ?? []).some((s) => s.status === 'Failed')

  function handleViewAudit() {
    navigate(`/monitor?tab=audit&entityType=${isAutomation ? 'WorkflowInstance' : 'Conversation'}&entityId=${run?.id}`)
  }

  return (
    <Drawer anchor="right" open={!!run} onClose={onClose}>
      <Box sx={{ width: 440, p: 2.5 }} role="presentation">
        {!run ? null : (
          <Stack spacing={2}>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography variant="h6" sx={{ fontWeight: 600 }}>
                {run.name}
              </Typography>
              <IconButton size="small" onClick={onClose} aria-label="Close">
                <CloseIcon fontSize="small" />
              </IconButton>
            </Stack>
            <Stack direction="row" spacing={1}>
              <Chip label={run.type} size="small" variant="outlined" />
              <Chip label={run.status} size="small" variant="outlined" />
            </Stack>

            <Divider />

            {isAutomation ? (
              <>
                <Typography variant="subtitle2">Timeline</Typography>
                {stepsQuery.isLoading && <CircularProgress size={20} />}
                <Timeline
                  emptyMessage="No step executions recorded yet."
                  steps={(stepsQuery.data?.items ?? []).map((step) => ({
                    id: step.id,
                    label: step.stepName,
                    description: step.stepType,
                    status: step.status,
                    timestamp: step.startedAt ?? undefined,
                    durationSeconds: step.startedAt && step.completedAt
                      ? Math.max(0, Math.round((Date.parse(step.completedAt) - Date.parse(step.startedAt)) / 1000))
                      : undefined,
                    error: step.error ?? undefined,
                  }))}
                />

                {hasFailedStep && (
                  <Alert
                    severity="warning"
                    action={
                      <Button size="small" onClick={() => retryMutation.mutate()} disabled={retryMutation.isPending}>
                        {retryMutation.isPending ? 'Retrying…' : 'Retry'}
                      </Button>
                    }
                  >
                    This run has a failed step.
                  </Alert>
                )}
              </>
            ) : (
              <>
                <Typography variant="subtitle2">Transcript</Typography>
                {messagesQuery.isLoading && <CircularProgress size={20} />}
                <Stack spacing={1}>
                  {(messagesQuery.data?.items ?? []).map((m) => (
                    <Box key={m.id}>
                      <Typography variant="caption" color="text.secondary">
                        {m.role} · {new Date(m.createdAt).toLocaleString()}
                      </Typography>
                      <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                        {m.content}
                      </Typography>
                    </Box>
                  ))}
                  {(messagesQuery.data?.items ?? []).length === 0 && !messagesQuery.isLoading && (
                    <Typography variant="body2" color="text.secondary">
                      No messages.
                    </Typography>
                  )}
                </Stack>
              </>
            )}

            <Divider />

            <Stack direction="row" spacing={2}>
              <Button size="small" onClick={handleViewAudit}>
                View Audit
              </Button>
            </Stack>

            <Divider />

            <Link
              component="button"
              variant="body2"
              underline="hover"
              onClick={() => setShowTechnical((v) => !v)}
              sx={{ display: 'flex', alignItems: 'center', gap: 0.5, textAlign: 'left' }}
            >
              <ExpandMoreIcon fontSize="small" sx={{ transform: showTechnical ? 'rotate(180deg)' : 'none' }} />
              Technical details
            </Link>
            {showTechnical && (
              <Stack spacing={0.75}>
                <TechnicalRow label="Correlation ID" value={correlationId ?? '—'} />
                <TechnicalRow label="Runtime" value="Semantic Kernel" />
                <TechnicalRow label="Workflow Runtime" value={isAutomation ? 'Elsa' : '—'} />
                <TechnicalRow
                  label={isAutomation ? 'Automation Version' : 'Assistant Version'}
                  value={isAutomation ? (workflowQuery.data ? String(workflowQuery.data.version) : '—') : '—'}
                />
                <TechnicalRow label="Model" value="—" />
                <TechnicalRow label="Tokens" value="—" />
                <TechnicalRow label="Tool Calls" value="—" />
                <TechnicalRow label="Execution Node" value="—" />
                <Typography variant="caption" color="text.secondary">
                  Model, Tokens, Tool Calls, and Execution Node aren't persisted by the backend yet — shown as
                  unavailable rather than guessed.
                </Typography>
              </Stack>
            )}

            {!isAutomation && conversationQuery.data?.module && (
              <Typography variant="caption" color="text.secondary">
                Module: {conversationQuery.data.module}
              </Typography>
            )}
          </Stack>
        )}
      </Box>
    </Drawer>
  )
}

function TechnicalRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="caption">{value}</Typography>
    </Stack>
  )
}
