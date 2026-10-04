import { describe, it, expect } from 'vitest'
import { getPersona, getNavSections, getBottomNavItems, getBreadcrumbs } from './roleNav'
import type { UserInfo } from '../auth/types'

function makeUser(roles: string[]): UserInfo {
  return {
    id: '1',
    email: 'a@b.com',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    avatarUrl: null,
    role: roles[0] ?? '',
    roles,
    tenantId: 't1',
    isActive: true,
    lastLoginAt: null,
    createdAt: '2026-01-01',
    updatedAt: null,
    mobileNumber: null,
    hasAadhaar: false,
  }
}

describe('getPersona', () => {
  it('is Public with no user', () => {
    expect(getPersona(null)).toBe('Public')
  })

  it('is SuperAdmin for SystemAdmin role, even combined with others', () => {
    expect(getPersona(makeUser(['SystemAdmin', 'Admin']))).toBe('SuperAdmin')
  })

  it('is Admin for the Admin role', () => {
    expect(getPersona(makeUser(['Admin']))).toBe('Admin')
  })

  it('does not treat a retired granular role (Editor/Contributor/WorkflowManager/UserManager) as Admin', () => {
    // These 4 roles were folded into Admin at the DB level (CollapseRbacToThreeRoles) — a real
    // account can no longer hold one, but this guards against ever reintroducing the old
    // UI-only-layering bug where the nav treated them as admin-adjacent without a real grant.
    for (const role of ['WorkflowManager', 'Editor', 'Contributor', 'UserManager']) {
      expect(getPersona(makeUser([role]))).toBe('User')
    }
  })

  it('defaults to User for a plain authenticated account', () => {
    expect(getPersona(makeUser(['User']))).toBe('User')
    expect(getPersona(makeUser([]))).toBe('User')
  })
})

