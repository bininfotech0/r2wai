import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Alert, Button, Chip, CircularProgress, Stack, Typography } from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import { executeWorkflow, getWorkflow } from '../../automations/api'
import { getWorkflowInstance, getWorkflowInstanceSteps } from '../../runs/api'
import type { WorkflowInstanceDto, WorkflowStepExecutionDto } from '../../runs/types'

const TERMINAL_STATUSES = new Set(['Completed', 'Failed', 'Cancelled'])
const STATUS_COLOR: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Completed: 'success',
  Running: 'warning',
  Pending: 'default',
  Failed: 'error',
}

interface AutomationPlaygroundPaneProps {
  workflowId: string
  onInspectorData?: (data: {
    instance: WorkflowInstanceDto | null
    steps: WorkflowStepExecutionDto[]
    workflowVersion: number | null
    isLoading: boolean
  }) => void
}

/** Center panel for Automation mode (redesign Phase 6) — picker now lives in Playground's shared left panel. */
export function AutomationPlaygroundPane({ workflowId, onInspectorData }: AutomationPlaygroundPaneProps) {
  const [instanceId, setInstanceId] = useState<string | null>(null)

  useEffect(() => setInstanceId(null), [workflowId])

  const executeMutation = useMutation({
    mutationFn: () => executeWorkflow(workflowId),
    onSuccess: (result) => setInstanceId(result.instanceId),
  })

  const instanceQuery = useQuery({
    queryKey: ['playground-instance', instanceId],
    queryFn: () => getWorkflowInstance(instanceId!),
    enabled: !!instanceId,
    refetchInterval: (query) => (query.state.data && TERMINAL_STATUSES.has(query.state.data.status) ? false : 2000),
  })
  const stepsQuery = useQuery({
    queryKey: ['playground-instance-steps', instanceId],
    queryFn: () => getWorkflowInstanceSteps(instanceId!),
    enabled: !!instanceId,
    refetchInterval: () =>
      instanceQuery.data && TERMINAL_STATUSES.has(instanceQuery.data.status) ? false : 2000,
  })
  const workflowQuery = useQuery({
    queryKey: ['playground-workflow', workflowId],
    queryFn: () => getWorkflow(workflowId),
    enabled: !!workflowId,
  })

  useEffect(() => {
    onInspectorData?.({
      instance: instanceQuery.data ?? null,
      steps: stepsQuery.data?.items ?? [],
      workflowVersion: workflowQuery.data?.version ?? null,
      isLoading: instanceQuery.isLoading || stepsQuery.isLoading,
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [instanceQuery.data, stepsQuery.data, workflowQuery.data])

  return (
    <Stack sx={{ height: '100%' }}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ p: 1.5, alignItems: 'center', borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
          Automation Test
        </Typography>
        <Button
          size="small"
          variant="contained"
          startIcon={<PlayArrowIcon />}
          disabled={!workflowId || executeMutation.isPending}
          onClick={() => executeMutation.mutate()}
        >
          {executeMutation.isPending ? 'Starting…' : 'Run Test'}
        </Button>
      </Stack>

      <Stack sx={{ flexGrow: 1, p: 2, overflowY: 'auto' }} spacing={2}>
        {executeMutation.isError && <Alert severity="error">Failed to start the run.</Alert>}
        {!instanceId ? (
          <Typography variant="body2" color="text.secondary">
            Run an automation to see its live step-by-step execution here. Status updates by polling — there's no
            real-time push yet (StatusHub exists but nothing in workflow execution calls it).
          </Typography>
        ) : (
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Typography variant="body2">Instance {instanceId.slice(0, 8)}…</Typography>
            {instanceQuery.data && (
              <Chip label={instanceQuery.data.status} size="small" color={STATUS_COLOR[instanceQuery.data.status] ?? 'default'} variant="outlined" />
            )}
            {instanceQuery.data && !TERMINAL_STATUSES.has(instanceQuery.data.status) && <CircularProgress size={16} />}
          </Stack>
        )}
      </Stack>
    </Stack>
  )
}
