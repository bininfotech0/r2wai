import { useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Chip,
  List,
  ListItemButton,
  ListItemText,
  Paper,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  Typography,
} from '@mui/material'
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined'
import { useNotificationFeed } from '../../../lib/notifications/NotificationFeedProvider'
import { PageHeader } from '../../../components/PageHeader'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'

export function InboxPage() {
  const { notifications, unreadCount, historyLoading, historyError, retryHistory, markAllRead, markRead } =
    useNotificationFeed()
  const [filter, setFilter] = useState<'all' | 'unread'>('all')
  const visible = filter === 'unread' ? notifications.filter((n) => !n.read) : notifications

  return (
    <Box>
      <PageHeader
        title="Inbox"
        description="Notifications sent to your account."
        actions={
          unreadCount > 0 ? (
            <Button size="small" onClick={markAllRead}>
              Mark all read
            </Button>
          ) : undefined
        }
      />

      {historyError && notifications.length > 0 && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={retryHistory}>
              Retry
            </Button>
          }
        >
          Older notifications could not be loaded. Live updates may still arrive.
        </Alert>
      )}

      <Tabs
        value={filter}
        onChange={(_, value: 'all' | 'unread') => setFilter(value)}
        aria-label="Filter inbox notifications"
        sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Tab value="all" label={`All (${notifications.length})`} />
        <Tab value="unread" label={`Unread (${unreadCount})`} />
      </Tabs>

      <Paper variant="outlined">
        {historyLoading && notifications.length === 0 ? (
          <Stack spacing={1.5} sx={{ p: 2 }} role="status" aria-label="Loading notifications">
            {[0, 1, 2, 3].map((i) => (
              <Stack key={i} spacing={0.5} sx={{ py: 1 }}>
                <Skeleton variant="text" width="35%" />
                <Skeleton variant="text" width="75%" />
                <Skeleton variant="text" width="20%" />
              </Stack>
            ))}
          </Stack>
        ) : historyError && notifications.length === 0 ? (
          <ErrorState
            title="Unable to load your notifications"
            description="Check your connection, then retry."
            onRetry={retryHistory}
          />
        ) : notifications.length === 0 ? (
          <EmptyState icon={InboxOutlinedIcon} title="Nothing yet" description="You'll see updates here as they happen." />
        ) : visible.length === 0 ? (
          <EmptyState icon={InboxOutlinedIcon} title="You're all caught up" description="There are no unread notifications." />
        ) : (
          <List disablePadding>
            {visible.map((n) => (
              <ListItemButton
                key={n.id}
                divider
                onClick={() => !n.read && markRead(n.id)}
                aria-label={`${n.read ? 'Read' : 'Unread'} notification: ${n.title}`}
                sx={{
                  bgcolor: n.read ? 'transparent' : 'action.hover',
                  borderLeft: '3px solid',
                  borderLeftColor: n.read ? 'transparent' : 'primary.main',
                  alignItems: 'flex-start',
                  py: 1.5,
                }}
              >
                <ListItemText
                  primary={
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between', minWidth: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: n.read ? 400 : 600, overflowWrap: 'anywhere' }}>
                        {n.title}
                      </Typography>
                      {!n.read && <Chip label="Unread" size="small" color="primary" variant="outlined" />}
                    </Stack>
                  }
                  secondary={
                    <>
                      {n.message}
                      <Typography component="span" variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>
                        {new Date(n.timestamp).toLocaleString()}
                      </Typography>
                    </>
                  }
                />
              </ListItemButton>
            ))}
          </List>
        )}
      </Paper>
    </Box>
  )
}
