import { createElement, useEffect, useMemo, useState } from 'react'
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
  ListItemIcon,
  ListItemText,
  ListSubheader,
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
import { getBottomNavItems, getBreadcrumbs, getHomeNavItem, getPersona, getPersonaLabel, getRailLayout, getSectionTabs, type NavItem, type NavSection } from '../lib/nav/roleNav'
import { getNavIcon } from '../lib/nav/navIcons'
import { useThemeMode } from '../theme/ThemeModeProvider'
import { CommandPalette } from '../components/CommandPalette'
import { CopilotPanel } from '../components/CopilotPanel'
import { NotificationBell } from '../components/NotificationBell'
import { WorkspaceSwitcher } from '../components/WorkspaceSwitcher'
import { NotificationFeedProvider } from '../lib/notifications/NotificationFeedProvider'
import { KeyboardShortcutsHelp } from '../components/KeyboardShortcutsHelp'
import { useDocumentTitle } from '../lib/useDocumentTitle'
import { LoadingSkeleton } from '../components/LoadingSkeleton'

// Copilot Studio-style frame: a full-width top bar over an icon-over-label rail.
const RAIL_WIDTH = 80
const TOP_BAR_HEIGHT = 56

function environmentColor(environment: string): 'success' | 'warning' | 'info' {
  if (environment === 'Production') return 'success'
  if (environment === 'Staging') return 'warning'
  return 'info'
}

function pathMatches(path: string, pathname: string): boolean {
  if (path === '/') return pathname === '/'
  return pathname === path || pathname.startsWith(`${path}/`)
}

/** A rail item stays highlighted on every page of its section and tab group, not just its own path. */
function isNavItemActive(item: NavItem, pathname: string): boolean {
  return pathMatches(item.path, pathname) || (item.matchPaths ?? []).some((path) => pathMatches(path, pathname))
}

const activePillBg = (t: Theme) => `color-mix(in srgb, ${t.palette.primary.main} 14%, transparent)`

function RailButton({ icon, label, active, ...rest }: {
  icon: string
  label: string
  active: boolean
} & ({ to: string; onClick?: never; expanded?: never } | { to?: never; onClick: (event: React.MouseEvent<HTMLElement>) => void; expanded: boolean })) {
  const linkProps = rest.to
    ? { component: RouterLink, to: rest.to, 'aria-current': active ? ('page' as const) : undefined }
    : { onClick: rest.onClick, 'aria-haspopup': 'menu' as const, 'aria-expanded': rest.expanded }
  return (
    <ButtonBase
      {...linkProps}
      aria-label={label}
      sx={{
        width: 72,
        py: 0.75,
        flexDirection: 'column',
        gap: 0.5,
        borderRadius: 2,
        color: active ? 'primary.main' : 'text.secondary',
        '&:hover': { color: 'text.primary' },
        '&:hover .rail-pill': { bgcolor: active ? activePillBg : 'action.hover' },
        '&.Mui-focusVisible .rail-pill': { outline: '2px solid', outlineColor: 'primary.main' },
      }}
    >
      <Box
        className="rail-pill"
        sx={{
          display: 'grid',
          placeItems: 'center',
          width: 48,
          height: 30,
          borderRadius: 999,
          bgcolor: active ? activePillBg : 'transparent',
          transition: (t) => t.transitions.create('background-color', { duration: 140, easing: 'ease' }),
        }}
      >
        {createElement(getNavIcon(icon), { sx: { fontSize: 20 } })}
      </Box>
      <Typography
        component="span"
        noWrap
        sx={{ maxWidth: 72, fontSize: 11, lineHeight: 1.2, fontWeight: active ? 700 : 500, textAlign: 'center' }}
      >
        {label}
      </Typography>
    </ButtonBase>
  )
}

