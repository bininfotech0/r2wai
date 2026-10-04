import { queryKeys } from '../../../lib/api/queryKeys'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Box, Chip, Grid, MenuItem, Paper, Skeleton, Stack, Tab, Tabs, TextField, Typography } from '@mui/material'
import ScienceOutlinedIcon from '@mui/icons-material/ScienceOutlined'
import BoltOutlinedIcon from '@mui/icons-material/BoltOutlined'
import { LivePreviewPane } from '../../assistants/components/LivePreviewPane'
import { listAssistants } from '../../assistants/api'
import { listWorkflows } from '../../automations/api'
import { listCapabilities } from '../../tools/api'
import { AutomationPlaygroundPane } from '../components/AutomationPlaygroundPane'
import { CapabilityPlaygroundPane } from '../components/CapabilityPlaygroundPane'
import { ExecutionInspector, type PlaygroundMode } from '../components/ExecutionInspector'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { describeApiError } from '../../../lib/api/fetchJson'
import { reduceToolCallSteps } from '../../../lib/chat/toolCallProgress'
import type { ToolCallProgressEvent } from '../../../lib/chat/types'
import type { WorkflowInstanceDto, WorkflowStepExecutionDto } from '../../runs/types'
import type { TestConnectionResult } from '../../tools/types'

const PANEL_HEIGHT = 600

/**
 * Single 3-panel test console (redesign Phase 6) replacing the old 3-tab, 2-column-per-tab
 * layout: left = what to test (mode + instance picker), center = the live execution itself,
 * right = Execution Inspector watching the same live session. Each mode's picker used to live
 * inside its own tab's pane (duplicated 3 times) — now it's one picker driving all three.
 */
