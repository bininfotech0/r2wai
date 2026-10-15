import { useState } from 'react'
import {
  Badge,
  Box,
  Button,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Popover,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import NotificationsOutlinedIcon from '@mui/icons-material/NotificationsOutlined'
import { Link as RouterLink, useNavigate } from 'react-router-dom'
import { useNotificationFeed } from '../lib/notifications/NotificationFeedProvider'
import { getSafeNotificationPath } from '../lib/notifications/notificationLinks'

export function NotificationBell() {
  const { notifications, unreadCount, markAllRead, markRead } = useNotificationFeed()
  const navigate = useNavigate()
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)

  function openNotification(id: string, link: string | null) {
    void markRead(id)
    setAnchorEl(null)
    const target = getSafeNotificationPath(link)
    if (target) navigate(target)
  }

  return (
    <>
      <Tooltip title="Notifications">
        <IconButton
          size="small"
          onClick={(e) => {
            setAnchorEl(e.currentTarget)
          }}
          aria-label={unreadCount ? `Notifications, ${unreadCount} unread` : 'Notifications'}
        >
          <Badge badgeContent={unreadCount} color="error" max={9}>
            <NotificationsOutlinedIcon fontSize="small" />
          </Badge>
        </IconButton>
      </Tooltip>
      <Popover
        open={!!anchorEl}
        anchorEl={anchorEl}
        onClose={() => setAnchorEl(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 2, py: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
            Notifications
          </Typography>
          <Button size="small" component={RouterLink} to="/inbox" onClick={() => setAnchorEl(null)}>
            View all
          </Button>
        </Stack>
        {unreadCount > 0 && (
          <Stack direction="row" sx={{ justifyContent: 'flex-end', px: 2, pb: 1 }}>
            <Button size="small" onClick={() => void markAllRead()}>
              Mark all read
            </Button>
          </Stack>
        )}
        <List sx={{ width: 320, maxHeight: 400, overflowY: 'auto' }} dense>
          {notifications.length === 0 ? (
            <ListItem>
              <ListItemText
                primary={
                  <Typography variant="body2" color="text.secondary">
                    No notifications yet.
                  </Typography>
                }
              />
            </ListItem>
          ) : (
            notifications.slice(0, 8).map((n) => (
              <ListItem key={n.id} divider disablePadding>
                <ListItemButton
                  onClick={() => openNotification(n.id, n.link)}
                  aria-label={`${n.read ? 'Read' : 'Unread'} notification: ${n.title}`}
                  sx={{ alignItems: 'flex-start', opacity: n.read ? 0.78 : 1 }}
                >
                  <ListItemText
                    primary={<Typography variant="body2" sx={{ fontWeight: n.read ? 500 : 700 }} noWrap>{n.title}</Typography>}
                    secondary={<Typography variant="caption" color="text.secondary" noWrap>{n.message}</Typography>}
                  />
                  {!n.read && (
                    <Box
                      aria-hidden="true"
                      sx={{ width: 8, height: 8, flex: '0 0 auto', mt: 1, ml: 1, borderRadius: '50%', bgcolor: 'primary.main' }}
                    />
                  )}
                </ListItemButton>
              </ListItem>
            ))
          )}
        </List>
      </Popover>
    </>
  )
}
