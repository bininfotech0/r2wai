import type { UserInfo } from '../auth/types'

export type RolePersona = 'Public' | 'User' | 'Admin' | 'SuperAdmin'

export function getPersona(user: UserInfo | null): RolePersona {
  if (!user) return 'Public'
  const roles = new Set(user.roles)
  if (roles.has('SystemAdmin')) return 'SuperAdmin'
  if (roles.has('Admin')) return 'Admin'
  return 'User'
}

export interface NavItem {
  label: string
  path: string
  icon: string
  /** Other paths that should also mark this item active (the rest of its tab group). */
  matchPaths?: string[]
}

export interface NavSection {
  label: string
  items: NavItem[]
}

export type BottomNavItem = NavItem

export interface BreadcrumbItem {
  label: string
  path: string
}

interface BottomNavPlacement {
  order: number
  label?: string
  icon?: string
}

interface NavigationEntry extends NavItem {
  id: string
  section?: string
  sectionsByPersona?: Partial<Record<RolePersona, string>>
  visibleTo: readonly RolePersona[]
  sidebar?: boolean
  routeLabel?: string
  breadcrumbParentPath?: string
  bottomNav?: Partial<Record<RolePersona, BottomNavPlacement>>
  labelsByPersona?: Partial<Record<RolePersona, string>>
  /**
   * Pages that are facets of one destination share a tab group: the sidebar shows only the
   * group's first entry, and MainLayout renders the group as tabs above each member page.
   */
  tabGroup?: string
  tabLabel?: string
}

const ADMIN_PERSONAS = ['Admin', 'SuperAdmin'] as const
const ALL_PERSONAS = ['User', 'Admin', 'SuperAdmin'] as const

/**
 * Navigation metadata lives in one registry. Role filtering, sidebar grouping, mobile shortcuts,
 * and route breadcrumbs are all derived from these entries so labels and paths don't drift
 * between the app shell surfaces.
 */
