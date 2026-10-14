import { Box, Stack, Typography } from '@mui/material'
import type { SvgIconComponent } from '@mui/icons-material'

/**
 * The first column of a list-page table, in the Copilot Studio pattern: a small tinted icon, the
 * item's name, and one muted secondary line (description, owner, source count…). Shared by the
 * Agents, Knowledge, Automations, Publish and Activity tables so rows look the same everywhere.
 */
export function NameCell({ icon: Icon, name, secondary }: { icon: SvgIconComponent; name: string; secondary?: string | null }) {
  return (
    <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center', height: '100%', minWidth: 0 }}>
      <Box sx={{ display: 'grid', placeItems: 'center', width: 28, height: 28, borderRadius: 1, flexShrink: 0, bgcolor: 'action.hover', color: 'primary.main' }}>
        <Icon sx={{ fontSize: 16 }} />
      </Box>
      <Box sx={{ minWidth: 0, lineHeight: 1.3 }}>
        <Typography variant="body2" noWrap sx={{ fontWeight: 600 }}>{name}</Typography>
        {secondary && (
          <Typography variant="caption" color="text.secondary" noWrap component="div">
            {secondary}
          </Typography>
        )}
      </Box>
    </Stack>
  )
}