describe('getNavSections', () => {
  it('renders the exact primary navigation as direct links for SuperAdmin', () => {
    const sections = getNavSections('SuperAdmin')
    const primaryItems = sections.map((section) => section.items[0])
    expect(primaryItems.map((item) => item.label)).toEqual([
      'Agents',
      'Connections',
      'Knowledge',
      'Automations',
      'Publish',
      'Activity',
      'Settings',
    ])
    expect(primaryItems.map((item) => item.path)).toEqual([
      '/assistants',
      '/workspaces',
      '/knowledge',
      '/automations',
      '/chatbots',
      '/runs',
      '/settings',
    ])
    expect(sections.map((section) => section.label)).toEqual([
      'Agents',
      'Connections',
      'Knowledge',
      'Automations',
      'Publish',
      'Activity',
      'Settings',
    ])
  })

  it('gives Admin the same primary links as SuperAdmin, minus restricted secondary items', () => {
    const sections = getNavSections('Admin')
    expect(sections.map((section) => section.items[0].label)).toEqual([
      'Agents',
      'Connections',
      'Knowledge',
      'Automations',
      'Publish',
      'Activity',
      'Settings',
    ])
  })

  it('surfaces the developer/API-key surface to Admin and SuperAdmin only', () => {
    // The developer contract is an admin surface: /developer composes the API-key and webhook
    // management whose create/revoke endpoints are Admin-gated. A plain User must not be
    // offered it, or the click lands on a 403.
    for (const persona of ['Admin', 'SuperAdmin'] as const) {
      const paths = getNavSections(persona).flatMap((s) => s.items.map((i) => i.path))
      expect(paths).toContain('/developer')
    }
    for (const persona of ['User', 'Public'] as const) {
      const paths = getNavSections(persona).flatMap((s) => s.items.map((i) => i.path))
      expect(paths.some((p) => p.startsWith('/developer'))).toBe(false)
    }
  })

  it('has no top-level distribution section, and no duplicate publish destination', () => {
    // Regression: a "Deploy" section gave "Chatbots" (Build) and "Website Widget" (Deploy) a
    // sidebar entry each for the same job, and the Channels page repeated "Website Widget" as
    // a tile. Chatbots is now the single door to distribution.
    for (const persona of ['Admin', 'SuperAdmin'] as const) {
      const sections = getNavSections(persona)
      expect(sections.some((s) => s.label === 'Deploy')).toBe(false)

      const paths = sections.flatMap((s) => s.items.map((i) => i.path))
      expect(paths.some((p) => p.startsWith('/deploy'))).toBe(false)
      expect(paths.filter((p) => p === '/chatbots')).toHaveLength(1)
    }
  })

  it('offers no Telegram entry anywhere, since it has no backend', () => {
    // Telegram has no enum member, adapter or webhook, so a nav link to it could only ever
    // lead to a screen that cannot work. See docs/api/MISSING-BACKEND-ENDPOINTS.md §2.3.
    for (const persona of ['Admin', 'SuperAdmin', 'User', 'Public'] as const) {
      const labels = getNavSections(persona)
        .flatMap((s) => s.items.map((i) => i.label))
        .join(' ')
      expect(labels).not.toMatch(/telegram/i)
    }
  })

  it('never lists the same path or label twice in one persona', () => {
    for (const persona of ['Admin', 'SuperAdmin', 'User', 'Public'] as const) {
      const items = getNavSections(persona).flatMap((s) => s.items)
      expect(items.map((i) => i.path)).toEqual([...new Set(items.map((i) => i.path))])
      expect(items.map((i) => i.label)).toEqual([...new Set(items.map((i) => i.label))])
    }
  })

  it('gives Admin and SuperAdmin a first-class Automations section', () => {
    for (const persona of ['Admin', 'SuperAdmin'] as const) {
      const sections = getNavSections(persona)
      const automations = sections.find((s) => s.label === 'Automations')
      expect(automations?.items.map((i) => i.path)).toEqual(['/automations'])
    }
  })

  it('keeps administration-only Automations out of the plain User menu', () => {
    const sections = getNavSections('User')
    expect(sections.some((s) => s.items.some((i) => i.path.startsWith('/automations')))).toBe(false)
  })

  it('uses Activity as the primary link while keeping confirmation and monitor destinations', () => {
    for (const persona of ['Admin', 'SuperAdmin'] as const) {
      const activity = getNavSections(persona).find((s) => s.label === 'Activity')
      expect(activity?.items.map((i) => [i.label, i.path])).toEqual([
        ['Activity', '/runs'],
        ['Confirmations', '/approvals'],
        ['Monitor', '/monitor'],
      ])
    }
  })

  it('keeps internal workflow terminology out of navigation labels', () => {
    for (const persona of ['User', 'Admin', 'SuperAdmin'] as const) {
      for (const item of getNavSections(persona).flatMap((s) => s.items)) {
        expect(item.label).not.toMatch(/\b(workflows?)\b/i)
      }
    }
  })

  it('gives Admin a limited Settings section without Security & Policies', () => {
    const settings = getNavSections('Admin').find((s) => s.label === 'Settings')
    expect(settings?.items.map((i) => i.label)).toEqual(['Settings', 'Users', 'API & SDK'])
  })

  it('gives Admin a limited Connections section without AI Models', () => {
    const connections = getNavSections('Admin').find((s) => s.label === 'Connections')
    expect(connections?.items.map((i) => i.label)).toEqual(['Connections', 'Integrations', 'MCP Servers'])
  })

  it('places Tools & APIs and AI Models under Connections for SuperAdmin only (ROADMAP.md §2)', () => {
    const sections = getNavSections('SuperAdmin')
    const connections = sections.find((s) => s.label === 'Connections')
    expect(connections?.items.some((i) => i.path === '/tools')).toBe(true)
    expect(connections?.items.some((i) => i.path === '/models')).toBe(true)
  })

  it('never surfaces Tools & APIs for Admin — CapabilitiesController is SystemAdmin-only, unaffected by the nav move', () => {
    const sections = getNavSections('Admin')
    expect(sections.some((s) => s.items.some((i) => i.path === '/tools'))).toBe(false)
  })

  it('uses Connections as the primary destination for Admin and SuperAdmin', () => {
    for (const persona of ['Admin', 'SuperAdmin'] as const) {
      const sections = getNavSections(persona)
      expect(sections.find((s) => s.label === 'Connections')?.items[0]).toMatchObject({
        label: 'Connections',
        path: '/workspaces',
      })
    }
  })

  it('keeps Departments out of primary navigation while preserving its direct route elsewhere', () => {
    for (const persona of ['User', 'Admin', 'SuperAdmin'] as const) {
      const paths = getNavSections(persona).flatMap((s) => s.items.map((i) => i.path))
      expect(paths).not.toContain('/departments')
    }
  })

  it('gives User a flat personal menu', () => {
    const sections = getNavSections('User')
    expect(sections).toHaveLength(1)
    expect(sections[0].items.some((i) => i.path === '/security')).toBe(false)
  })

  it('gives Public nothing', () => {
    expect(getNavSections('Public')).toEqual([])
  })
})

describe('getBottomNavItems', () => {
  it('keeps the compact mobile nav focused on daily destinations', () => {
    for (const persona of ['Public', 'User', 'Admin', 'SuperAdmin'] as const) {
      expect(getBottomNavItems(persona).some((i) => i.path.startsWith('/automations'))).toBe(false)
    }
  })

  it('keeps a fixed five-item quick-access row', () => {
    for (const persona of ['User', 'Admin', 'SuperAdmin'] as const) {
      expect(getBottomNavItems(persona)).toHaveLength(5)
    }
  })
})

describe('getBreadcrumbs', () => {
  it('uses route metadata for labels and inserts the Publish parent for channel pages', () => {
    expect(getBreadcrumbs('/deploy/widget', 'Admin')).toEqual([
      { label: 'Home', path: '/' },
      { label: 'Publish', path: '/chatbots' },
      { label: 'Channels', path: '/deploy' },
      { label: 'Website Widget', path: '/deploy/widget' },
    ])
  })

  it('hides only a complete GUID segment and keeps malformed ids visible', () => {
    expect(getBreadcrumbs('/assistants/123e4567-e89b-12d3-a456-426614174000', 'Admin')).toEqual([
      { label: 'Home', path: '/' },
      { label: 'Agents', path: '/assistants' },
    ])
    expect(getBreadcrumbs('/assistants/123e4567-e89b-12d3-a456-42661417400z', 'Admin')).toEqual([
      { label: 'Home', path: '/' },
      { label: 'Agents', path: '/assistants' },
      { label: '123e4567 E89b 12d3 A456 42661417400z', path: '/assistants/123e4567-e89b-12d3-a456-42661417400z' },
    ])
  })
})