const NAVIGATION: readonly NavigationEntry[] = [
  {
    id: 'home', label: 'Home', path: '/', icon: 'Home', routeLabel: 'Home', visibleTo: ALL_PERSONAS,
    bottomNav: {
      User: { order: 0 }, Admin: { order: 0 }, SuperAdmin: { order: 0 }, Public: { order: 0 },
    },
  },
  {
    id: 'agents', section: 'Agents', label: 'Agents', path: '/assistants', icon: 'SmartToy', routeLabel: 'Agents', visibleTo: ADMIN_PERSONAS,
    bottomNav: {
      Admin: { order: 1 }, SuperAdmin: { order: 1 }, Public: { order: 1 },
    },
  },
  {
    id: 'playground', section: 'Agents', label: 'Playground', path: '/playground', icon: 'Science', routeLabel: 'Playground', visibleTo: ALL_PERSONAS,
    sectionsByPersona: { User: '' },
    labelsByPersona: { User: 'AI Assistant' },
    bottomNav: {
      User: { order: 1, label: 'Assistant', icon: 'SmartToy' },
      Admin: { order: 2, label: 'Chat' },
      SuperAdmin: { order: 2, label: 'Chat' },
      Public: { order: 2, label: 'Chat' },
    },
  },
  // One "Connections" destination: four pages that all answer "how does an agent reach an outside
  // system" used to be four sidebar items. Each keeps its own URL; they render as tabs instead.
  { id: 'connections', section: 'Connections', label: 'Connections', path: '/workspaces', icon: 'Apps', routeLabel: 'Connections', visibleTo: ADMIN_PERSONAS, tabGroup: 'connections', tabLabel: 'Connected Systems' },
  { id: 'integrations', label: 'Integrations', path: '/integrations', icon: 'Build', routeLabel: 'Integrations', breadcrumbParentPath: '/workspaces', visibleTo: ADMIN_PERSONAS, sidebar: false, tabGroup: 'connections' },
  { id: 'mcp', label: 'MCP Servers', path: '/mcp-connections', icon: 'RocketLaunch', routeLabel: 'MCP Servers', breadcrumbParentPath: '/workspaces', visibleTo: ADMIN_PERSONAS, sidebar: false, tabGroup: 'connections' },
  { id: 'tools', label: 'Tools & APIs', path: '/tools', icon: 'Extension', routeLabel: 'Tools & APIs', breadcrumbParentPath: '/workspaces', visibleTo: ['SuperAdmin'], sidebar: false, tabGroup: 'connections' },
  {
    id: 'knowledge', section: 'Knowledge', sectionsByPersona: { User: '' }, label: 'Knowledge', path: '/knowledge', icon: 'MenuBook', routeLabel: 'Knowledge', visibleTo: ALL_PERSONAS,
    bottomNav: { User: { order: 2 } },
  },
  { id: 'automations', section: 'Automations', label: 'Automations', path: '/automations', icon: 'AccountTree', routeLabel: 'Automations', visibleTo: ADMIN_PERSONAS },
  {
    id: 'publish', section: 'Publish', label: 'Publish', path: '/chatbots', icon: 'Forum', routeLabel: 'Publish', visibleTo: ADMIN_PERSONAS,
    tabGroup: 'publish', tabLabel: 'Chatbots',
  },
  { id: 'activity', section: 'Activity', label: 'Activity', path: '/runs', icon: 'PlayCircleOutline', routeLabel: 'Activity', visibleTo: ADMIN_PERSONAS },
  { id: 'confirmations', section: 'Activity', label: 'Confirmations', path: '/approvals', icon: 'FactCheck', routeLabel: 'Confirmations', visibleTo: ADMIN_PERSONAS },
  { id: 'monitor', section: 'Activity', label: 'Monitor', path: '/monitor', icon: 'MonitorHeart', routeLabel: 'Monitor', visibleTo: ADMIN_PERSONAS },
  { id: 'settings', section: 'Settings', label: 'Settings', path: '/settings', icon: 'Settings', routeLabel: 'Settings', visibleTo: ADMIN_PERSONAS },
  {
    id: 'users', section: 'Settings', label: 'Users & Roles', path: '/users', icon: 'PeopleAlt', routeLabel: 'Users & Roles', visibleTo: ADMIN_PERSONAS,
    labelsByPersona: { Admin: 'Users' },
  },
  { id: 'models', section: 'Settings', label: 'AI Models', path: '/models', icon: 'ModelTraining', routeLabel: 'AI Models', visibleTo: ['SuperAdmin'] },
  { id: 'security', section: 'Settings', label: 'Security & Policies', path: '/security', icon: 'Lock', routeLabel: 'Security & Policies', visibleTo: ['SuperAdmin'] },
  { id: 'developer', section: 'Settings', label: 'API & SDK', path: '/developer', icon: 'Code', routeLabel: 'API & SDK', visibleTo: ADMIN_PERSONAS },
  {
    id: 'my-activity', label: 'My Activity', path: '/runs', icon: 'History', routeLabel: 'Activity', visibleTo: ['User'],
  },
  {
    id: 'inbox', label: 'Notifications', path: '/inbox', icon: 'NotificationsActive', routeLabel: 'Inbox', visibleTo: ['User'],
    bottomNav: {
      User: { order: 3, label: 'Inbox' }, Admin: { order: 3, label: 'Inbox' },
      SuperAdmin: { order: 3, label: 'Inbox' }, Public: { order: 3, label: 'Inbox' },
    },
  },
  {
    id: 'profile', label: 'Profile', path: '/profile', icon: 'Person', routeLabel: 'Profile', visibleTo: ['User'],
    bottomNav: {
      User: { order: 4 }, Admin: { order: 4 }, SuperAdmin: { order: 4 }, Public: { order: 4 },
    },
  },

  // Route-only entries supply breadcrumb names for work surfaces that are reached from another
  // page, or whose path differs from the customer-facing name in the sidebar.
  { id: 'departments-route', label: 'Departments', path: '/departments', icon: 'CorporateFare', routeLabel: 'Departments', visibleTo: ALL_PERSONAS, sidebar: false },
  { id: 'automation-detail-route', label: 'Automations', path: '/automations/:id', icon: 'AccountTree', routeLabel: 'Automations', visibleTo: ADMIN_PERSONAS, sidebar: false },
  { id: 'automation-builder-route', label: 'Builder', path: '/automations/:id/builder', icon: 'AccountTree', routeLabel: 'Builder', visibleTo: ADMIN_PERSONAS, sidebar: false },
  { id: 'chatbot-detail-route', label: 'Chatbots', path: '/chatbots/:id', icon: 'Forum', routeLabel: 'Chatbots', visibleTo: ADMIN_PERSONAS, sidebar: false },
  { id: 'channels-route', label: 'Channels', path: '/deploy', icon: 'Forum', routeLabel: 'Channels', breadcrumbParentPath: '/chatbots', visibleTo: ADMIN_PERSONAS, sidebar: false, tabGroup: 'publish' },
  { id: 'widget-route', label: 'Website Widget', path: '/deploy/widget', icon: 'Language', routeLabel: 'Website Widget', visibleTo: ADMIN_PERSONAS, sidebar: false, tabGroup: 'publish' },
  { id: 'widget-chatbot-route', label: 'Website Widget', path: '/deploy/widget/:chatbotId', icon: 'Language', routeLabel: 'Website Widget', visibleTo: ADMIN_PERSONAS, sidebar: false },
  { id: 'whatsapp-route', label: 'WhatsApp', path: '/deploy/whatsapp/:chatbotId', icon: 'Forum', routeLabel: 'WhatsApp', visibleTo: ADMIN_PERSONAS, sidebar: false },
  { id: 'about-route', label: 'About', path: '/about', icon: 'Help', routeLabel: 'About', visibleTo: ALL_PERSONAS, sidebar: false },
]

