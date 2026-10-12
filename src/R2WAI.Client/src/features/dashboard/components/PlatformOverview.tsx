import { queryKeys } from '../../../lib/api/queryKeys'
import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Box, Paper, Skeleton, Stack, Typography } from '@mui/material'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import ForumOutlined from '@mui/icons-material/ForumOutlined'
import PeopleAltOutlined from '@mui/icons-material/PeopleAltOutlined'
import BoltOutlined from '@mui/icons-material/BoltOutlined'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlineOutlined'
import { fetchJson, describeApiError } from '../../../lib/api/fetchJson'
import { StatCard } from '../../../components/StatCard'
import { ErrorState } from '../../../components/ErrorState'
import { MultiSeriesChart } from '../../../components/charts/MultiSeriesChart'
import { TrendLineChart } from '../../../components/charts/TrendLineChart'
import { trendSeries } from './trend'
import type { AiStatsDto, AssistantUsageSummary, DailyTrendPoint, MetricsDto, PagedResult } from '../types'

const statGrid = (columns: Record<string, string>) => ({
  display: 'grid',
  gridTemplateColumns: columns,
  gap: 2,
})

/**
 * The platform's numbers: KPI tiles (those Monitor's Overview doesn't already show), the 30-day usage trend and per-agent performance. These used
 * to open the Home page; Home is now create-first (as in Copilot Studio), and the figures live here
 * on Activity → Monitor, unchanged. Every tile and chart plots exactly what its endpoint returns.
 */
export function PlatformOverview() {
  const metrics = useQuery({
    queryKey: ['operations', 'metrics'],
    queryFn: () => fetchJson<MetricsDto>('/operations/metrics'),
  })
  const aiStats = useQuery({
    queryKey: ['operations', 'ai-stats'],
    queryFn: () => fetchJson<AiStatsDto>('/operations/ai-stats'),
  })
  // The series arrives wrapped in a `days` envelope; trendSeries unwraps it and renders an explicit
  // empty frame when there is no activity rather than placeholder points.
  const dailyTrend = useQuery({
    queryKey: ['operations', 'daily-trend', 30],
    queryFn: () => fetchJson<{ days?: DailyTrendPoint[] }>('/operations/daily-trend?days=30'),
  })
  // MetricsDto has no published counter, so the published count reads publishStatus directly.
  const assistantBreakdown = useQuery({
    queryKey: queryKeys.assistants.dashboardBreakdown,
    queryFn: () => fetchJson<PagedResult<AssistantUsageSummary>>('/assistants?page=1&pageSize=200'),
  })

  const allAssistants = useMemo(() => assistantBreakdown.data?.items ?? [], [assistantBreakdown.data])
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

  return (
    <Stack spacing={2.5} sx={{ mb: 3 }}>
      {metrics.isError && (
        <ErrorState {...describeApiError(metrics.error, "Couldn't load platform metrics")} onRetry={() => void metrics.refetch()} />
      )}

      {/* Monitor's own Overview already shows executions today, success rate and latency below. */}
      <Box sx={statGrid({ xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(160px, 1fr))' })}>
        <StatCard label="Agents" value={metrics.data?.totalAssistants ?? 0} trendPercent={metrics.data?.assistantsTrendPercent} icon={SmartToyOutlined} color="primary" loading={metrics.isLoading} />
        <StatCard label="Published" value={assistantBreakdown.isLoading ? 0 : publishedCount} icon={CheckCircleOutlineIcon} color="success" loading={assistantBreakdown.isLoading} />
        <StatCard label="Conversations" value={metrics.data?.totalConversations ?? 0} trendPercent={metrics.data?.conversationsTrendPercent} icon={ForumOutlined} color="info" loading={metrics.isLoading} />
        <StatCard label="Tokens used" value={aiStats.data?.totalTokens?.toLocaleString() ?? '—'} icon={BoltOutlined} color="secondary" loading={aiStats.isLoading} />
        <StatCard label="Automations" value={metrics.data?.totalWorkflows ?? 0} icon={AccountTreeOutlined} color="secondary" loading={metrics.isLoading} />
        <StatCard label="Active users" value={metrics.data?.activeUsersByLogin ?? 0} trendPercent={metrics.data?.activeUsersTrendPercent} icon={PeopleAltOutlined} color="warning" loading={metrics.isLoading} />
        <StatCard label="Knowledge bases" value={metrics.data?.totalKnowledgeBases ?? 0} icon={MenuBookOutlined} color="primary" loading={metrics.isLoading} />
      </Box>

      <Box sx={statGrid({ xs: 'minmax(0, 1fr)', md: 'minmax(0, 3fr) minmax(0, 2fr)' })}>
        <Paper variant="outlined" sx={{ p: 2, height: '100%', minWidth: 0 }}>
          <Stack direction="row" sx={{ alignItems: 'baseline', justifyContent: 'space-between', mb: 1 }}>
            <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600 }}>Usage</Typography>
            <Typography variant="caption" color="text.secondary">Last {aiStats.data?.windowDays ?? 30} days</Typography>
          </Stack>
          {dailyTrend.isLoading ? (
            <Skeleton variant="rounded" height={240} />
          ) : dailyTrend.error ? (
            <Typography variant="body2" color="text.secondary">Usage trend is unavailable right now.</Typography>
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
          <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600, mb: 1 }}>Agent performance</Typography>
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
    </Stack>
  )
}