function NavRail({ items, more, pathname }: { items: NavItem[]; more: NavSection[]; pathname: string }) {
  const [moreAnchor, setMoreAnchor] = useState<HTMLElement | null>(null)
  const moreActive = more.some((section) => section.items.some((item) => isNavItemActive(item, pathname)))
    && !items.some((item) => isNavItemActive(item, pathname))

  return (
    <Box
      component="nav"
      aria-label="Main navigation"
      sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.25, height: '100%', py: 1, overflowY: 'auto' }}
    >
      {items.map((item) => (
        <RailButton key={item.path} icon={item.icon} label={item.label} to={item.path} active={isNavItemActive(item, pathname)} />
      ))}
      <Box sx={{ flexGrow: 1 }} />
      {more.length > 0 && (
        <>
          <RailButton
            icon="More"
            label="More"
            active={moreActive || moreAnchor !== null}
            expanded={moreAnchor !== null}
            onClick={(event) => setMoreAnchor(event.currentTarget)}
          />
          <Menu
            anchorEl={moreAnchor}
            open={moreAnchor !== null}
            onClose={() => setMoreAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            transformOrigin={{ vertical: 'bottom', horizontal: 'left' }}
            slotProps={{ paper: { sx: { minWidth: 240, ml: 1 } }, list: { dense: true } }}
          >
            {more.flatMap((section, index) => [
              index > 0 && <Divider key={`${section.label}-divider`} />,
              <ListSubheader key={`${section.label}-header`} sx={{ lineHeight: '32px', bgcolor: 'transparent' }}>
                {section.label}
              </ListSubheader>,
              ...section.items.map((item) => {
                const Icon = getNavIcon(item.icon)
                const active = isNavItemActive(item, pathname)
                return (
                  <MenuItem
                    key={item.path}
                    component={RouterLink}
                    to={item.path}
                    selected={active}
                    aria-current={active ? 'page' : undefined}
                    onClick={() => setMoreAnchor(null)}
                  >
                    <ListItemIcon><Icon fontSize="small" /></ListItemIcon>
                    <ListItemText primary={item.label} />
                  </MenuItem>
                )
              }),
            ])}
          </Menu>
        </>
      )}
    </Box>
  )
}

