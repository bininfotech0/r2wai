import { useQuery } from '@tanstack/react-query'
import { Box, Chip, Grid, Paper, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'
import PlayCircleOutlineOutlined from '@mui/icons-material/PlayCircleOutlineOutlined'
import TrendingUpOutlined from '@mui/icons-material/TrendingUpOutlined'
import SpeedOutlined from '@mui/icons-material/SpeedOutlined'
import BoltOutlined from '@mui/icons-material/BoltOutlined'
import ErrorOutlineOutlined from '@mui/icons-material/ErrorOutlineOutlined'
import FactCheckOutlined from '@mui/icons-material/FactCheckOutlined'
import { StatCard } from '../../../components/StatCard'
import { TrendLineChart } from '../../../components/charts/TrendLineChart'
import { DonutChart } from '../../../components/charts/DonutChart'
import { getDailyTrend, getMetrics } from '../api'
import { listPendingApprovals } from '../../approvals/api'
import { listIntegrations } from '../../integrations/api'
import { listKnowledgeBases } from '../../knowledge/api'
import { listModels } from '../../admin/api'

// Aggregated from each area's own existing status fields — no new tracking. Bounded fetch
// (matches the pattern already used by Runs/Approvals' own client-side merges): a health
// summary needs every row to count, not one page of them, and tenant scale here is small.
const HEALTH_FETCH_SIZE = 200

function HealthRow({
  to,
  label,
  segments,
  emptyLabel,
}: {
  to: string
  label: string
  segments: { label: string; count: number; color: 'success' | 'error' | 'default' | 'warning' }[]
  emptyLabel: string
}) {
  const total = segments.reduce((sum, s) => sum + s.count, 0)
  return (
    <Stack
      component={RouterLink}
      to={to}
      direction="row"
      spacing={1.5}
      sx={{ alignItems: 'center', justifyContent: 'space-between', textDecoration: 'none', color: 'inherit', py: 0.75 }}
    >
      <Typography variant="body2">{label}</Typography>
      <Stack direction="row" spacing={0.75}>
        {total === 0 ? (
          <Typography variant="caption" color="text.secondary">
            {emptyLabel}
          </Typography>
        ) : (
          segments
            .filter((s) => s.count > 0)
            .map((s) => <Chip key={s.label} label={`${s.count} ${s.label}`} size="small" color={s.color} variant="outlined" />)
        )}
      </Stack>
    </Stack>
  )
}

export function OverviewTab() {
  const metricsQuery = useQuery({ queryKey: ['monitor', 'metrics'], queryFn: getMetrics })
  const trendQuery = useQuery({ queryKey: ['monitor', 'daily-trend'], queryFn: () => getDailyTrend(14) })
  const approvalsQuery = useQuery({ queryKey: ['monitor', 'approval-backlog'], queryFn: () => listPendingApprovals(1, HEALTH_FETCH_SIZE) })
  const integrationsQuery = useQuery({ queryKey: ['monitor', 'integrations-health'], queryFn: () => listIntegrations(1, HEALTH_FETCH_SIZE, '') })
  const knowledgeQuery = useQuery({ queryKey: ['monitor', 'knowledge-health'], queryFn: () => listKnowledgeBases(1, HEALTH_FETCH_SIZE, '') })
  const modelsQuery = useQuery({ queryKey: ['monitor', 'models-health'], queryFn: listModels })

  const metrics = metricsQuery.data
  const trendData = (trendQuery.data?.days ?? []).map((d) => ({
    label: new Date(d.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
    value: d.runs,
  }))

  const errorTotal = (metrics?.apiErrors ?? 0) + (metrics?.aiErrors ?? 0) + (metrics?.workflowErrors ?? 0)

  const integrations = integrationsQuery.data?.items ?? []
  const integrationsHealth = [
    { label: 'Connected', count: integrations.filter((i) => i.lastTestStatus === 'Connected').length, color: 'success' as const },
    { label: 'Error', count: integrations.filter((i) => i.lastTestStatus === 'Error').length, color: 'error' as const },
    { label: 'Not tested', count: integrations.filter((i) => !i.lastTestStatus).length, color: 'default' as const },
  ]

  const knowledgeBases = knowledgeQuery.data?.items ?? []
  const knowledgeHealth = [
    { label: 'Active', count: knowledgeBases.filter((k) => k.status === 'Active').length, color: 'success' as const },
    { label: 'Failed', count: knowledgeBases.filter((k) => k.status === 'Failed').length, color: 'error' as const },
    { label: 'Creating', count: knowledgeBases.filter((k) => k.status === 'Creating').length, color: 'warning' as const },
  ]

  const models = modelsQuery.data?.items ?? []
  const modelsHealth = [
    { label: 'Active', count: models.filter((m) => m.isActive).length, color: 'success' as const },
    { label: 'Inactive', count: models.filter((m) => !m.isActive).length, color: 'default' as const },
  ]

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={2} sx={{ flexWrap: 'wrap' }}>
        <StatCard
          label="Executions Today"
          value={metrics?.completedToday ?? 0}
          icon={PlayCircleOutlineOutlined}
          color="info"
          loading={metricsQuery.isLoading}
        />
        <StatCard
          label="Success Rate"
          value={metrics ? `${metrics.successRate.toFixed(1)}%` : '—'}
          icon={TrendingUpOutlined}
          color="success"
          loading={metricsQuery.isLoading}
        />
        <StatCard
          label="Avg Latency"
          value={metrics ? `${Math.round(metrics.averageLatencyMs)} ms` : '—'}
          icon={SpeedOutlined}
          color="primary"
          loading={metricsQuery.isLoading}
        />
        <StatCard
          label="AI Requests"
          value={metrics?.aiRequests ?? 0}
          icon={BoltOutlined}
          color="secondary"
          loading={metricsQuery.isLoading}
        />
        <StatCard
          label="Total Errors"
          value={metrics ? errorTotal : 0}
          icon={ErrorOutlineOutlined}
          color={errorTotal > 0 ? 'error' : 'success'}
          loading={metricsQuery.isLoading}
        />
        <StatCard
          label="Confirmation Backlog"
          value={approvalsQuery.data?.totalCount ?? 0}
          icon={FactCheckOutlined}
          color={(approvalsQuery.data?.totalCount ?? 0) > 0 ? 'warning' : 'success'}
          loading={approvalsQuery.isLoading}
        />
      </Stack>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
          Platform Health
        </Typography>
        <Stack divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
          <HealthRow to="/integrations" label="Integrations" segments={integrationsHealth} emptyLabel="No integrations yet" />
          <HealthRow to="/knowledge" label="Knowledge Bases" segments={knowledgeHealth} emptyLabel="No knowledge bases yet" />
          <HealthRow to="/models" label="AI Models" segments={modelsHealth} emptyLabel="No models configured yet" />
        </Stack>
      </Paper>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              Run Trend (14 days)
            </Typography>
            {trendData.length > 0 ? (
              <TrendLineChart data={trendData} />
            ) : (
              <Typography variant="body2" color="text.secondary">
                No run data yet.
              </Typography>
            )}
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              Errors by Source
            </Typography>
            {errorTotal > 0 ? (
              <DonutChart
                total={errorTotal}
                centerLabel="Errors"
                segments={[
                  { label: 'API', value: metrics?.apiErrors ?? 0, color: '#f59e0b' },
                  { label: 'AI', value: metrics?.aiErrors ?? 0, color: '#ef4444' },
                  { label: 'Workflow', value: metrics?.workflowErrors ?? 0, color: '#6d28d9' },
                ]}
              />
            ) : (
              <Typography variant="body2" color="text.secondary">
                No errors recorded.
              </Typography>
            )}
          </Paper>
        </Grid>
      </Grid>

      <Box>
        <Typography variant="caption" color="text.secondary">
          Last updated {metrics ? new Date(metrics.timestamp).toLocaleString() : '—'}
        </Typography>
      </Box>
    </Stack>
  )
}
