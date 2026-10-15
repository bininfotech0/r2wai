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
  getToolCall,
  getWorkflowInstance,
  getWorkflowInstanceSteps,
  listAuditLogsByEntityType,
  retryFailedStep,
} from '../api'
import type { RunItem, ToolCallDetailDto } from '../types'

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
  const isToolCall = run?.type === 'ToolCall'
  const isConversation = !!run && !isAutomation && !isToolCall

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
    enabled: isConversation,
  })
  const messagesQuery = useQuery({
    queryKey: ['run-messages', run?.id],
    queryFn: () => getMessages(run!.id),
    enabled: isConversation,
  })
  const toolCallQuery = useQuery({
    queryKey: ['run-tool-call', run?.id],
    queryFn: () => getToolCall(run!.id),
    enabled: !!run && isToolCall,
  })

  const auditQuery = useQuery({
    queryKey: ['run-audit', run?.type, run?.id],
    queryFn: () => listAuditLogsByEntityType(isAutomation ? 'WorkflowInstance' : 'Conversation'),
    enabled: !!run && !isToolCall,
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
    if (isToolCall) {
      // A tool call's audit trail lives on its ToolDefinition (execution/denial rows).
      const toolId = toolCallQuery.data?.toolDefinitionId
      navigate(toolId ? `/monitor?tab=audit&entityType=ToolDefinition&entityId=${toolId}` : '/monitor?tab=audit&entityType=ToolDefinition')
      return
    }
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
              <Chip label={isToolCall ? 'Tool call' : run.type} size="small" variant="outlined" />
              <Chip label={run.status} size="small" variant="outlined" />
            </Stack>

            <Divider />

            {isToolCall ? (
              <ToolCallDetails
                detail={toolCallQuery.data}
                loading={toolCallQuery.isLoading}
                failed={toolCallQuery.isError}
                onOpenConfirmation={() => navigate('/approvals')}
              />
            ) : isAutomation ? (
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
                      <Button size="small" onClick={() => retryMutation.mutate()} disabled={retryMutation.isPending} loading={retryMutation.isPending}>
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
            {showTechnical && isToolCall && (
              <Stack spacing={0.75}>
                <TechnicalRow label="Execution ID" value={run.id} />
                <TechnicalRow label="Source" value={toolCallQuery.data?.plugin ?? '—'} />
                <TechnicalRow label="Function" value={toolCallQuery.data?.function ?? '—'} />
                <TechnicalRow label="Confirmation ID" value={toolCallQuery.data?.approvalRequestId ?? '—'} />
              </Stack>
            )}
            {showTechnical && !isToolCall && (
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

            {isConversation && conversationQuery.data?.module && (
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

const TOOL_CALL_SUMMARY: Record<ToolCallDetailDto['status'], string> = {
  Succeeded: 'The tool ran and completed.',
  Failed: 'The tool ran but returned an error.',
  Denied: 'Governance blocked this call before it ran.',
  AwaitingApproval: 'Paused until someone approves the confirmation.',
  Prepared: 'Started but never recorded a result — the server may have stopped mid-call.',
  Unknown: 'Timed out or was cancelled after sending — the target may or may not have acted.',
}

function ToolCallDetails({ detail, loading, failed, onOpenConfirmation }: {
  detail: ToolCallDetailDto | undefined
  loading: boolean
  failed: boolean
  onOpenConfirmation: () => void
}) {
  if (loading) return <CircularProgress size={20} />
  if (failed || !detail) return <Alert severity="error">This tool call's details could not be loaded.</Alert>

  return (
    <Stack spacing={1.25}>
      <Typography variant="subtitle2">Outcome</Typography>
      <Typography variant="body2">{TOOL_CALL_SUMMARY[detail.status]}</Typography>
      {detail.denialReason && <Alert severity="warning">Reason: {detail.denialReason}</Alert>}
      {detail.error && <Alert severity="error" sx={{ wordBreak: 'break-word' }}>{detail.error}</Alert>}
      <Stack spacing={0.75}>
        <TechnicalRow label="Tool" value={detail.toolName ?? detail.function} />
        <TechnicalRow label="Requested by" value={detail.userName ?? '—'} />
        <TechnicalRow label="Started" value={new Date(detail.startedAt).toLocaleString()} />
        <TechnicalRow label="Duration" value={detail.durationMs != null ? `${detail.durationMs} ms` : '—'} />
      </Stack>
      {detail.approvalRequestId && (
        <Box>
          <Button size="small" variant="outlined" onClick={onOpenConfirmation}>
            Open confirmations
          </Button>
        </Box>
      )}
    </Stack>
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
