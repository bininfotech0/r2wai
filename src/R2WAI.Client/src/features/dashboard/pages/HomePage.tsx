import { queryKeys } from '../../../lib/api/queryKeys'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  Box,
  Button,
  Grid,
  Paper,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import CircleIcon from '@mui/icons-material/Circle'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import ForumOutlined from '@mui/icons-material/ForumOutlined'
import PlayCircleOutlineOutlined from '@mui/icons-material/PlayCircleOutlineOutlined'
import TrendingUpOutlined from '@mui/icons-material/TrendingUpOutlined'
import PeopleAltOutlined from '@mui/icons-material/PeopleAltOutlined'
import TimerOutlined from '@mui/icons-material/TimerOutlined'
import BoltOutlined from '@mui/icons-material/BoltOutlined'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlineOutlined'
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined'
import { fetchJson, describeApiError } from '../../../lib/api/fetchJson'
import { StatCard } from '../../../components/StatCard'
import { StatusBadge } from '../../../components/StatusBadge'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { CapabilityFlowDiagram } from '../../../components/CapabilityFlowDiagram'
import { UniversalCreate, type CreateKind } from '../../../components/UniversalCreate'
import { MultiSeriesChart } from '../../../components/charts/MultiSeriesChart'
import { TrendLineChart } from '../../../components/charts/TrendLineChart'
import { OnboardingChecklist } from '../components/OnboardingChecklist'
import { trendSeries } from '../components/trend'
import { CreateAssistantDialog } from '../../assistants/dialogs/CreateAssistantDialog'
import { CreateAutomationDialog } from '../../automations/dialogs/CreateAutomationDialog'
import type {
  AiStatsDto,
  AssistantSummary,
  AssistantUsageSummary,
  DailyTrendPoint,
  HealthStatus,
  MetricsDto,
  PagedResult,
  RecentWorkItem,
  WorkflowSummary,
} from '../types'

function useMetrics() {
  return useQuery({
    queryKey: ['operations', 'metrics'],
    queryFn: () => fetchJson<MetricsDto>('/operations/metrics'),
  })
}

function useHealth() {
  return useQuery({
    queryKey: ['operations', 'health'],
    queryFn: () => fetchJson<HealthStatus>('/operations/health'),
  })
}

function useRecentWork() {
  return useQuery({
    queryKey: ['dashboard', 'recent-work'],
    queryFn: async (): Promise<RecentWorkItem[]> => {
      const [assistants, workflows] = await Promise.all([
        fetchJson<PagedResult<AssistantSummary>>('/assistants?page=1&pageSize=5'),
        fetchJson<PagedResult<WorkflowSummary>>('/workflows?page=1&pageSize=5'),
      ])
      const items: RecentWorkItem[] = [
        ...assistants.items.map((a) => ({
          id: a.id,
          name: a.name,
          type: 'Assistant' as const,
          status: a.publishStatus,
          timestamp: a.createdAt,
        })),
        ...workflows.items.map((w) => ({
          id: w.id,
          name: w.name,
          type: 'Automation' as const,
          status: w.versionStatus,
          timestamp: w.modifiedAt ?? w.createdAt,
        })),
      ]
      return items.sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp)).slice(0, 5)
    },
  })
}

function usePendingApprovalsCount() {
  return useQuery({
    queryKey: ['approvals', 'pending-count'],
    queryFn: () => fetchJson<PagedResult<unknown>>('/approvals/pending?page=1&pageSize=1'),
  })
}

/**
 * 30-day conversation/run trend from /operations/daily-trend. Real recorded activity —
 * the chart plots exactly what the endpoint returns and renders an explicit empty frame
 * when there is none, rather than substituting placeholder points.
 *
 * The series arrives wrapped in a `days` envelope, not as a bare array; `trendSeries`
 * unwraps it. See ../components/trend.
 */
function useDailyTrend() {
  return useQuery({
    queryKey: ['operations', 'daily-trend', 30],
    queryFn: () => fetchJson<{ days?: DailyTrendPoint[] }>('/operations/daily-trend?days=30'),
  })
}

