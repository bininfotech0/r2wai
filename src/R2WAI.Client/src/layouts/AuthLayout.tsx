import { Box } from '@mui/material'
import { Outlet, useNavigation } from 'react-router-dom'
import { LoadingSkeleton } from '../components/LoadingSkeleton'

// Bare layout for auth pages (login/forgot/reset) — no app chrome, mirrors
// R2WAI.Web's AuthLayout.razor.
export function AuthLayout() {
  const navigation = useNavigation()
  return (
    <Box
      sx={{
        position: 'relative',
        minHeight: '100dvh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        px: 2,
        py: { xs: 3, sm: 5 },
        bgcolor: 'background.default',
        backgroundImage: (theme) => `radial-gradient(ellipse at 8% 8%, color-mix(in srgb, ${theme.palette.primary.main} 12%, transparent), transparent 36%), radial-gradient(ellipse at 96% 92%, color-mix(in srgb, ${theme.palette.secondary.main} 10%, transparent), transparent 34%)`,
      }}
    >
      {navigation.state === 'loading'
        ? <Box sx={{ width: '100%', maxWidth: 480, px: 3 }}><LoadingSkeleton variant="text" count={8} /></Box>
        : <Outlet />}
    </Box>
  )
}
