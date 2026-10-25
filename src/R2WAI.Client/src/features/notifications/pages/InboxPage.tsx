import { useEffect, useState } from 'react'
import { formatDistanceToNowStrict } from 'date-fns'
import {
  Alert,
  Avatar,
  Box,
  Button,
  Chip,
  CircularProgress,
  Fade,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  MenuItem,
  Paper,
  Pagination,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined'
import NotificationsOutlinedIcon from '@mui/icons-material/NotificationsOutlined'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlineOutlined'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined'
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined'
import ApprovalOutlinedIcon from '@mui/icons-material/ApprovalOutlined'
import MarkEmailReadOutlinedIcon from '@mui/icons-material/MarkEmailReadOutlined'
import OpenInNewOutlinedIcon from '@mui/icons-material/OpenInNewOutlined'
import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { PageHeader } from '../../../components/PageHeader'
import { useNotificationFeed } from '../../../lib/notifications/NotificationFeedProvider'
import { getSafeNotificationPath } from '../../../lib/notifications/notificationLinks'

type Filter = 'all' | 'unread'
const PAGE_SIZE = 10

function notificationTone(type: string | null): 'primary' | 'success' | 'warning' | 'error' | 'info' {
  const value = type?.toLowerCase() ?? ''
  if (value.includes('approval')) return 'primary'
  if (value.includes('success') || value.includes('complete')) return 'success'
  if (value.includes('warning') || value.includes('warn')) return 'warning'
  if (value.includes('error') || value.includes('fail')) return 'error'
  return 'info'
}

function NotificationTypeIcon({ type }: { type: string | null }) {
  const value = type?.toLowerCase() ?? ''
  if (value.includes('approval')) return <ApprovalOutlinedIcon fontSize="small" />
  if (value.includes('success') || value.includes('complete')) return <CheckCircleOutlineIcon fontSize="small" />
  if (value.includes('warning') || value.includes('warn')) return <WarningAmberOutlinedIcon fontSize="small" />
  if (value.includes('error') || value.includes('fail')) return <ErrorOutlineIcon fontSize="small" />
  return <NotificationsOutlinedIcon fontSize="small" />
}

function notificationTime(timestamp: number) {
  if (!Number.isFinite(timestamp) || timestamp <= 0) return 'Time unavailable'
  return formatDistanceToNowStrict(new Date(timestamp), { addSuffix: true })
}

export function InboxPage() {
  const { notifications, unreadCount, historyLoading, historyError, retryHistory, markAllRead, markRead } =
    useNotificationFeed()
  const navigate = useNavigate()
  const theme = useTheme()
  const reduceMotion = useMediaQuery('(prefers-reduced-motion: reduce)')
  const [filter, setFilter] = useState<Filter>('all')
  const [typeFilter, setTypeFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [markingAll, setMarkingAll] = useState(false)
  const [pendingIds, setPendingIds] = useState<Set<string>>(() => new Set())
  const notificationTypes = [...new Set(notifications.map((n) => n.type?.trim()).filter((type): type is string => !!type))]
    .sort((a, b) => a.localeCompare(b))
  const normalizedSearch = search.trim().toLocaleLowerCase()
  const visible = notifications.filter((notification) => {
    if (filter === 'unread' && notification.read) return false
    if (typeFilter !== 'all' && notification.type?.trim() !== typeFilter) return false
    if (!normalizedSearch) return true
    return `${notification.title} ${notification.message} ${notification.type ?? ''}`.toLocaleLowerCase().includes(normalizedSearch)
  })
  const hasRefiningFilters = typeFilter !== 'all' || normalizedSearch.length > 0
  const pageCount = Math.ceil(visible.length / PAGE_SIZE)
  const pageNotifications = visible.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  useEffect(() => {
    setPage((current) => Math.min(current, Math.max(1, pageCount)))
  }, [pageCount])

  function clearFilters() {
    setFilter('all')
    setTypeFilter('all')
    setSearch('')
    setPage(1)
  }

  async function handleMarkAllRead() {
    if (markingAll) return
    setMarkingAll(true)
    try {
      await markAllRead()
    } finally {
      setMarkingAll(false)
    }
  }

  async function handleMarkRead(id: string) {
    setPendingIds((prev) => new Set(prev).add(id))
    try {
      await markRead(id)
    } finally {
      setPendingIds((prev) => {
        const next = new Set(prev)
        next.delete(id)
        return next
      })
    }
  }

  function openNotification(id: string, link: string | null) {
    const target = getSafeNotificationPath(link)
    if (target) {
      void markRead(id)
      navigate(target)
    } else {
      void markRead(id)
    }
  }

  return (
    <Box>
      <PageHeader
        title="Inbox"
        description="Updates from approvals, automations, and your workspace."
        actions={
          unreadCount > 0 ? (
            <Button
              size="small"
              onClick={() => void handleMarkAllRead()}
              disabled={markingAll}
              startIcon={markingAll ? <CircularProgress size={15} color="inherit" /> : <CheckCircleOutlineIcon />}
            >
              {markingAll ? 'Marking…' : 'Mark all read'}
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
        onChange={(_, value: Filter) => { setFilter(value); setPage(1) }}
        aria-label="Filter inbox notifications"
        sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Tab value="all" label={`All (${notifications.length})`} />
        <Tab value="unread" label={`Unread (${unreadCount})`} />
      </Tabs>

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2, alignItems: { xs: 'stretch', sm: 'center' } }}>
        <TextField
          size="small"
          type="search"
          label="Search notifications"
          placeholder="Search title, message, or type"
          value={search}
          onChange={(event) => { setSearch(event.target.value); setPage(1) }}
          sx={{ flex: 1, maxWidth: { sm: 480 } }}
        />
        <TextField
          select
          size="small"
          label="Type"
          value={typeFilter}
          onChange={(event) => { setTypeFilter(event.target.value); setPage(1) }}
          sx={{ width: { xs: '100%', sm: 220 } }}
        >
          <MenuItem value="all">All types</MenuItem>
          {notificationTypes.map((type) => (
            <MenuItem key={type} value={type}>{type.charAt(0).toUpperCase() + type.slice(1)}</MenuItem>
          ))}
        </TextField>
      </Stack>

      <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
        {historyLoading && notifications.length === 0 ? (
          <Stack spacing={1.5} sx={{ p: 2 }} role="status" aria-label="Loading notifications">
            {[0, 1, 2, 3].map((i) => (
              <Stack key={i} direction="row" spacing={1.5} sx={{ py: 1, alignItems: 'center' }}>
                <Skeleton variant="circular" width={38} height={38} />
                <Stack spacing={0.5} sx={{ flex: 1 }}>
                  <Skeleton variant="text" width="35%" />
                  <Skeleton variant="text" width="75%" />
                  <Skeleton variant="text" width="20%" />
                </Stack>
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
          hasRefiningFilters ? (
            <EmptyState
              icon={InboxOutlinedIcon}
              title="No notifications match"
              description="Try a different search or filter, or clear the filters to see your inbox."
              actionLabel="Clear filters"
              onAction={clearFilters}
            />
          ) : (
            <EmptyState icon={InboxOutlinedIcon} title="You're all caught up" description="There are no unread notifications." />
          )
        ) : (
          <>
          <List disablePadding aria-label="Notifications">
            {pageNotifications.map((n) => {
              const tone = notificationTone(n.type)
              const target = getSafeNotificationPath(n.link)
              const timestamp = Number.isFinite(n.timestamp) && n.timestamp > 0 ? new Date(n.timestamp) : null
              const typeLabel = n.type ? n.type.charAt(0).toUpperCase() + n.type.slice(1) : 'Update'
              return (
                <Fade key={n.id} in timeout={reduceMotion ? 0 : 160} appear={false}>
                  <ListItem
                    divider
                    secondaryAction={
                      <Stack direction="row" spacing={0.5}>
                        {target && (
                          <Tooltip title="Open notification">
                            <IconButton
                              size="small"
                              aria-label={`Open notification: ${n.title}`}
                              onClick={() => openNotification(n.id, n.link)}
                            >
                              <OpenInNewOutlinedIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        {!n.read && (
                          <Tooltip title="Mark as read">
                            <span>
                              <IconButton
                                size="small"
                                aria-label={`Mark as read: ${n.title}`}
                                onClick={() => void handleMarkRead(n.id)}
                                disabled={pendingIds.has(n.id)}
                              >
                                {pendingIds.has(n.id) ? (
                                  <CircularProgress size={18} />
                                ) : (
                                  <MarkEmailReadOutlinedIcon fontSize="small" />
                                )}
                              </IconButton>
                            </span>
                          </Tooltip>
                        )}
                      </Stack>
                    }
                    sx={{
                      p: 0,
                      bgcolor: n.read ? 'transparent' : 'action.hover',
                      borderLeft: '3px solid',
                      borderLeftColor: n.read ? 'transparent' : 'primary.main',
                      transition: reduceMotion ? 'none' : theme.transitions.create('background-color', { duration: 160 }),
                    }}
                  >
                    <ListItemButton
                      onClick={() => openNotification(n.id, n.link)}
                      aria-label={`${n.read ? 'Read' : 'Unread'} notification: ${n.title}${target ? ', open' : ''}`}
                      sx={{ alignItems: 'flex-start', py: 1.5, pl: 1.75, pr: target ? 11 : 7 }}
                    >
                      <Avatar
                        variant="rounded"
                        sx={{
                          width: 38,
                          height: 38,
                          mr: 1.5,
                          flexShrink: 0,
                          color: `${tone}.main`,
                          bgcolor: `${tone}.light`,
                        }}
                      >
                        <NotificationTypeIcon type={n.type} />
                      </Avatar>
                      <ListItemText
                        sx={{ my: 0, minWidth: 0 }}
                        primary={
                          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', minWidth: 0, mb: 0.25 }}>
                            <Typography
                              variant="body2"
                              sx={{ fontWeight: n.read ? 500 : 700, overflowWrap: 'anywhere', minWidth: 0, flex: 1 }}
                            >
                              {n.title}
                            </Typography>
                            {!n.read && <Chip label="Unread" size="small" color="primary" variant="outlined" />}
                          </Stack>
                        }
                        secondary={
                          <>
                            <Typography
                              component="span"
                              variant="body2"
                              color="text.secondary"
                              sx={{ display: '-webkit-box', overflow: 'hidden', WebkitBoxOrient: 'vertical', WebkitLineClamp: 2, overflowWrap: 'anywhere' }}
                            >
                              {n.message}
                            </Typography>
                            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mt: 0.75, flexWrap: 'wrap' }}>
                              <Chip
                                label={typeLabel}
                                size="small"
                                variant="outlined"
                                sx={{ height: 20, fontSize: 11, borderColor: 'divider', color: 'text.secondary' }}
                              />
                              <Tooltip title={timestamp ? timestamp.toLocaleString() : 'Time unavailable'}>
                                <Typography component="span" variant="caption" color="text.secondary">
                                  {notificationTime(n.timestamp)}
                                </Typography>
                              </Tooltip>
                            </Stack>
                          </>
                        }
                      />
                    </ListItemButton>
                  </ListItem>
                </Fade>
              )
            })}
          </List>
          {pageCount > 1 && (
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              spacing={1}
              sx={{ px: 2, py: 1.5, alignItems: 'center', justifyContent: 'space-between', borderTop: '1px solid', borderColor: 'divider' }}
            >
              <Typography variant="caption" color="text.secondary" aria-live="polite">
                Showing {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, visible.length)} of {visible.length}
              </Typography>
              <Pagination count={pageCount} page={page} onChange={(_, value) => setPage(value)} size="small" showFirstButton showLastButton aria-label="Inbox pages" />
            </Stack>
          )}
          </>
        )}
      </Paper>
    </Box>
  )
}
