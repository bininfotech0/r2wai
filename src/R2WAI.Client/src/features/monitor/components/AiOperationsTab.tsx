import { useQuery } from '@tanstack/react-query'
import { List, ListItem, ListItemText, Paper, Stack, Typography } from '@mui/material'
import TokenOutlined from '@mui/icons-material/TokenOutlined'
import ForumOutlined from '@mui/icons-material/ForumOutlined'
import TimerOutlined from '@mui/icons-material/TimerOutlined'
import { StatCard } from '../../../components/StatCard'
import { getAiStats, listAuditLogs } from '../api'

export function AiOperationsTab() {
  const statsQuery = useQuery({ queryKey: ['monitor', 'ai-stats'], queryFn: getAiStats })
  const activityQuery = useQuery({
    queryKey: ['monitor', 'ai-activity'],
    queryFn: () => listAuditLogs({}, 1, 30),
  })

  const recentAiActivity = (activityQuery.data?.items ?? []).filter(
    (a) => a.entityType === 'AssistantDefinition' || a.entityType === 'Conversation' || a.entityType === 'Message',
  )

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={2} sx={{ flexWrap: 'wrap' }}>
        <StatCard
          label="Tokens (30d)"
          value={statsQuery.data?.totalTokens ?? 0}
          icon={TokenOutlined}
          color="primary"
          loading={statsQuery.isLoading}
        />
        <StatCard
          label="Conversations (30d)"
          value={statsQuery.data?.totalConversations ?? 0}
          icon={ForumOutlined}
          color="secondary"
          loading={statsQuery.isLoading}
        />
        <StatCard
          label="Avg Response Time"
          value={statsQuery.data ? `${statsQuery.data.avgResponseTimeSec}s` : '—'}
          icon={TimerOutlined}
          color="info"
          loading={statsQuery.isLoading}
        />
      </Stack>
      {statsQuery.data && statsQuery.data.totalTokens === 0 && (
        <Typography variant="caption" color="text.secondary">
          Tokens show 0 because the assistant-chat write path doesn't record per-message token usage yet — a known
          backend gap, not empty usage.
        </Typography>
      )}

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
          Recent AI Activity
        </Typography>
        <List dense>
          {recentAiActivity.map((a) => (
            <ListItem key={a.id} divider>
              <ListItemText
                primary={`${a.action} · ${a.entityType}`}
                secondary={`${a.userName ?? 'System'} · ${new Date(a.timestamp).toLocaleString()}`}
              />
            </ListItem>
          ))}
          {recentAiActivity.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              No recent AI-related activity.
            </Typography>
          )}
        </List>
      </Paper>
    </Stack>
  )
}