const PERSONA_LABELS: Record<RolePersona, string> = {
  Public: '',
  User: 'User',
  Admin: 'Admin',
  SuperAdmin: 'Super Admin',
}

function isVisibleTo(entry: NavigationEntry, persona: RolePersona): boolean {
  return entry.visibleTo.includes(persona)
}

function tabGroupMembers(group: string, persona: RolePersona): NavigationEntry[] {
  return NAVIGATION.filter((entry) => entry.tabGroup === group && isVisibleTo(entry, persona))
}

function itemForPersona(entry: NavigationEntry, persona: RolePersona): NavItem {
  const item: NavItem = {
    label: entry.labelsByPersona?.[persona] ?? entry.label,
    path: entry.path,
    icon: entry.icon,
  }
  if (entry.tabGroup) {
    const others = tabGroupMembers(entry.tabGroup, persona).map((member) => member.path).filter((path) => path !== entry.path)
    if (others.length > 0) item.matchPaths = others
  }
  return item
}

export function getHomeNavItem(): NavItem {
  const home = NAVIGATION.find((entry) => entry.id === 'home')
  if (!home) throw new Error('The navigation registry must define a home destination.')
  return itemForPersona(home, 'Admin')
}

export function getNavSections(persona: RolePersona): NavSection[] {
  const groups = new Map<string, NavItem[]>()

  for (const entry of NAVIGATION) {
    if (entry.id === 'home' || entry.sidebar === false || !isVisibleTo(entry, persona)) continue
    const sectionLabel = entry.sectionsByPersona?.[persona] ?? entry.section ?? ''
    const items = groups.get(sectionLabel) ?? []
    items.push(itemForPersona(entry, persona))
    groups.set(sectionLabel, items)
  }

  return [...groups].map(([label, items]) => ({ label, items }))
}

export function getBottomNavItems(persona: RolePersona): BottomNavItem[] {
  const placements: { order: number; item: BottomNavItem }[] = []
  for (const entry of NAVIGATION) {
    const placement = entry.bottomNav?.[persona]
    if (!placement) continue
    const item = itemForPersona(entry, persona)
    placements.push({
      order: placement.order,
      item: { ...item, label: placement.label ?? item.label, icon: placement.icon ?? item.icon },
    })
  }
  return placements.sort((a, b) => a.order - b.order).map(({ item }) => item)
}

function matchesRoute(pattern: string, pathname: string): boolean {
  const patternSegments = pattern.split('/').filter(Boolean)
  const pathSegments = pathname.split('/').filter(Boolean)
  return patternSegments.length === pathSegments.length && patternSegments.every((segment, index) =>
    segment.startsWith(':') || segment === pathSegments[index],
  )
}

function routeEntry(pathname: string, persona: RolePersona): NavigationEntry | undefined {
  return NAVIGATION
    .filter((entry) => entry.routeLabel && isVisibleTo(entry, persona) && matchesRoute(entry.path, pathname))
    .sort((a, b) => b.path.length - a.path.length)[0]
}

function titleFromPathSegment(segment: string): string {
  return segment
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase())
}

function isGuidLike(segment: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(segment)
}

export function getBreadcrumbs(pathname: string, persona: RolePersona): BreadcrumbItem[] {
  const home = getHomeNavItem()
  const breadcrumbs: BreadcrumbItem[] = [{ label: home.label, path: home.path }]
  let accumulatedPath = ''

  for (const segment of pathname.split('/').filter(Boolean)) {
    accumulatedPath += `/${segment}`
    const entry = routeEntry(accumulatedPath, persona)

    if (entry?.breadcrumbParentPath && !breadcrumbs.some((crumb) => crumb.path === entry.breadcrumbParentPath)) {
      const parent = routeEntry(entry.breadcrumbParentPath, persona)
      if (parent) breadcrumbs.push({ label: parent.routeLabel!, path: entry.breadcrumbParentPath })
    }

    if (isGuidLike(segment)) continue
    breadcrumbs.push({
      label: entry?.routeLabel ?? titleFromPathSegment(segment),
      path: accumulatedPath,
    })
  }

  return breadcrumbs
}

export interface SectionTabs {
  tabs: NavItem[]
  /** Path of the tab the current page belongs to. */
  activePath: string
}

/** The tab strip for a page that belongs to a tab group, or null for every other page. */
export function getSectionTabs(pathname: string, persona: RolePersona): SectionTabs | null {
  const entry = routeEntry(pathname, persona)
  if (!entry?.tabGroup) return null
  const members = tabGroupMembers(entry.tabGroup, persona)
  if (members.length < 2) return null
  return {
    tabs: members.map((member) => ({ label: member.tabLabel ?? member.label, path: member.path, icon: member.icon })),
    activePath: entry.path,
  }
}

export function getPersonaLabel(persona: RolePersona): string {
  return PERSONA_LABELS[persona]
}
