import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  ButtonBase,
  Divider,
  InputAdornment,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  TextField,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import CheckIcon from '@mui/icons-material/Check'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import SearchIcon from '@mui/icons-material/Search'
import SettingsOutlined from '@mui/icons-material/SettingsOutlined'
import WorkspacesOutlined from '@mui/icons-material/WorkspacesOutlined'
import { useNavigate } from 'react-router-dom'
import { listWorkspaces } from '../features/workspaces/api'
import { useSnackbar } from '../lib/notifications/useSnackbar'
import { setActiveWorkspaceId, useActiveWorkspaceId } from '../lib/workspace/activeWorkspace'

// Search only earns its space once the list stops fitting at a glance.
const SEARCH_THRESHOLD = 6

/**
 * Top-bar workspace picker, the counterpart of Copilot Studio's environment switcher. The chosen
 * workspace travels as X-Workspace-Id (see activeWorkspace.ts) and narrows every list in the
 * Studio. Switching drops every cached query so no page keeps showing the previous workspace.
 */
export function WorkspaceSwitcher() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const activeId = useActiveWorkspaceId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const [filter, setFilter] = useState('')

  const query = useQuery({ queryKey: ['workspaces'], queryFn: listWorkspaces, staleTime: 60_000 })
  const workspaces = useMemo(() => query.data ?? [], [query.data])
  const active = workspaces.find((w) => w.id === activeId) ?? workspaces.find((w) => w.isDefault)

  // No choice yet, or a stored id that no longer exists (deleted, other tenant): pin the default,
  // so the name shown always matches what the server filters by.
  useEffect(() => {
    if (active && active.id !== activeId) setActiveWorkspaceId(active.id)
  }, [active, activeId])

  const visible = filter.trim()
    ? workspaces.filter((w) => w.name.toLowerCase().includes(filter.trim().toLowerCase()))
    : workspaces

  const close = () => {
    setAnchor(null)
    setFilter('')
  }

  const choose = (id: string, name: string) => {
    close()
    if (id === active?.id) return
    setActiveWorkspaceId(id)
    void queryClient.invalidateQueries()
    notify(`Switched to ${name}`, 'success')
  }

  if (!active) return null

  return (
    <>
      <ButtonBase
        onClick={(event) => setAnchor(event.currentTarget)}
        aria-label={`Workspace: ${active.name}. Change workspace`}
        aria-haspopup="menu"
        aria-expanded={anchor !== null}
        sx={{
          gap: 0.75,
          height: 34,
          px: 1.25,
          maxWidth: { xs: 160, sm: 260 },
          border: '1px solid',
          borderColor: 'divider',
          borderRadius: 1,
          color: 'text.primary',
          transition: (t) => t.transitions.create(['border-color', 'background-color'], { duration: 140, easing: 'ease' }),
          '&:hover': { bgcolor: 'action.hover', borderColor: 'text.secondary' },
        }}
      >
        <WorkspacesOutlined sx={{ fontSize: 18, color: 'text.secondary' }} />
        <Box sx={{ minWidth: 0, textAlign: 'left' }}>
          <Typography component="span" sx={{ display: { xs: 'none', md: 'block' }, fontSize: 11, lineHeight: 1.1, color: 'text.secondary' }}>
            Workspace
          </Typography>
          <Typography component="span" noWrap sx={{ display: 'block', fontSize: 13, fontWeight: 600, lineHeight: 1.25 }}>
            {active.name}
          </Typography>
        </Box>
        <ExpandMoreIcon sx={{ fontSize: 18, color: 'text.secondary' }} />
      </ButtonBase>

      <Menu
        anchorEl={anchor}
        open={anchor !== null}
        onClose={close}
        slotProps={{ paper: { sx: { width: 320, maxWidth: 'calc(100vw - 32px)' } }, list: { dense: true } }}
      >
        {workspaces.length > SEARCH_THRESHOLD && (
          <Box sx={{ px: 1.5, pt: 0.5, pb: 1 }} onKeyDown={(event) => event.stopPropagation()}>
            <TextField
              size="small"
              fullWidth
              autoFocus
              placeholder="Find a workspace"
              value={filter}
              onChange={(event) => setFilter(event.target.value)}
              slotProps={{
                input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> },
                htmlInput: { 'aria-label': 'Find a workspace' },
              }}
            />
          </Box>
        )}
        {visible.map((workspace) => (
          <MenuItem key={workspace.id} selected={workspace.id === active.id} onClick={() => choose(workspace.id, workspace.name)}>
            <ListItemIcon>{workspace.id === active.id && <CheckIcon fontSize="small" />}</ListItemIcon>
            <ListItemText
              primary={workspace.name}
              secondary={workspace.isDefault ? 'Default' : undefined}
              slotProps={{ primary: { noWrap: true } }}
            />
            <Typography variant="caption" color="text.secondary" sx={{ ml: 2, fontVariantNumeric: 'tabular-nums' }}>
              {workspace.resources.total} {workspace.resources.total === 1 ? 'item' : 'items'}
            </Typography>
          </MenuItem>
        ))}
        {visible.length === 0 && (
          <MenuItem disabled>
            <ListItemText primary="No workspace matches" />
          </MenuItem>
        )}
        <Divider />
        <MenuItem onClick={() => { close(); navigate('/workspaces?new=1') }}>
          <ListItemIcon><AddIcon fontSize="small" /></ListItemIcon>
          <ListItemText primary="New workspace" />
        </MenuItem>
        <MenuItem onClick={() => { close(); navigate('/workspaces') }}>
          <ListItemIcon><SettingsOutlined fontSize="small" /></ListItemIcon>
          <ListItemText primary="Manage workspaces" />
        </MenuItem>
      </Menu>
    </>
  )
}
