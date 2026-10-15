import { Box, Card, CardContent, Grid, Skeleton, Stack } from '@mui/material'

interface LoadingSkeletonProps {
  /** Number of placeholder cards. Ignored when `variant` is 'table' or 'text'. */
  count?: number
  variant?: 'card' | 'table' | 'text'
  /** Table mode: how many placeholder rows. */
  rows?: number
  height?: number
}

/**
 * The shared "we're still fetching" placeholder. Pages previously inlined their own
 * Skeleton blocks with slightly different geometry per screen; this keeps the loading
 * state consistent and — more importantly — keeps it at the same footprint as the real
 * content so the layout does not jump when data lands.
 *
 * Deliberately skeleton-only: it never stands in for data, and callers pair it with
 * ErrorState when `query.error` is set rather than showing one in place of the other.
 */
const srOnly = { position: 'absolute', width: '1px', height: '1px', p: 0, m: '-1px', overflow: 'hidden', clip: 'rect(0 0 0 0)', whiteSpace: 'nowrap', border: 0 } as const

/** Screen-reader status for every skeleton variant — announced politely, invisible on screen. */
function LoadingAnnouncement() {
  return <Box component="span" sx={srOnly}>Loading…</Box>
}

export function LoadingSkeleton({ count = 6, variant = 'card', rows = 5, height }: LoadingSkeletonProps) {
  if (variant === 'text') {
    return (
      <Stack spacing={1} role="status" aria-busy="true">
        <LoadingAnnouncement />
        {Array.from({ length: count }).map((_, i) => (
          <Skeleton key={i} variant="text" height={height} />
        ))}
      </Stack>
    )
  }

  if (variant === 'table') {
    return (
      <Stack spacing={1} role="status" aria-busy="true">
        <LoadingAnnouncement />
        <Skeleton variant="rounded" height={40} />
        {Array.from({ length: rows }).map((_, i) => (
          <Skeleton key={i} variant="rounded" height={48} />
        ))}
      </Stack>
    )
  }

  return (
    <Grid container spacing={2} role="status" aria-busy="true">
      <LoadingAnnouncement />
      {Array.from({ length: count }).map((_, i) => (
        <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }}>
          <Card variant="outlined" sx={{ height: '100%' }}>
            <CardContent>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1.5 }}>
                <Skeleton variant="circular" width={28} height={28} />
                <Skeleton variant="text" width="60%" height={28} />
              </Stack>
              <Skeleton variant="text" sx={{ mb: 0.5 }} />
              <Skeleton variant="text" width="85%" />
              <Stack direction="row" spacing={1} sx={{ mt: 1.5 }}>
                <Skeleton variant="rounded" width={70} height={22} sx={{ borderRadius: 999 }} />
                <Skeleton variant="rounded" width={56} height={22} sx={{ borderRadius: 999 }} />
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}
