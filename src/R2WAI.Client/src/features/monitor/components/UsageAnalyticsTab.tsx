import { useQuery } from '@tanstack/react-query'
import { Grid, Paper, Stack, Typography } from '@mui/material'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import ForumOutlined from '@mui/icons-material/ForumOutlined'
import DescriptionOutlined from '@mui/icons-material/DescriptionOutlined'
import { DonutChart } from '../../../components/charts/DonutChart'
import { StatCard } from '../../../components/StatCard'
import { getUsageAnalytics } from '../api'

export function UsageAnalyticsTab() {
  const query = useQuery({ queryKey: ['monitor', 'usage-analytics'], queryFn: () => getUsageAnalytics(30) })
  const data = query.data

  return (
    <Stack spacing={2}>
      <Typography variant="caption" color="text.secondary">
        {data?.period ?? 'Last 30 days'}
      </Typography>
      <Stack direction="row" spacing={2} sx={{ flexWrap: 'wrap' }}>
        <StatCard
          label="Assistants"
          value={data?.assistants.total ?? 0}
          icon={SmartToyOutlined}
          color="primary"
          loading={query.isLoading}
        />
        <StatCard
          label="Active Assistants"
          value={data?.assistants.active ?? 0}
          icon={SmartToyOutlined}
          color="success"
          loading={query.isLoading}
        />
        <StatCard
          label="Conversations"
          value={data?.conversations.total ?? 0}
          icon={ForumOutlined}
          color="info"
          loading={query.isLoading}
        />
        <StatCard
          label="Messages"
          value={data?.conversations.messages ?? 0}
          icon={ForumOutlined}
          color="secondary"
          loading={query.isLoading}
        />
        <StatCard
          label="Documents Uploaded"
          value={data?.documents.uploaded ?? 0}
          icon={DescriptionOutlined}
          color="warning"
          loading={query.isLoading}
        />
      </Stack>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              Workflow Executions
            </Typography>
            {data && data.workflows.executions > 0 ? (
              <DonutChart
                total={data.workflows.executions}
                centerLabel="Executions"
                segments={[
                  { label: 'Completed', value: data.workflows.completed, color: '#22c55e' },
                  { label: 'Failed', value: data.workflows.failed, color: '#ef4444' },
                  {
                    label: 'Other',
                    value: Math.max(0, data.workflows.executions - data.workflows.completed - data.workflows.failed),
                    color: '#94a3b8',
                  },
                ]}
              />
            ) : (
              <Typography variant="body2" color="text.secondary">
                No workflow executions in this window.
              </Typography>
            )}
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              Confirmations
            </Typography>
            {data && data.approvals.total > 0 ? (
              <DonutChart
                total={data.approvals.total}
                centerLabel="Requests"
                segments={[
                  { label: 'Approved', value: data.approvals.approved, color: '#22c55e' },
                  { label: 'Pending', value: data.approvals.pending, color: '#f59e0b' },
                  {
                    label: 'Other',
                    value: Math.max(0, data.approvals.total - data.approvals.approved - data.approvals.pending),
                    color: '#94a3b8',
                  },
                ]}
              />
            ) : (
              <Typography variant="body2" color="text.secondary">
                No approval requests in this window.
              </Typography>
            )}
          </Paper>
        </Grid>
      </Grid>
    </Stack>
  )
}