export function MainLayout() {
  const { user, logout } = useAuth()
  const { isDark, toggle } = useThemeMode()
  const location = useLocation()
  const navigation = useNavigation()
  const navigate = useNavigate()
  const theme = useTheme()
  // Below `md` the rail becomes an overlay drawer opened from the top bar; below `sm` the bottom
  // nav is the primary quick-access surface (one-thumb reach on a phone).
  const isTablet = useMediaQuery(theme.breakpoints.down('md'))
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))

  // The overlay rail remembers the page it was opened on, so navigating closes it without an effect.
  const [drawerOpenedAt, setDrawerOpenedAt] = useState<string | null>(null)
  const drawerOpen = isTablet && drawerOpenedAt === location.pathname
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [copilotOpen, setCopilotOpen] = useState(false)
  const [shortcutsOpen, setShortcutsOpen] = useState(false)
  const [userMenuAnchor, setUserMenuAnchor] = useState<HTMLElement | null>(null)
  const [environment, setEnvironment] = useState<string | null>(null)

  const persona = getPersona(user)
  const homeItem = getHomeNavItem()
  const rail = useMemo(() => getRailLayout(persona), [persona])
  const bottomNavItems = useMemo(() => getBottomNavItems(persona), [persona])
  // Workspaces scope admin resources; a plain user only sees what is shared with them.
  const showWorkspaceSwitcher = persona === 'Admin' || persona === 'SuperAdmin'

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

  const railContent = <NavRail items={rail.items} more={rail.more} pathname={location.pathname} />

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
          borderBottom: '1px solid',
          borderColor: 'divider',
          zIndex: (t) => t.zIndex.drawer + 1,
        }}
      >
        <Toolbar sx={{ minHeight: { xs: TOP_BAR_HEIGHT }, gap: { xs: 0.5, sm: 1 }, px: { xs: 1, sm: 2 } }}>
          {isTablet && (
            <IconButton
              edge="start"
              size="small"
              onClick={() => setDrawerOpenedAt(drawerOpen ? null : location.pathname)}
              aria-label={drawerOpen ? 'Close navigation' : 'Open navigation'}
              aria-expanded={drawerOpen}
            >
              <MenuIcon />
            </IconButton>
          )}
          <Stack
            direction="row"
            spacing={1}
            component={RouterLink}
            to="/"
            aria-label="R2WAI Studio"
            sx={{ alignItems: 'center', textDecoration: 'none', color: 'text.primary', mr: { xs: 0.5, sm: 1.5 }, flexShrink: 0 }}
          >
            <Box
              sx={{
                display: 'grid',
                placeItems: 'center',
                width: 28,
                height: 28,
                borderRadius: 1.5,
                background: (t) => `linear-gradient(135deg, ${t.palette.primary.main}, ${t.palette.secondary.main})`,
                color: '#fff',
              }}
            >
              <HubOutlinedIcon sx={{ fontSize: 18 }} />
            </Box>
            <Typography sx={{ display: { xs: 'none', sm: 'block' }, fontWeight: 650, fontSize: 15, whiteSpace: 'nowrap' }}>
              R2WAI Studio
            </Typography>
          </Stack>
          {showWorkspaceSwitcher && <WorkspaceSwitcher />}
          {environment && (
            <Chip
              size="small"
              icon={<CircleIcon sx={{ fontSize: '10px !important', color: `${environmentColor(environment)}.main` }} />}
              label={environment}
              variant="outlined"
              sx={{ display: { xs: 'none', lg: 'flex' } }}
            />
          )}

          <Box sx={{ flexGrow: 1 }} />

          <Box
            component="button"
            type="button"
            onClick={() => setPaletteOpen(true)}
            aria-label="Search agents, knowledge, tools"
            sx={{
              display: { xs: 'none', md: 'flex' },
              alignItems: 'center',
              gap: 1,
              width: { md: 280, lg: 360 },
              height: 34,
              px: 1.5,
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 1,
              bgcolor: 'action.hover',
              color: 'text.secondary',
              font: 'inherit',
              cursor: 'pointer',
              transition: (t) => t.transitions.create(['border-color', 'background-color'], { duration: 140, easing: 'ease' }),
              '&:hover': { borderColor: 'text.secondary', bgcolor: 'action.selected' },
            }}
          >
            <SearchIcon fontSize="small" />
            <Typography variant="body2" sx={{ flexGrow: 1, textAlign: 'left', color: 'text.secondary' }} noWrap>
              Search agents, knowledge, tools…
            </Typography>
            <Box
              component="kbd"
              sx={{
                fontSize: '0.75rem',
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
          <Box sx={{ flexGrow: { xs: 0, md: 1 } }} />
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
            <IconButton size="small" onClick={toggle} aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}>
              {isDark ? <LightModeIcon fontSize="small" /> : <DarkModeIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
          <NotificationBell />
          <Tooltip title="AI Copilot">
            <IconButton
              size="small"
              color={copilotOpen ? 'primary' : 'default'}
              onClick={() => setCopilotOpen((o) => !o)}
              aria-label="AI Copilot"
              aria-pressed={copilotOpen}
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
            sx={{ ml: 0.5, px: 0.75, py: 0.5, borderRadius: 2 }}
          >
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Avatar sx={{ width: 30, height: 30, fontSize: '0.8rem' }}>
                {(user?.displayName ?? user?.email ?? 'U').charAt(0).toUpperCase()}
              </Avatar>
              <Box sx={{ display: { xs: 'none', lg: 'block' }, lineHeight: 1.2, textAlign: 'left' }}>
                <Typography variant="caption" sx={{ display: 'block', fontWeight: 600 }}>
                  {user?.displayName ?? user?.email}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {getPersonaLabel(persona)}
                </Typography>
              </Box>
              <ExpandMoreIcon fontSize="small" sx={{ display: { xs: 'none', lg: 'block' }, color: 'text.secondary' }} />
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
            <MenuItem component={RouterLink} to="/memory" onClick={() => setUserMenuAnchor(null)}>
              Memory
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
        variant={isTablet ? 'temporary' : 'permanent'}
        open={isTablet ? drawerOpen : true}
        onClose={() => setDrawerOpenedAt(null)}
        ModalProps={{ keepMounted: true }}
        sx={{
          // Under the top bar on every size: the overlay rail opens below it (see its spacer).
          zIndex: (t) => t.zIndex.drawer,
          width: isTablet ? 0 : RAIL_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: RAIL_WIDTH,
            boxSizing: 'border-box',
            overflow: 'hidden',
            bgcolor: 'background.paper',
            borderRight: '1px solid',
            borderColor: 'divider',
          },
        }}
      >
        {/* Spacer so the rail starts below the full-width top bar. */}
        <Box sx={{ minHeight: TOP_BAR_HEIGHT, flexShrink: 0 }} />
        {railContent}
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
        }}
      >
        <Box sx={{ minHeight: TOP_BAR_HEIGHT }} />
        <Box
          aria-live="polite"
          aria-atomic="true"
          sx={{ position: 'absolute', width: 1, height: 1, p: 0, m: -1, overflow: 'hidden', clip: 'rect(0, 0, 0, 0)', whiteSpace: 'nowrap', border: 0 }}
        >
          {pageLabel}
        </Box>
        <Breadcrumbs sx={{ mb: { xs: 2, sm: 2.5 }, minHeight: 20 }} aria-label="Breadcrumb navigation">
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
          sx={{ position: 'fixed', bottom: 0, left: 0, right: 0, zIndex: (t) => t.zIndex.drawer + 1, borderTop: '1px solid', borderColor: 'divider', bgcolor: 'background.paper' }}
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
