import { useState } from 'react'
import { Alert, Box, CircularProgress, Divider, Link, Stack, Typography } from '@mui/material'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import QueryStatsOutlinedIcon from '@mui/icons-material/QueryStatsOutlined'
import { Timeline, type TimelineStep } from '../../../components/Timeline'
import type { WorkflowInstanceDto, WorkflowStepExecutionDto } from '../../runs/types'
import type { TestConnectionResult } from '../../tools/types'

export type PlaygroundMode = 'Assistant' | 'Automation' | 'Capability'

interface AssistantInspectorData {
  toolCallSteps: TimelineStep[]
}

interface AutomationInspectorData {
  instance: WorkflowInstanceDto | null
  steps: WorkflowStepExecutionDto[]
  workflowVersion: number | null
  isLoading: boolean
}

interface CapabilityInspectorData {
  result: TestConnectionResult | null
  isPending: boolean
  testedAt: string | null
  riskLevel: string | null
  requiredRole: string | null
}

interface ExecutionInspectorProps {
  mode: PlaygroundMode
  assistant?: AssistantInspectorData
  automation?: AutomationInspectorData
  capability?: CapabilityInspectorData
}

/**
 * Right panel of the unified Playground (redesign Phase 6). Unlike RunInspectorDrawer
 * (which reconstructs a *past* run from persisted data, so tool calls always show "—" —
 * they're never stored), this watches a *live* test session, so Assistant mode's tool
 * calls are real, current data straight from Phase 2's streaming events — not fabricated,
 * just genuinely available here in a way a historical view can't offer.
 */
export function ExecutionInspector({ mode, assistant, automation, capability }: ExecutionInspectorProps) {
  const [showTechnical, setShowTechnical] = useState(false)

  return (
    <Stack sx={{ height: '100%' }}>
      <Box sx={{ p: 1.75, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'background.default' }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <Box sx={{ display: 'grid', placeItems: 'center', width: 34, height: 34, borderRadius: 1.5, bgcolor: 'action.selected', color: 'primary.main' }}>
            <QueryStatsOutlinedIcon fontSize="small" />
          </Box>
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Execution inspector</Typography>
            <Typography variant="caption" color="text.secondary">Live run details</Typography>
          </Box>
        </Stack>
      </Box>
      <Box sx={{ flexGrow: 1, p: 2, overflowY: 'auto' }}>
        <Stack spacing={2}>
          {mode === 'Assistant' && (
            <>
              <Typography variant="subtitle2">Tool calls this turn</Typography>
              <Timeline
                dense
                emptyMessage="No tool calls yet — send a message that needs a tool to see live progress here."
                steps={assistant?.toolCallSteps ?? []}
              />
            </>
          )}

          {mode === 'Automation' && (
            <>
              <Typography variant="subtitle2">Timeline</Typography>
              {automation?.isLoading && <CircularProgress size={20} />}
              <Timeline
                emptyMessage="Run the automation to see its live step-by-step execution here."
                steps={(automation?.steps ?? []).map((step) => ({
                  id: step.id,
                  label: step.stepName,
                  description: step.stepType,
                  status: step.status,
                  timestamp: step.startedAt ?? undefined,
                  durationSeconds:
                    step.startedAt && step.completedAt
                      ? Math.max(0, Math.round((Date.parse(step.completedAt) - Date.parse(step.startedAt)) / 1000))
                      : undefined,
                  error: step.error ?? undefined,
                }))}
              />
            </>
          )}

          {mode === 'Capability' && (
            <>
              <Typography variant="subtitle2">Test result</Typography>
              {capability?.isPending && <CircularProgress size={20} />}
              {!capability?.isPending && capability?.result && (
                <Alert severity={capability.result.success ? 'success' : 'warning'}>{capability.result.message}</Alert>
              )}
              {!capability?.isPending && !capability?.result && (
                <Typography variant="body2" color="text.secondary">
                  Run a test to see the real connection result here.
                </Typography>
              )}
              {capability?.testedAt && (
                <Typography variant="caption" color="text.secondary">
                  Last tested {new Date(capability.testedAt).toLocaleString()}
                </Typography>
              )}
            </>
          )}

          <Divider />

          <Link
            component="button"
            variant="body2"
            underline="hover"
            onClick={() => setShowTechnical((v) => !v)}
            sx={{ display: 'flex', alignItems: 'center', gap: 0.5, textAlign: 'left', width: 'fit-content' }}
          >
            <ExpandMoreIcon fontSize="small" sx={{ transform: showTechnical ? 'rotate(180deg)' : 'none' }} />
            Technical details
          </Link>
          {showTechnical && (
            <Stack spacing={0.75}>
              <TechnicalRow label="Correlation ID" value="—" />
              {mode === 'Assistant' && (
                <>
                  <TechnicalRow label="Runtime" value="Semantic Kernel" />
                  <TechnicalRow label="Model" value="—" />
                  <TechnicalRow label="Tokens" value="—" />
                </>
              )}
              {mode === 'Automation' && (
                <>
                  <TechnicalRow label="Workflow Runtime" value="Elsa" />
                  <TechnicalRow
                    label="Automation Version"
                    value={automation?.workflowVersion != null ? String(automation.workflowVersion) : '—'}
                  />
                  <TechnicalRow label="Instance ID" value={automation?.instance?.id ?? '—'} />
                </>
              )}
              {mode === 'Capability' && (
                <>
                  <TechnicalRow label="Runtime" value="DynamicToolExecutor" />
                  <TechnicalRow label="Risk Level" value={capability?.riskLevel ?? '—'} />
                  <TechnicalRow label="Required Role" value={capability?.requiredRole ?? '—'} />
                </>
              )}
              <Typography variant="caption" color="text.secondary">
                Fields marked "—" aren't persisted by the backend yet — shown as unavailable rather than guessed.
              </Typography>
            </Stack>
          )}
        </Stack>
      </Box>
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