/** Token/response-time rollup used for the usage card. */
function useAiStats() {
  return useQuery({
    queryKey: ['operations', 'ai-stats'],
    queryFn: () => fetchJson<AiStatsDto>('/operations/ai-stats'),
  })
}

/**
 * Published-assistant count and the per-agent usage ranking, both derived from the real
 * assistant list. MetricsDto has no published counter, so this reads publishStatus
 * directly rather than the UI guessing one.
 */
function useAssistantBreakdown() {
  return useQuery({
    queryKey: queryKeys.assistants.dashboardBreakdown,
    queryFn: () => fetchJson<PagedResult<AssistantUsageSummary>>('/assistants?page=1&pageSize=200'),
  })
}

export function HomePage() {
  const navigate = useNavigate()
  const metrics = useMetrics()
  const health = useHealth()
  const recentWork = useRecentWork()
  const pendingApprovals = usePendingApprovalsCount()
  const dailyTrend = useDailyTrend()
  const aiStats = useAiStats()
  const assistantBreakdown = useAssistantBreakdown()
  const [prompt, setPrompt] = useState('')
  const [pickerOpen, setPickerOpen] = useState(false)
  const [createAssistantOpen, setCreateAssistantOpen] = useState(false)
  const [createAutomationOpen, setCreateAutomationOpen] = useState(false)

  // Memoised on the query data rather than `?? []` inline, so the reference is stable
  // across renders and the memos below are not invalidated by an undefined cache.
  const allAssistants = useMemo(
    () => assistantBreakdown.data?.items ?? [],
    [assistantBreakdown.data],
  )
  const publishedCount = allAssistants.filter((a) => a.publishStatus === 'Published').length
  const topAgents = useMemo(
    () =>
      [...allAssistants]
        .sort((a, b) => b.usageCount - a.usageCount)
        .slice(0, 5)
        .map((a) => ({ label: a.name.length > 22 ? `${a.name.slice(0, 22)}…` : a.name, value: a.usageCount })),
    [allAssistants],
  )

  const trendPoints = useMemo(
    () =>
      trendSeries(dailyTrend.data).map((d) => ({
        label: new Date(d.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
        Conversations: d.conversations,
        Executions: d.runs,
      })),
    [dailyTrend.data],
  )

  function handleCreateKind(kind: CreateKind) {
    setPickerOpen(false)
    if (kind === 'assistant') setCreateAssistantOpen(true)
    else if (kind === 'automation') setCreateAutomationOpen(true)
    else if (kind === 'knowledge') navigate('/knowledge')
    else navigate('/integrations')
  }

  return (
    <Stack spacing={3}>
      <Paper
        sx={{
          p: { xs: 2, sm: 3 },
          background: (t) => t.palette.mode === 'dark'
            ? `linear-gradient(115deg, color-mix(in srgb, ${t.palette.primary.main} 14%, ${t.palette.background.paper}), color-mix(in srgb, ${t.palette.secondary.main} 13%, ${t.palette.background.paper}))`
            : `linear-gradient(115deg, color-mix(in srgb, ${t.palette.primary.main} 8%, ${t.palette.background.paper}), color-mix(in srgb, ${t.palette.secondary.main} 10%, ${t.palette.background.paper}))`,
          color: 'text.primary',
          border: '1px solid',
          borderColor: 'divider',
          boxShadow: (t) => t.palette.mode === 'dark' ? 'none' : `0 8px 24px color-mix(in srgb, ${t.palette.primary.main} 7%, transparent)`,
        }}
      >
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
          Good {new Date().getHours() < 12 ? 'morning' : 'afternoon'}
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          What would you like to build or automate?
        </Typography>

        <Box
          component="form"
          onSubmit={(e) => {
            e.preventDefault()
            setPickerOpen(true)
          }}
          sx={{
            display: 'flex',
            flexDirection: { xs: 'column', sm: 'row' },
            gap: 1,
            bgcolor: 'background.paper',
            borderRadius: 2,
            p: 1,
            border: '1px solid',
            borderColor: 'divider',
            boxShadow: (t) => t.palette.mode === 'dark' ? '0 4px 14px rgba(0,0,0,.12)' : '0 4px 14px rgba(15,23,42,.06)',
          }}
        >
          <TextField
            fullWidth
            variant="standard"
            placeholder="Describe what you want R2WAI to create…"
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            slotProps={{ input: { disableUnderline: true, sx: { px: 1 } } }}
          />
          <Button
            type="submit"
            variant="contained"
            startIcon={<AutoAwesomeIcon />}
            sx={{ width: { xs: '100%', sm: 'auto' } }}
          >
            Create
          </Button>
        </Box>

        <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: 'wrap' }}>
          <Button variant="outlined" color="primary" size="small" onClick={() => setCreateAssistantOpen(true)}>
            + Assistant
          </Button>
          <Button variant="outlined" color="primary" size="small" onClick={() => setCreateAutomationOpen(true)}>
            + Automation
          </Button>
          <Button variant="outlined" color="primary" size="small" onClick={() => navigate('/knowledge')}>
            + Knowledge
          </Button>
          <Button variant="outlined" color="primary" size="small" onClick={() => navigate('/integrations')}>
            + Integration
          </Button>
        </Stack>
      </Paper>

      <UniversalCreate open={pickerOpen} onClose={() => setPickerOpen(false)} onSelect={handleCreateKind} />
      {createAssistantOpen && (
        <CreateAssistantDialog
          open={createAssistantOpen}
          initialDescription={prompt}
          onClose={() => setCreateAssistantOpen(false)}
          onCreated={(id) => {
            setCreateAssistantOpen(false)
            setPrompt('')
            navigate(`/assistants/${id}`)
          }}
        />
      )}
      {createAutomationOpen && (
        <CreateAutomationDialog
          open={createAutomationOpen}
          initialDescription={prompt}
          onClose={() => setCreateAutomationOpen(false)}
          onCreated={(id) => {
            setCreateAutomationOpen(false)
            setPrompt('')
            navigate(`/automations/${id}`)
          }}
        />
      )}

      {metrics.isError && (
        <ErrorState
          {...describeApiError(metrics.error, "Couldn't load platform metrics")}
          onRetry={() => void metrics.refetch()}
        />
      )}

      <OnboardingChecklist />

      <Typography variant="h6" component="h2" sx={{ fontWeight: 600, mb: -1 }}>
        At a glance
      </Typography>
      <Stack
        spacing={0}
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: 'repeat(2, minmax(0, 1fr))',
            sm: 'repeat(3, minmax(0, 1fr))',
            lg: 'repeat(4, minmax(0, 1fr))',
            xl: 'repeat(6, minmax(0, 1fr))',
          },
          gap: 2,
        }}
      >
        <StatCard
          label="AI Assistants"
          value={metrics.data?.totalAssistants ?? 0}
          trendPercent={metrics.data?.assistantsTrendPercent}
          icon={SmartToyOutlined}
          color="primary"
          loading={metrics.isLoading}
        />
        <StatCard
          label="Published"
          value={assistantBreakdown.isLoading ? 0 : publishedCount}
          icon={CheckCircleOutlineIcon}
          color="success"
          loading={assistantBreakdown.isLoading}
        />
        <StatCard
          label="Conversations"
          value={metrics.data?.totalConversations ?? 0}
          trendPercent={metrics.data?.conversationsTrendPercent}
          icon={ForumOutlined}
          color="info"
          loading={metrics.isLoading}
        />
        {/* "Successful resolutions" is not a counter the API keeps — successRate is a
            percentage over totalRequests, so this shows the rate rather than inventing a
            resolved-count by multiplying the two. */}
        <StatCard
          label="Success rate"
          value={metrics.data?.successRate != null ? `${metrics.data.successRate.toFixed(1)}%` : '—'}
          icon={TrendingUpOutlined}
          color="success"
          loading={metrics.isLoading}
        />
        <StatCard
          label="Avg response time"
          value={
            aiStats.data?.avgResponseTimeSec != null
              ? `${aiStats.data.avgResponseTimeSec.toFixed(1)}s`
              : '—'
          }
          icon={TimerOutlined}
          color="warning"
          loading={aiStats.isLoading}
        />
        <StatCard
          label="Tokens used"
          value={aiStats.data?.totalTokens?.toLocaleString() ?? '—'}
          icon={BoltOutlined}
          color="secondary"
          loading={aiStats.isLoading}
        />
      </Stack>

      {/* Secondary operational figures the tenant acted on before the brief's KPI list was
          written — kept rather than dropped so the dashboard doesn't lose signal. */}
      <Typography variant="h6" component="h2" sx={{ fontWeight: 600, mb: -1 }}>
        Operations
      </Typography>
      <Stack
        spacing={0}
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: 'repeat(2, minmax(0, 1fr))',
            sm: 'repeat(4, minmax(0, 1fr))',
          },
          gap: 2,
        }}
      >
        <StatCard
          label="Automations"
          value={metrics.data?.activeWorkflows ?? 0}
          trendPercent={metrics.data?.applicationsTrendPercent}
          icon={AccountTreeOutlined}
          color="secondary"
          loading={metrics.isLoading}
        />
        <StatCard
          label="Executions today"
          value={metrics.data?.completedToday ?? 0}
          icon={PlayCircleOutlineOutlined}
          color="info"
          loading={metrics.isLoading}
        />
        <StatCard
          label="Active users"
          value={metrics.data?.activeUsersByLogin ?? 0}
          trendPercent={metrics.data?.activeUsersTrendPercent}
          icon={PeopleAltOutlined}
          color="warning"
          loading={metrics.isLoading}
        />
        <StatCard
          label="Knowledge bases"
          value={metrics.data?.totalKnowledgeBases ?? 0}
          icon={MenuBookOutlined}
          color="primary"
          loading={metrics.isLoading}
        />
      </Stack>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: 'minmax(0, 1fr)',
            md: 'minmax(0, 3fr) minmax(0, 2fr)',
          },
          gap: 2,
        }}
      >
          <Paper variant="outlined" sx={{ p: 2, height: '100%', minWidth: 0 }}>
            <Stack direction="row" sx={{ alignItems: 'baseline', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                Usage
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Last {aiStats.data?.windowDays ?? 30} days
              </Typography>
            </Stack>
            {dailyTrend.isLoading ? (
              <Skeleton variant="rounded" height={240} />
            ) : dailyTrend.error ? (
              <Typography variant="body2" color="text.secondary">
                Usage trend is unavailable right now.
              </Typography>
            ) : (
              <MultiSeriesChart
                data={trendPoints}
                series={[
                  { key: 'Conversations', label: 'Conversations' },
                  { key: 'Executions', label: 'Executions', color: '#0EA5E9' },
                ]}
              />
            )}
          </Paper>
          <Paper variant="outlined" sx={{ p: 2, height: '100%', minWidth: 0 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
              Agent performance
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
              Conversations handled per agent.
            </Typography>
            {assistantBreakdown.isLoading ? (
              <Skeleton variant="rounded" height={240} />
            ) : topAgents.length === 0 || topAgents.every((a) => a.value === 0) ? (
              <Stack sx={{ height: 240, alignItems: 'center', justifyContent: 'center' }}>
                <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
                  No agent has handled a conversation yet.
                  <br />
                  Publish an agent to start collecting usage.
                </Typography>
              </Stack>
            ) : (
              <TrendLineChart data={topAgents} height={240} />
            )}
          </Paper>
      </Box>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: 'minmax(0, 1fr)',
            md: 'minmax(0, 3fr) minmax(0, 2fr)',
          },
          gap: 2,
        }}
      >
          <Paper variant="outlined" sx={{ p: 2, height: '100%', minWidth: 0 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              Recent Work
            </Typography>
            {recentWork.isLoading && (
              <Stack spacing={1.5}>
                {[0, 1, 2].map((i) => (
                  <Stack key={i} direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                    <Skeleton variant="rounded" width={28} height={28} sx={{ borderRadius: 1.5 }} />
                    <Skeleton variant="text" width="60%" />
                  </Stack>
                ))}
              </Stack>
            )}
            {recentWork.data && recentWork.data.length === 0 && (
              <EmptyState
                icon={InboxOutlinedIcon}
                title="Nothing yet"
                description="Create your first assistant or automation to see it here."
              />
            )}
            <Stack spacing={0.5}>
              {recentWork.data?.map((item) => {
                const Icon = item.type === 'Assistant' ? SmartToyOutlined : AccountTreeOutlined
                const target = item.type === 'Assistant' ? `/assistants/${item.id}` : `/automations/${item.id}`
                return (
                  <Stack
                    key={`${item.type}-${item.id}`}
                    direction="row"
                    spacing={1.5}
                    onClick={() => navigate(target)}
                    sx={{
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      px: 1,
                      py: 0.75,
                      mx: -1,
                      borderRadius: 1.5,
                      cursor: 'pointer',
                      transition: 'background-color 120ms ease',
                      '&:hover': { bgcolor: 'action.hover' },
                    }}
                  >
                    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', minWidth: 0 }}>
                      <Box
                        sx={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          width: 28,
                          height: 28,
                          borderRadius: 1.5,
                          flexShrink: 0,
                          bgcolor: 'action.selected',
                          color: 'text.secondary',
                        }}
                      >
                        <Icon sx={{ fontSize: 16 }} />
                      </Box>
                      <Typography variant="body2" noWrap>
                        {item.name}
                      </Typography>
                    </Stack>
                    <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', flexShrink: 0 }}>
                      <StatusBadge status={item.status} />
                      <ChevronRightIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
                    </Stack>
                  </Stack>
                )
              })}
            </Stack>
          </Paper>
          <Paper variant="outlined" sx={{ p: 2, height: '100%', minWidth: 0 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              Needs Attention
            </Typography>
            {pendingApprovals.isLoading ? (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                <Skeleton variant="circular" width={28} height={28} />
                <Skeleton variant="text" width="50%" />
              </Stack>
            ) : pendingApprovals.data?.totalCount ? (
              <Stack
                direction="row"
                spacing={1.5}
                onClick={() => navigate('/approvals')}
                sx={{
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  px: 1,
                  py: 0.75,
                  mx: -1,
                  borderRadius: 1.5,
                  cursor: 'pointer',
                  transition: 'background-color 120ms ease',
                  '&:hover': { bgcolor: 'action.hover' },
                }}
              >
                <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                  <Box
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      width: 28,
                      height: 28,
                      borderRadius: '50%',
                      bgcolor: (t) => (t.palette.mode === 'dark' ? 'warning.dark' : '#FEF3C7'),
                      color: 'warning.main',
                    }}
                  >
                    <CircleIcon sx={{ fontSize: 10 }} />
                  </Box>
                  <Typography variant="body2">
                    {pendingApprovals.data.totalCount} confirmation
                    {pendingApprovals.data.totalCount === 1 ? '' : 's'} waiting on you
                  </Typography>
                </Stack>
                <ChevronRightIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
              </Stack>
            ) : (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', color: 'text.secondary' }}>
                <CheckCircleOutlineIcon sx={{ fontSize: 20, color: 'success.main' }} />
                <Typography variant="body2" color="text.secondary">
                  All caught up — no approvals waiting.
                </Typography>
              </Stack>
            )}
          </Paper>
      </Box>

      <Grid container spacing={2}>
        <Grid size={12}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <CircleIcon
              sx={{ fontSize: 10, color: health.data?.status === 'healthy' ? 'success.main' : 'error.main' }}
            />
            <Typography variant="body2" color="text.secondary">
              Platform {health.data?.status ?? (health.isLoading ? 'checking…' : 'unknown')}
            </Typography>
          </Stack>
        </Grid>

        <Grid size={12}>
          <CapabilityFlowDiagram />
        </Grid>
      </Grid>
    </Stack>
  )
}
