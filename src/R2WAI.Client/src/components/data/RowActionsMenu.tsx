import { useState } from 'react'
import { IconButton, ListItemText, Menu, MenuItem } from '@mui/material'
import MoreHorizIcon from '@mui/icons-material/MoreHoriz'

export interface RowAction {
  label: string
  onSelect: () => void
  /** Error colour, e.g. Delete. */
  destructive?: boolean
  disabled?: boolean
}

/**
 * The ⋯ menu at the end of a list-page table row (Copilot Studio pattern). Clicks are stopped
 * here so opening the menu never also triggers the row's own navigation.
 */
export function RowActionsMenu({ label, actions }: { label: string; actions: RowAction[] }) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  return (
    <>
      <IconButton
        size="small"
        aria-label={`Actions for ${label}`}
        aria-haspopup="menu"
        aria-expanded={anchor !== null}
        onClick={(event) => {
          event.stopPropagation()
          setAnchor(event.currentTarget)
        }}
        onKeyDown={(event) => event.stopPropagation()}
      >
        <MoreHorizIcon fontSize="small" />
      </IconButton>
      <Menu
        anchorEl={anchor}
        open={anchor !== null}
        onClose={() => setAnchor(null)}
        onClick={(event) => event.stopPropagation()}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {actions.map((action) => (
          <MenuItem
            key={action.label}
            disabled={action.disabled}
            onClick={() => {
              setAnchor(null)
              action.onSelect()
            }}
            sx={action.destructive ? { color: 'error.main' } : undefined}
          >
            <ListItemText primary={action.label} />
          </MenuItem>
        ))}
      </Menu>
    </>
  )
}
