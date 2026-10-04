import { useState } from 'react'
import {
  Badge,
  Button,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Popover,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import NotificationsOutlinedIcon from '@mui/icons-material/NotificationsOutlined'
import { Link as RouterLink } from 'react-router-dom'
import { useNotificationFeed } from '../lib/notifications/NotificationFeedProvider'

export function NotificationBell() {
  const { notifications, unreadCount, markAllRead } = useNotificationFeed()
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)

  return (
    <>
      <Tooltip title="Notifications">
        <IconButton
          size="small"
          onClick={(e) => {
            setAnchorEl(e.currentTarget)
            markAllRead()
          }}
          aria-label="Notifications"
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
              <ListItem key={n.id} divider>
                <ListItemText primary={n.title} secondary={n.message} />
              </ListItem>
            ))
          )}
        </List>
      </Popover>
    </>
  )
}
