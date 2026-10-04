import type { SvgIconComponent } from '@mui/icons-material'
import { Box, Button, Typography } from '@mui/material'
import InboxOutlined from '@mui/icons-material/InboxOutlined'

interface EmptyStateProps {
  icon?: SvgIconComponent
  title: string
  description?: string
  actionLabel?: string
  onAction?: () => void
}

/**
 * The "No Capabilities... [+ Add Capability]" pattern (brief §26) — a
 * professional empty state for any module with nothing in it yet, instead
 * of each page rolling its own placeholder text.
 */
export function EmptyState({ icon: Icon = InboxOutlined, title, description, actionLabel, onAction }: EmptyStateProps) {
  return (
    <Box sx={{ textAlign: 'center', py: { xs: 2, sm: 6 }, px: 3, maxWidth: 520, mx: 'auto' }}>
      <Box
        sx={{
          display: 'grid',
          placeItems: 'center',
          width: { xs: 52, sm: 60 },
          height: { xs: 52, sm: 60 },
          mx: 'auto',
          mb: { xs: 1.25, sm: 2 },
          borderRadius: 3,
          color: 'primary.main',
          bgcolor: (theme) => `color-mix(in srgb, ${theme.palette.primary.main} 9%, ${theme.palette.background.paper})`,
          border: '1px solid',
          borderColor: 'divider',
        }}
      >
        <Icon sx={{ fontSize: 27 }} />
      </Box>
      <Typography variant="h6" component="h2" sx={{ fontWeight: 650 }}>
        {title}
      </Typography>
      {description && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, maxWidth: 440, mx: 'auto' }}>
          {description}
        </Typography>
      )}
      {actionLabel && onAction && (
        <Button variant="contained" onClick={onAction} sx={{ mt: { xs: 1.25, sm: 2 } }}>
          {actionLabel}
        </Button>
      )}
    </Box>
  )
}
