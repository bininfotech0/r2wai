import { useMemo, useState } from 'react'
import { Command } from 'cmdk'
import { Dialog, Box } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { getNavSections, getPersona } from '../lib/nav/roleNav'
import { useAuth } from '../lib/auth/useAuth'
import './CommandPalette.css'

interface CommandPaletteProps {
  open: boolean
  onClose: () => void
  isDark: boolean
  onToggleTheme: () => void
  onToggleCopilot: () => void
  onShowShortcuts: () => void
}

// Ctrl+K shell — filled in incrementally per phase (each feature area added
// its own nav commands as it landed); Phase 13 adds the first real action
// commands (not just nav-jumps) plus a persistent-actions group, closing out
// the plan's "full command-palette registry across all ported areas."
export function CommandPalette({ open, onClose, isDark, onToggleTheme, onToggleCopilot, onShowShortcuts }: CommandPaletteProps) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')

  const persona = getPersona(user)
  const sections = useMemo(() => getNavSections(persona), [persona])

  function go(path: string) {
    navigate(path)
    onClose()
  }

  function run(action: () => void) {
    action()
    onClose()
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth slotProps={{ paper: { sx: { overflow: 'hidden' } } }}>
      <Command shouldFilter label="Command palette">
        <Box sx={{ p: 1.5, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Command.Input
            autoFocus
            value={search}
            onValueChange={setSearch}
            placeholder="Search assistants, knowledge, tools…"
            className="command-palette-input"
          />
        </Box>
        <Command.List className="command-palette-list">
          <Command.Empty className="command-palette-empty">No results found.</Command.Empty>

          <Command.Group heading="Actions">
            <Command.Item onSelect={() => go('/')}>Go to Home</Command.Item>
            <Command.Item onSelect={() => go('/profile')}>Go to Profile</Command.Item>
            <Command.Item onSelect={() => go('/inbox')}>Go to Inbox</Command.Item>
            <Command.Item onSelect={() => go('/settings')}>Go to Settings</Command.Item>
            <Command.Item onSelect={() => run(onToggleCopilot)}>Toggle AI Copilot panel</Command.Item>
            <Command.Item onSelect={() => run(onToggleTheme)}>{isDark ? 'Switch to light mode' : 'Switch to dark mode'}</Command.Item>
            <Command.Item onSelect={() => run(onShowShortcuts)}>Show keyboard shortcuts</Command.Item>
            <Command.Item onSelect={() => run(() => void logout())}>Sign out</Command.Item>
          </Command.Group>

          {sections.map((section) => (
            <Command.Group key={section.label || 'nav'} heading={section.label || undefined}>
              {section.items.map((item) => (
                <Command.Item key={item.path} onSelect={() => go(item.path)}>
                  {item.label}
                </Command.Item>
              ))}
            </Command.Group>
          ))}
        </Command.List>
      </Command>
    </Dialog>
  )
}
