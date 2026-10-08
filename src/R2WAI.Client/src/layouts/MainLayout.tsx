import { useEffect, useMemo, useState } from 'react'
import {
  AppBar,
  Avatar,
  BottomNavigation,
  BottomNavigationAction,
  Box,
  Breadcrumbs,
  ButtonBase,
  Chip,
  Divider,
  Drawer,
  IconButton,
  Link as MuiLink,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Paper,
  Stack,
  Tab,
  Tabs,
  Toolbar,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import type { Theme } from '@mui/material/styles'
import HubOutlinedIcon from '@mui/icons-material/HubOutlined'
import MenuIcon from '@mui/icons-material/Menu'
import SearchIcon from '@mui/icons-material/Search'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import HelpOutlineIcon from '@mui/icons-material/HelpOutlineOutlined'
import LightModeIcon from '@mui/icons-material/LightMode'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import CircleIcon from '@mui/icons-material/Circle'
import { Link as RouterLink, Outlet, useLocation, useNavigate, useNavigation } from 'react-router-dom'
import { useAuth } from '../lib/auth/useAuth'
import { getBottomNavItems, getBreadcrumbs, getHomeNavItem, getNavSections, getPersona, getPersonaLabel, getSectionTabs, type NavItem } from '../lib/nav/roleNav'
import { getNavIcon } from '../lib/nav/navIcons'
import { useThemeMode } from '../theme/ThemeModeProvider'
import { CommandPalette } from '../components/CommandPalette'
import { CopilotPanel } from '../components/CopilotPanel'
import { NotificationBell } from '../components/NotificationBell'
import { NotificationFeedProvider } from '../lib/notifications/NotificationFeedProvider'
import { KeyboardShortcutsHelp } from '../components/KeyboardShortcutsHelp'
import { useDocumentTitle } from '../lib/useDocumentTitle'
import { LoadingSkeleton } from '../components/LoadingSkeleton'

const DRAWER_WIDTH = 248
const COLLAPSED_DRAWER_WIDTH = 72
const DRAWER_COLLAPSED_STORAGE_KEY = 'r2wai.sidebar.collapsed'

function readDesktopDrawerCollapsed(): boolean {
  try {
    return typeof window !== 'undefined' && window.localStorage.getItem(DRAWER_COLLAPSED_STORAGE_KEY) === 'true'
  } catch {
    return false
  }
}

const sidebarIconSx = { minWidth: 36, color: 'inherit' }

const sidebarItemSx = {
  position: 'relative',
  borderRadius: 2,
  mb: 0.25,
  color: 'text.secondary',
  '& .MuiListItemText-primary': { fontSize: '0.875rem', fontWeight: 500 },
  '&:hover': { bgcolor: 'action.hover', color: 'text.primary' },
  '&.Mui-selected': {
    bgcolor: (t: Theme) => `color-mix(in srgb, ${t.palette.primary.main} 12%, ${t.palette.background.paper})`,
    color: 'primary.main',
    '& .MuiListItemText-primary': { fontWeight: 700 },
    '& .MuiListItemIcon-root': { color: 'primary.main' },
    '&:hover': { bgcolor: (t: Theme) => `color-mix(in srgb, ${t.palette.primary.main} 16%, ${t.palette.background.paper})` },
  },
}

function environmentColor(environment: string): 'success' | 'warning' | 'info' {
  if (environment === 'Production') return 'success'
  if (environment === 'Staging') return 'warning'
  return 'info'
}

function pathMatches(path: string, pathname: string): boolean {
  return pathname === path || pathname.startsWith(`${path}/`)
}

/** A sidebar item stays highlighted on every page of its tab group, not just its own path. */
function isNavItemActive(item: NavItem, pathname: string): boolean {
  return pathMatches(item.path, pathname) || (item.matchPaths ?? []).some((path) => pathMatches(path, pathname))
}

export function MainLayout() {
  const { user, logout } = useAuth()
  const { isDark, toggle } = useThemeMode()
  const location = useLocation()
  const navigation = useNavigation()
  const navigate = useNavigate()
  const theme = useTheme()
  // Below `md`: sidebar becomes an overlay (temporary) instead of pushing content, matching the
  // brief's §21 requirement. Below `sm`: the bottom nav takes over as the primary quick-access
  // surface, since a temporary drawer isn't a great one-thumb-reach pattern on a phone.
  const isTablet = useMediaQuery(theme.breakpoints.down('md'))
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))

  const [drawerOpen, setDrawerOpen] = useState(!isTablet)
  const [desktopCollapsed, setDesktopCollapsed] = useState(readDesktopDrawerCollapsed)
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [copilotOpen, setCopilotOpen] = useState(false)
  const [shortcutsOpen, setShortcutsOpen] = useState(false)
  const [userMenuAnchor, setUserMenuAnchor] = useState<HTMLElement | null>(null)
  const [environment, setEnvironment] = useState<string | null>(null)

  const persona = getPersona(user)
  const homeItem = getHomeNavItem()
  const HomeIcon = getNavIcon(homeItem.icon)
  const isRailCollapsed = !isTablet && desktopCollapsed
  const desktopDrawerWidth = isRailCollapsed ? COLLAPSED_DRAWER_WIDTH : DRAWER_WIDTH
  const sections = useMemo(() => getNavSections(persona), [persona])
  const bottomNavItems = useMemo(() => getBottomNavItems(persona), [persona])

  useEffect(() => {
    try {
      window.localStorage.setItem(DRAWER_COLLAPSED_STORAGE_KEY, String(desktopCollapsed))
    } catch {
      // The preference is optional; keep the in-memory toggle usable when storage is unavailable.
    }
  }, [desktopCollapsed])

  // Auto-close the overlay drawer on navigation (temporary variant only — desktop's persistent
  // drawer is meant to stay put).
  useEffect(() => {
    if (isTablet) setDrawerOpen(false)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname])

  // Keep drawer state in sync when the viewport crosses the md breakpoint (e.g. resizing a
  // browser window) — restore the persistent-open default going back to desktop, rather than
  // leaving it stuck closed from whatever state the temporary drawer was last in.
  useEffect(() => {
    setDrawerOpen(!isTablet)
  }, [isTablet])

  useEffect(() => {
    let cancelled = false
    fetch('/api-health')
      .then((res) => (res.ok ? res.json() : null))
      .then((data: { environment?: string } | null) => {
        if (!cancelled && data?.environment) setEnvironment(data.environment)
      })
      .catch(() => {
        // Badge just stays hidden if the health endpoint can't be reached.
      })
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    function isTypingTarget(target: EventTarget | null): boolean {
      const el = target as HTMLElement | null
      return !!el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable)
    }
    function onKeyDown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        setPaletteOpen(true)
        return
      }
      if (e.key === '?' && !isTypingTarget(e.target)) {
        e.preventDefault()
        setShortcutsOpen(true)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const breadcrumbs = useMemo(
    () => getBreadcrumbs(location.pathname, persona),
    [location.pathname, persona],
  )

  const sectionTabs = useMemo(
    () => getSectionTabs(location.pathname, persona),
    [location.pathname, persona],
  )

  const pageLabel = breadcrumbs[breadcrumbs.length - 1]?.label ?? homeItem.label
  useDocumentTitle(pageLabel === homeItem.label ? 'R2WAI Studio' : `${pageLabel} · R2WAI Studio`)

  useEffect(() => {
    document.getElementById('main-content')?.focus()
  }, [location.pathname])

  async function handleLogout() {
    setUserMenuAnchor(null)
    await logout()
    navigate('/login')
  }

  return (
    <NotificationFeedProvider>
    <Box sx={{ display: 'flex', minHeight: '100dvh', bgcolor: 'background.default', color: 'text.primary' }}>
      <MuiLink
        href="#main-content"
        sx={{
          position: 'absolute',
          left: -9999,
          top: 'auto',
          zIndex: (t) => t.zIndex.tooltip + 1,
          p: 1,
          bgcolor: 'background.paper',
          '&:focus-visible': { left: 8, top: 8 },
        }}
      >
        Skip to main content
      </MuiLink>
      <AppBar
        position="fixed"
        color="inherit"
        elevation={0}
        sx={{
          backgroundColor: 'background.paper',
          backdropFilter: 'blur(16px)',
          borderBottom: '1px solid',
          borderColor: 'divider',
          zIndex: (t) => t.zIndex.drawer + 1,
          ml: { md: `${desktopDrawerWidth}px` },
          width: { md: `calc(100% - ${desktopDrawerWidth}px)` },
          transition: (t) => t.transitions.create(['margin', 'width']),
        }}
      >
        <Toolbar sx={{ minHeight: { xs: 58, sm: 64 }, gap: { xs: 0.25, sm: 1 }, px: { xs: 1, sm: 2 } }}>
          <Tooltip title={isTablet ? (drawerOpen ? 'Close navigation' : 'Open navigation') : (isRailCollapsed ? 'Expand navigation' : 'Collapse navigation')}>
            <IconButton
              edge="start"
              size={isMobile ? 'small' : 'medium'}
              onClick={() => isTablet ? setDrawerOpen((o) => !o) : setDesktopCollapsed((collapsed) => !collapsed)}
              aria-label={isTablet ? (drawerOpen ? 'Close navigation' : 'Open navigation') : (isRailCollapsed ? 'Expand navigation' : 'Collapse navigation')}
            >
              <MenuIcon />
            </IconButton>
          </Tooltip>
          {isTablet && (
            <Typography
              variant="subtitle1"
              component={RouterLink}
              to="/"
              sx={{ fontWeight: 700, textDecoration: 'none', color: 'inherit', mr: { xs: 0, sm: 1 }, fontSize: { xs: '0.875rem', sm: '1rem' } }}
            >
              {isMobile ? 'R2WAI' : 'R2WAI Studio'}
            </Typography>
          )}
          {environment && (
            <Chip
              size="small"
              icon={<CircleIcon sx={{ fontSize: '10px !important', color: `${environmentColor(environment)}.main` }} />}
              label={environment}
              variant="outlined"
              sx={{ display: { xs: 'none', sm: 'flex' } }}
            />
          )}

          <Box sx={{ flexGrow: 1 }} />

          <Box
            component="button"
            type="button"
            onClick={() => setPaletteOpen(true)}
            aria-label="Search assistants, knowledge, tools"
            sx={{
              display: { xs: 'none', md: 'flex' },
              alignItems: 'center',
              gap: 1,
              width: 320,
              height: 36,
              px: 1.5,
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 2,
              bgcolor: 'action.hover',
              color: 'text.secondary',
              font: 'inherit',
              cursor: 'pointer',
              transition: 'border-color 120ms ease, background-color 120ms ease',
              '&:hover': { borderColor: 'primary.main', bgcolor: 'action.selected' },
            }}
          >
            <SearchIcon fontSize="small" />
            <Typography variant="body2" sx={{ flexGrow: 1, textAlign: 'left', color: 'text.secondary' }} noWrap>
              Search assistants, knowledge, tools…
            </Typography>
            <Box
              component="kbd"
              sx={{
                fontSize: '0.7rem',
                fontFamily: 'inherit',
                fontWeight: 600,
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 1,
                px: 0.6,
                py: 0.1,
                bgcolor: 'background.paper',
                color: 'text.secondary',
              }}
            >
              Ctrl K
            </Box>
          </Box>
          <Tooltip title="Search (Ctrl+K)">
            <IconButton
              size="small"
              onClick={() => setPaletteOpen(true)}
              aria-label="Search"
              sx={{ display: { xs: 'inline-flex', md: 'none' } }}
            >
              <SearchIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title={isDark ? 'Light mode' : 'Dark mode'}>
            <IconButton size="small" onClick={toggle} aria-label="Toggle dark mode">
              {isDark ? <LightModeIcon fontSize="small" /> : <DarkModeIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
          <NotificationBell />
          <Tooltip title="AI Copilot">
            <IconButton
              size="small"
              color={copilotOpen ? 'primary' : 'default'}
              onClick={() => setCopilotOpen((o) => !o)}
              aria-label="Toggle AI Copilot"
              sx={{ display: { xs: 'none', sm: 'inline-flex' } }}
            >
              <AutoAwesomeIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Help">
            <IconButton size="small" component={RouterLink} to="/about" aria-label="Help" sx={{ display: { xs: 'none', sm: 'inline-flex' } }}>
              <HelpOutlineIcon fontSize="small" />
            </IconButton>
          </Tooltip>

          <ButtonBase
            onClick={(e) => setUserMenuAnchor(e.currentTarget)}
            aria-label="Account menu"
            aria-haspopup="true"
            aria-expanded={!!userMenuAnchor}
            sx={{ ml: 1, px: 1, py: 0.5, borderRadius: 2 }}
          >
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Avatar sx={{ width: 30, height: 30, fontSize: '0.8rem' }}>
                {(user?.displayName ?? user?.email ?? 'U').charAt(0).toUpperCase()}
              </Avatar>
              <Box sx={{ display: { xs: 'none', md: 'block' }, lineHeight: 1.2 }}>
                <Typography variant="caption" sx={{ display: 'block', fontWeight: 600 }}>
                  {user?.displayName ?? user?.email}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {getPersonaLabel(persona)}
                </Typography>
              </Box>
              <ExpandMoreIcon fontSize="small" sx={{ display: { xs: 'none', md: 'block' }, color: 'text.secondary' }} />
            </Stack>
          </ButtonBase>
          <Menu anchorEl={userMenuAnchor} open={!!userMenuAnchor} onClose={() => setUserMenuAnchor(null)}>
            <MenuItem onClick={() => { setUserMenuAnchor(null); setCopilotOpen(true) }}>
              Open AI Copilot
            </MenuItem>
            <MenuItem onClick={() => { setUserMenuAnchor(null); setShortcutsOpen(true) }}>
              Keyboard shortcuts
            </MenuItem>
            <MenuItem component={RouterLink} to="/about" onClick={() => setUserMenuAnchor(null)}>
              Help & About
            </MenuItem>
            <Divider />
            <MenuItem component={RouterLink} to="/profile" onClick={() => setUserMenuAnchor(null)}>
              Profile
            </MenuItem>
            <MenuItem component={RouterLink} to="/inbox" onClick={() => setUserMenuAnchor(null)}>
              Inbox
            </MenuItem>
            <MenuItem component={RouterLink} to="/settings" onClick={() => setUserMenuAnchor(null)}>
              Settings
            </MenuItem>
            <Divider />
            <MenuItem onClick={() => void handleLogout()}>Sign out</MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Drawer
        variant={isTablet ? 'temporary' : 'persistent'}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{
          width: isTablet ? 0 : desktopDrawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: isTablet ? DRAWER_WIDTH : desktopDrawerWidth,
            boxSizing: 'border-box',
            overflow: 'hidden',
            display: 'flex',
            flexDirection: 'column',
            bgcolor: 'background.paper',
            color: 'text.primary',
            borderRight: '1px solid',
            borderColor: 'divider',
          },
        }}
      >
        <Stack
          direction="row"
          spacing={1.25}
          component={RouterLink}
          to="/"
          sx={{
            flexShrink: 0,
            alignItems: 'center',
            justifyContent: isRailCollapsed ? 'center' : 'flex-start',
            px: isRailCollapsed ? 1 : 2,
            minHeight: 64,
            textDecoration: 'none',
            borderBottom: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 34,
              height: 34,
              borderRadius: 2,
              flexShrink: 0,
              background: (t) => `linear-gradient(135deg, ${t.palette.primary.main}, ${t.palette.secondary.main})`,
              color: '#fff',
            }}
          >
            <HubOutlinedIcon fontSize="small" />
          </Box>
          {!isRailCollapsed && (
            <Box sx={{ minWidth: 0 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 750, color: 'text.primary', lineHeight: 1.2 }}>
                R2WAI
              </Typography>
              <Typography variant="caption" sx={{ color: 'text.secondary', whiteSpace: 'nowrap' }}>
                AI Work Execution Platform
              </Typography>
            </Box>
          )}
        </Stack>
        <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
          <List sx={{ flexGrow: 1, minHeight: 0, overflowY: 'auto', px: isRailCollapsed ? 1 : 1.5, py: 1.5 }}>
            <Tooltip title={isRailCollapsed ? homeItem.label : ''} placement="right">
              <ListItemButton
                component={RouterLink}
                to={homeItem.path}
                selected={location.pathname === homeItem.path}
                aria-label={homeItem.label}
                aria-current={location.pathname === homeItem.path ? 'page' : undefined}
                sx={[sidebarItemSx, isRailCollapsed && { justifyContent: 'center', px: 1 }]}
              >
                <ListItemIcon sx={isRailCollapsed ? { minWidth: 0, justifyContent: 'center', color: 'inherit' } : sidebarIconSx}>
                  <HomeIcon fontSize="small" />
                </ListItemIcon>
                {!isRailCollapsed && <ListItemText primary={homeItem.label} />}
              </ListItemButton>
            </Tooltip>

            {sections.map((section) => {
              const primaryItem = section.label ? section.items[0] : undefined
              const visibleItems = primaryItem && !isRailCollapsed ? section.items.slice(1) : section.items
              const renderItem = (item: (typeof section.items)[number], isPrimaryItem: boolean) => {
                const Icon = getNavIcon(item.icon)
                const active = isNavItemActive(item, location.pathname)
                return (
                  <Tooltip key={item.path} title={isRailCollapsed ? item.label : ''} placement="right">
                    <ListItemButton
                      component={RouterLink}
                      to={item.path}
                      selected={active}
                      aria-label={item.label}
                      aria-current={active ? 'page' : undefined}
                      sx={[
                        sidebarItemSx,
                        isRailCollapsed && { justifyContent: 'center', px: 1 },
                        !isPrimaryItem && !isRailCollapsed && { ml: 2, width: 'calc(100% - 16px)' },
                        !isPrimaryItem && { '& .MuiListItemText-primary': { fontSize: '0.8125rem' } },
                      ]}
                    >
                      <ListItemIcon sx={isRailCollapsed ? { minWidth: 0, justifyContent: 'center', color: 'inherit' } : sidebarIconSx}>
                        <Icon fontSize="small" />
                      </ListItemIcon>
                      {!isRailCollapsed && <ListItemText primary={item.label} />}
                    </ListItemButton>
                  </Tooltip>
                )
              }
              return (
                <Box key={section.label || 'nav'} role="group" aria-label={section.label ? `${section.label} navigation` : 'Navigation'} sx={{ mb: 0.75 }}>
                  {primaryItem && !isRailCollapsed && renderItem(primaryItem, true)}
                  {visibleItems.map((item, index) => renderItem(item, !primaryItem && index === 0 || Boolean(primaryItem && isRailCollapsed && index === 0)))}
                </Box>
              )
            })}
          </List>

        </Box>
      </Drawer>

      <Box
        id="main-content"
        component="main"
        tabIndex={-1}
        sx={{
          flexGrow: 1,
          minWidth: 0,
          width: '100%',
          maxWidth: 1920,
          mx: 'auto',
          p: { xs: 1.5, sm: 2.5, lg: 3 },
          // Room for the fixed bottom nav below `sm` so page content never sits under it.
          pb: isMobile ? 8 : { xs: 2, sm: 3 },
          transition: (t) => t.transitions.create('margin'),
        }}
      >
        <Toolbar />
        <Box
          aria-live="polite"
          aria-atomic="true"
          sx={{ position: 'absolute', width: 1, height: 1, p: 0, m: -1, overflow: 'hidden', clip: 'rect(0, 0, 0, 0)', whiteSpace: 'nowrap', border: 0 }}
        >
          {pageLabel}
        </Box>
        <Breadcrumbs sx={{ mb: { xs: 2.5, sm: 3 }, minHeight: 20 }} aria-label="Breadcrumb navigation">
          {breadcrumbs.map((crumb, i) =>
            i === breadcrumbs.length - 1 ? (
              <Typography key={crumb.path} variant="body2" color="text.primary">
                {crumb.label}
              </Typography>
            ) : (
              <MuiLink key={crumb.path} component={RouterLink} to={crumb.path} variant="body2" underline="hover">
                {crumb.label}
              </MuiLink>
            ),
          )}
        </Breadcrumbs>
        {sectionTabs && (
          <Tabs
            value={sectionTabs.activePath}
            variant="scrollable"
            scrollButtons="auto"
            aria-label={`${sectionTabs.tabs[0].label} sections`}
            sx={{ mb: { xs: 2, md: 3 }, borderBottom: 1, borderColor: 'divider', minHeight: 40 }}
          >
            {sectionTabs.tabs.map((tab) => (
              <Tab key={tab.path} value={tab.path} label={tab.label} component={RouterLink} to={tab.path} sx={{ minHeight: 40 }} />
            ))}
          </Tabs>
        )}
        {navigation.state === 'loading' ? <LoadingSkeleton variant="text" count={8} /> : <Outlet />}
      </Box>

      {isMobile && (
        <Paper
          elevation={0}
          sx={{ position: 'fixed', bottom: 0, left: 0, right: 0, zIndex: (t) => t.zIndex.drawer + 1, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.paper', backdropFilter: 'blur(16px)' }}
        >
          <BottomNavigation
            // '/' prefix-matches every path, so it's checked last/explicitly rather than via
            // the same startsWith scan the other (mutually non-overlapping) item paths use.
            value={
              bottomNavItems.find((item) => item.path !== '/' && location.pathname.startsWith(item.path))?.path ??
              (location.pathname === '/' ? '/' : false)
            }
            onChange={(_, value: string) => navigate(value)}
            showLabels
          >
            {bottomNavItems.map((item) => {
              const Icon = getNavIcon(item.icon)
              const active = location.pathname === item.path || location.pathname.startsWith(`${item.path}/`)
              return (
                <BottomNavigationAction
                  key={item.path}
                  label={item.label}
                  value={item.path}
                  icon={<Icon fontSize="small" />}
                  aria-current={active ? 'page' : undefined}
                />
              )
            })}
          </BottomNavigation>
        </Paper>
      )}

      <CommandPalette
        open={paletteOpen}
        onClose={() => setPaletteOpen(false)}
        isDark={isDark}
        onToggleTheme={toggle}
        onToggleCopilot={() => setCopilotOpen((o) => !o)}
        onShowShortcuts={() => setShortcutsOpen(true)}
      />
      <CopilotPanel open={copilotOpen} onClose={() => setCopilotOpen(false)} />
      <KeyboardShortcutsHelp open={shortcutsOpen} onClose={() => setShortcutsOpen(false)} />
    </Box>
    </NotificationFeedProvider>
  )
}
