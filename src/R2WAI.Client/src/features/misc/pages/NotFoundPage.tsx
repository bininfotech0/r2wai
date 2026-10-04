import { Box, Button, Paper, Stack, Typography } from '@mui/material'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'
import { useDocumentTitle } from '../../../lib/useDocumentTitle'

/** Product 404 for unmatched routes. */
export function NotFoundPage() {
  useDocumentTitle('Page not found · R2WAI Studio')
  const navigate = useNavigate()
  const { pathname } = useLocation()

  return (
    <Box sx={{ maxWidth: 720, mx: 'auto', py: { xs: 3, sm: 6, md: 10 } }}>
      <Paper variant="outlined" sx={{ p: { xs: 3, sm: 5 }, textAlign: 'center' }}>
        <Typography variant="overline" color="primary.main" sx={{ fontWeight: 700, letterSpacing: 1.5 }}>
          Error 404
        </Typography>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 700, mt: 0.5 }}>
          Page not found
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mt: 1.5 }}>
          We couldn’t find this page. The link may be out of date, or the address may have a typo.
        </Typography>
        <Typography
          component="code"
          variant="body2"
          sx={{ display: 'block', mt: 2, color: 'text.secondary', overflowWrap: 'anywhere' }}
        >
          {pathname}
        </Typography>
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1.25}
          sx={{ mt: 3, justifyContent: 'center', flexWrap: 'wrap' }}
        >
          <Button variant="contained" onClick={() => navigate('/')}>
            Go to Home
          </Button>
          <Button component={RouterLink} to="/workspaces" variant="outlined">
            Connected Systems
          </Button>
          <Button component={RouterLink} to="/assistants" variant="text">
            AI Assistants
          </Button>
        </Stack>
      </Paper>
    </Box>
  )
}
