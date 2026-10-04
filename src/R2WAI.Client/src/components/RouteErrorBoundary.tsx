import { Box, Container, Typography } from '@mui/material'
import { useRouteError } from 'react-router-dom'
import { ErrorState } from './ErrorState'

function getCorrelationId(error: unknown): string | null {
  if (typeof error !== 'object' || error === null || !('correlationId' in error)) return null
  const value = error.correlationId
  return typeof value === 'string' && value.trim() ? value : null
}

/** Safe recovery UI for route-render failures. Raw exception details are never shown. */
export function RouteErrorBoundary() {
  const error = useRouteError()
  const correlationId = getCorrelationId(error)

  return (
    <Box component="main" sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: 2 }}>
      <Container maxWidth="sm">
        <ErrorState
          title="This page could not be displayed"
          description="Try loading the page again. If the problem continues, contact your administrator."
          onRetry={() => window.location.reload()}
        />
        {correlationId && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center' }}>
            Error ID: {correlationId}
          </Typography>
        )}
      </Container>
    </Box>
  )
}