export function PlaygroundPage() {
  const navigate = useNavigate()
  const [mode, setMode] = useState<PlaygroundMode>('Assistant')
  // Only tracks an explicit user pick per mode — the effective selection falls back to the
  // first loaded item, derived during render rather than synced via an effect.
  const [selectedAssistantId, setSelectedAssistantId] = useState<string | null>(null)
  const [selectedWorkflowId, setSelectedWorkflowId] = useState<string | null>(null)
  const [selectedCapabilityId, setSelectedCapabilityId] = useState<string | null>(null)

  const [toolCallEvents, setToolCallEvents] = useState<ToolCallProgressEvent[]>([])
  const [automationInspector, setAutomationInspector] = useState<{
    instance: WorkflowInstanceDto | null
    steps: WorkflowStepExecutionDto[]
    workflowVersion: number | null
    isLoading: boolean
  } | null>(null)
  const [capabilityInspector, setCapabilityInspector] = useState<{
    result: TestConnectionResult | null
    isPending: boolean
    testedAt: string | null
    riskLevel: string | null
    requiredRole: string | null
  } | null>(null)

  const assistantsQuery = useQuery({
    queryKey: queryKeys.assistants.playgroundList,
    queryFn: () => listAssistants(1, 50, ''),
    enabled: mode === 'Assistant',
  })
  const workflowsQuery = useQuery({
    queryKey: ['workflows', 'playground-list'],
    queryFn: () => listWorkflows(1, 50, ''),
    enabled: mode === 'Automation',
  })
  const capabilitiesQuery = useQuery({
    queryKey: ['capabilities', 'playground-list'],
    queryFn: () => listCapabilities(1, 100, ''),
    enabled: mode === 'Capability',
  })

  const assistants = assistantsQuery.data?.items ?? []
  const workflows = workflowsQuery.data?.items ?? []
  const capabilities = capabilitiesQuery.data?.items ?? []

  const assistantId = selectedAssistantId ?? assistants[0]?.id ?? ''
  const workflowId = selectedWorkflowId ?? workflows[0]?.id ?? ''
  const capabilityId = selectedCapabilityId ?? capabilities[0]?.id ?? ''
  const selectedTargetName = mode === 'Assistant'
    ? assistants.find((assistant) => assistant.id === assistantId)?.name
    : mode === 'Automation'
      ? workflows.find((workflow) => workflow.id === workflowId)?.name
      : capabilities.find((capability) => capability.id === capabilityId)?.name
  const targetCount = mode === 'Assistant' ? assistants.length : mode === 'Automation' ? workflows.length : capabilities.length
  const targetLabel = mode === 'Assistant'
    ? (targetCount === 1 ? 'assistant' : 'assistants')
    : mode === 'Automation'
      ? (targetCount === 1 ? 'automation' : 'automations')
      : (targetCount === 1 ? 'tool' : 'tools')

  return (
    <Box sx={{ maxWidth: 1680, mx: 'auto', pb: 3 }}>
      <Paper
        variant="outlined"
        sx={{
          mb: 2.5,
          p: { xs: 2, sm: 2.5, lg: 3 },
          borderRadius: 3,
          overflow: 'hidden',
          background: (theme) => `linear-gradient(112deg, ${theme.palette.primary.main}12 0%, ${theme.palette.background.paper} 68%)`,
        }}
      >
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Box sx={{ minWidth: 0 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 0.5 }}>
              <Typography variant="overline" color="primary.main" sx={{ fontWeight: 750, letterSpacing: 1.1 }}>TEST WORKSPACE</Typography>
              <Chip icon={<BoltOutlinedIcon />} label="Real API" size="small" color="success" variant="outlined" />
            </Stack>
            <Typography variant="h4" sx={{ fontWeight: 750, letterSpacing: '-0.035em', fontSize: { xs: '1.65rem', sm: '2rem' } }}>Playground</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, maxWidth: 740 }}>
              Run a real conversation, automation, or tool check before you publish.
            </Typography>
          </Box>
          <Box sx={{ display: { xs: 'none', sm: 'grid' }, placeItems: 'center', flex: '0 0 auto', width: 56, height: 56, borderRadius: 2.5, color: 'primary.main', bgcolor: 'action.selected' }}>
            <ScienceOutlinedIcon sx={{ fontSize: 30 }} />
          </Box>
        </Stack>
      </Paper>

      <Grid container spacing={2.25} sx={{ alignItems: 'stretch' }}>
        <Grid size={{ xs: 12, lg: 3, xl: 2 }}>
          <Paper variant="outlined" sx={{ height: '100%', borderRadius: 3, overflow: 'hidden' }}>
            <Tabs
              value={mode}
              onChange={(_, v: PlaygroundMode) => setMode(v)}
              variant="scrollable"
              allowScrollButtonsMobile
              aria-label="Playground mode"
              sx={{
                borderBottom: '1px solid',
                borderColor: 'divider',
                bgcolor: 'action.hover',
                '& .MuiTab-root': { minHeight: 48, fontWeight: 650, textTransform: 'none' },
              }}
            >
              <Tab label="Assistant" value="Assistant" />
              <Tab label="Automation" value="Automation" />
              <Tab label="Tool/API" value="Capability" />
            </Tabs>
            <Box sx={{ p: { xs: 1.75, sm: 2 } }}>
              <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 1.5 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Choose a target</Typography>
                {!assistantsQuery.isLoading && !workflowsQuery.isLoading && !capabilitiesQuery.isLoading && (
                  <Chip size="small" variant="outlined" label={`${targetCount} ${targetLabel}`} />
                )}
              </Stack>
              {mode === 'Assistant' && assistantsQuery.isLoading && (
                <Stack spacing={1} role="status" aria-label="Loading assistants">
                  <Skeleton variant="text" width={96} />
                  <Skeleton variant="rounded" height={40} />
                </Stack>
              )}
              {mode === 'Assistant' && assistantsQuery.isError && (
                <ErrorState
                  {...describeApiError(assistantsQuery.error, "Couldn't load assistants")}
                  onRetry={() => void assistantsQuery.refetch()}
                />
              )}
              {mode === 'Assistant' && !assistantsQuery.isLoading && !assistantsQuery.isError && assistants.length > 0 && (
                <TextField
                  select
                  fullWidth
                  size="small"
                  label="Assistant"
                  value={assistantId}
                  onChange={(e) => setSelectedAssistantId(e.target.value)}
                  sx={{ '& .MuiOutlinedInput-root': { borderRadius: 2 } }}
                >
                  {assistants.map((a) => (
                    <MenuItem key={a.id} value={a.id}>
                      {a.name}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              {mode === 'Automation' && workflowsQuery.isLoading && (
                <Stack spacing={1} role="status" aria-label="Loading automations">
                  <Skeleton variant="text" width={96} />
                  <Skeleton variant="rounded" height={40} />
                </Stack>
              )}
              {mode === 'Automation' && workflowsQuery.isError && (
                <ErrorState
                  {...describeApiError(workflowsQuery.error, "Couldn't load automations")}
                  onRetry={() => void workflowsQuery.refetch()}
                />
              )}
              {mode === 'Automation' && !workflowsQuery.isLoading && !workflowsQuery.isError && workflows.length > 0 && (
                <TextField
                  select
                  fullWidth
                  size="small"
                  label="Automation"
                  value={workflowId}
                  onChange={(e) => setSelectedWorkflowId(e.target.value)}
                  sx={{ '& .MuiOutlinedInput-root': { borderRadius: 2 } }}
                >
                  {workflows.map((w) => (
                    <MenuItem key={w.id} value={w.id}>
                      {w.name}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              {mode === 'Capability' && capabilitiesQuery.isLoading && (
                <Stack spacing={1} role="status" aria-label="Loading tools">
                  <Skeleton variant="text" width={96} />
                  <Skeleton variant="rounded" height={40} />
                </Stack>
              )}
              {mode === 'Capability' && capabilitiesQuery.isError && (
                <ErrorState
                  {...describeApiError(capabilitiesQuery.error, "Couldn't load tools")}
                  onRetry={() => void capabilitiesQuery.refetch()}
                />
              )}
              {mode === 'Capability' && !capabilitiesQuery.isLoading && !capabilitiesQuery.isError && capabilities.length > 0 && (
                <TextField
                  select
                  fullWidth
                  size="small"
                  label="Tool / API"
                  value={capabilityId}
                  onChange={(e) => setSelectedCapabilityId(e.target.value)}
                  sx={{ '& .MuiOutlinedInput-root': { borderRadius: 2 } }}
                >
                  {capabilities.map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.name}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              {mode === 'Assistant' && assistants.length === 0 && !assistantsQuery.isLoading && !assistantsQuery.isError && (
                <EmptyState
                  title="No assistants yet"
                  description="Create an assistant to start testing conversations here."
                  actionLabel="Go to assistants"
                  onAction={() => navigate('/assistants')}
                />
              )}
              {mode === 'Automation' && workflows.length === 0 && !workflowsQuery.isLoading && !workflowsQuery.isError && (
                <EmptyState
                  title="No automations yet"
                  description="Create an automation to run a test from the Playground."
                  actionLabel="Go to automations"
                  onAction={() => navigate('/automations')}
                />
              )}
              {mode === 'Capability' && capabilities.length === 0 && !capabilitiesQuery.isLoading && !capabilitiesQuery.isError && (
                <EmptyState
                  title="No tools yet"
                  description="Add a capability or API before testing it here."
                  actionLabel="Go to tools"
                  onAction={() => navigate('/tools')}
                />
              )}
              {selectedTargetName && (
                <Box sx={{ mt: 2, p: 1.5, borderRadius: 2, bgcolor: 'action.hover', border: '1px solid', borderColor: 'divider' }}>
                  <Typography variant="caption" color="text.secondary">CURRENTLY TESTING</Typography>
                  <Typography variant="body2" sx={{ mt: 0.25, fontWeight: 650, overflowWrap: 'anywhere' }}>{selectedTargetName}</Typography>
                </Box>
              )}
            </Box>
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, lg: 6, xl: 7 }}>
          <Paper variant="outlined" sx={{ height: { xs: 480, sm: PANEL_HEIGHT }, minWidth: 0, display: 'flex', flexDirection: 'column', borderRadius: 3, overflow: 'hidden' }}>
            {mode === 'Assistant' &&
              (assistantId ? (
                <LivePreviewPane
                  key={assistantId}
                  assistantId={assistantId}
                  onToolCallEvents={setToolCallEvents}
                />
              ) : (
                <Typography variant="body2" color="text.secondary" sx={{ p: 3 }}>
                  {assistantsQuery.isError
                    ? 'Assistant data could not be loaded. Retry from the selector panel.'
                    : assistantsQuery.isLoading
                      ? 'Loading assistants…'
                      : 'Choose an assistant to test a conversation.'}
                </Typography>
              ))}
            {mode === 'Automation' &&
              (workflowId ? (
                <AutomationPlaygroundPane
                  key={workflowId}
                  workflowId={workflowId}
                  onInspectorData={setAutomationInspector}
                />
              ) : (
                <Typography variant="body2" color="text.secondary" sx={{ p: 3 }}>
                  {workflowsQuery.isError
                    ? 'Automation data could not be loaded. Retry from the selector panel.'
                    : workflowsQuery.isLoading
                      ? 'Loading automations…'
                      : 'Choose an automation to run a test.'}
                </Typography>
              ))}
            {mode === 'Capability' &&
              (capabilityId ? (
                <CapabilityPlaygroundPane
                  key={capabilityId}
                  capabilityId={capabilityId}
                  onInspectorData={setCapabilityInspector}
                />
              ) : (
                <Typography variant="body2" color="text.secondary" sx={{ p: 3 }}>
                  {capabilitiesQuery.isError
                    ? 'Tool data could not be loaded. Retry from the selector panel.'
                    : capabilitiesQuery.isLoading
                      ? 'Loading tools…'
                      : 'Choose a tool to run a connection test.'}
                </Typography>
              ))}
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, lg: 3, xl: 3 }}>
          <Paper variant="outlined" sx={{ height: { xs: 420, sm: PANEL_HEIGHT }, minWidth: 0, display: 'flex', flexDirection: 'column', borderRadius: 3, overflow: 'hidden' }}>
            <ExecutionInspector
              mode={mode}
              assistant={{ toolCallSteps: reduceToolCallSteps(toolCallEvents) }}
              automation={automationInspector ?? undefined}
              capability={capabilityInspector ?? undefined}
            />
          </Paper>
        </Grid>
      </Grid>
    </Box>
  )
}
