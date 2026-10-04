import { Box, Button, Typography } from '@mui/material'
import ErrorOutlineOutlined from '@mui/icons-material/ErrorOutlineOutlined'

interface ErrorStateProps {
  title?: string
  description?: string
  onRetry?: () => void
  retryLabel?: string
}

/**
 * A business-friendly failure state ("Unable to retrieve..." + Try Again),
 * replacing the one-off MUI `Alert` blocks pages currently roll individually.
 * Never render the raw exception here — callers pass a human description.
 */
export function ErrorState({
  title = 'Unable to load this data.',
  description,
  onRetry,
  retryLabel = 'Try Again',
}: ErrorStateProps) {
  return (
    <Box sx={{ textAlign: 'center', py: { xs: 5, sm: 7 }, px: 3, maxWidth: 520, mx: 'auto' }}>
      <Box
        sx={{
          display: 'grid',
          placeItems: 'center',
          width: 60,
          height: 60,
          mx: 'auto',
          mb: 2,
          borderRadius: 3,
          color: 'error.main',
          bgcolor: (theme) => `color-mix(in srgb, ${theme.palette.error.main} 9%, ${theme.palette.background.paper})`,
          border: '1px solid',
          borderColor: 'divider',
        }}
      >
        <ErrorOutlineOutlined sx={{ fontSize: 27 }} />
      </Box>
      <Typography variant="h6" component="h2" sx={{ fontWeight: 650 }}>
        {title}
      </Typography>
      {description && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75, maxWidth: 440, mx: 'auto' }}>
          {description}
        </Typography>
      )}
      {onRetry && (
        <Button variant="outlined" onClick={onRetry} sx={{ mt: 2 }}>
          {retryLabel}
        </Button>
      )}
    </Box>
  )
}
