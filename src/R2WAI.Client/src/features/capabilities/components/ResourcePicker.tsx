import { useState } from 'react'
import { Checkbox, List, ListItemButton, ListItemIcon, ListItemText, TextField, Typography } from '@mui/material'

interface ResourcePickerItem {
  id: string
  name: string
  description?: string | null
}

interface ResourcePickerProps {
  items: ResourcePickerItem[]
  selectedIds: string[]
  onToggle: (id: string) => void
  searchPlaceholder?: string
  emptyMessage?: string
}

/** A searchable checkbox list — the shared picker for linking Tools/Knowledge/Workflows into a Capability. */
export function ResourcePicker({
  items,
  selectedIds,
  onToggle,
  searchPlaceholder = 'Search…',
  emptyMessage = 'Nothing available yet.',
}: ResourcePickerProps) {
  const [search, setSearch] = useState('')
  const filtered = items.filter((i) => i.name.toLowerCase().includes(search.toLowerCase()))

  return (
    <>
      <TextField
        fullWidth
        size="small"
        placeholder={searchPlaceholder}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        sx={{ mb: 1 }}
      />
      <List dense sx={{ maxHeight: 320, overflowY: 'auto' }}>
        {filtered.map((item) => (
          <ListItemButton key={item.id} onClick={() => onToggle(item.id)}>
            <ListItemIcon>
              <Checkbox edge="start" checked={selectedIds.includes(item.id)} tabIndex={-1} disableRipple />
            </ListItemIcon>
            <ListItemText primary={item.name} secondary={item.description} />
          </ListItemButton>
        ))}
        {filtered.length === 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
            {items.length === 0 ? emptyMessage : 'No matches.'}
          </Typography>
        )}
      </List>
    </>
  )
}
