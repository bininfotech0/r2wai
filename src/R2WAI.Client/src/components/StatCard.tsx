import type { SvgIconComponent } from '@mui/icons-material'
import { Box, Paper, Skeleton, Stack, Typography } from '@mui/material'
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward'
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward'

type StatColor = 'primary' | 'secondary' | 'success' | 'warning' | 'info' | 'error'

interface StatCardProps {
  label: string
  value: string | number
  trendPercent?: number | null
  icon?: SvgIconComponent
  color?: StatColor
  loading?: boolean
}

export function StatCard({ label, value, trendPercent, icon: Icon, color = 'primary', loading }: StatCardProps) {
  const hasTrend = trendPercent !== undefined && trendPercent !== null
  const isUp = hasTrend && trendPercent! >= 0

  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.75, sm: 2.25 },
        flex: 1,
        minWidth: { xs: 0, sm: 160 },
        minHeight: 112,
        background: (t) => `linear-gradient(145deg, ${t.palette.background.paper}, color-mix(in srgb, ${t.palette[color].main} 2.5%, ${t.palette.background.paper}))`,
      }}
    >
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block', fontWeight: 550 }}>
            {label}
          </Typography>
          {loading ? (
            <Skeleton variant="text" width={56} height={36} sx={{ mt: 0.5 }} />
          ) : (
            <Typography variant="h4" sx={{ mt: 0.6, fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
              {value}
            </Typography>
          )}
        </Box>
        {Icon && (
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 40,
              height: 40,
              borderRadius: 2.5,
              flexShrink: 0,
              bgcolor: (t) =>
                t.palette.mode === 'dark'
                  ? `color-mix(in srgb, ${t.palette[color].main} 18%, ${t.palette.background.paper})`
                  : `color-mix(in srgb, ${t.palette[color].main} 10%, ${t.palette.background.paper})`,
              color: `${color}.main`,
            }}
          >
            <Icon fontSize="small" />
          </Box>
        )}
      </Stack>
      {loading ? (
        <Skeleton variant="text" width={64} height={20} sx={{ mt: 0.5 }} />
      ) : (
        hasTrend && (
          <Stack direction="row" spacing={0.5} sx={{ mt: 0.5, alignItems: 'center' }}>
            {isUp ? (
              <ArrowUpwardIcon sx={{ fontSize: 14, color: 'success.main' }} />
            ) : (
              <ArrowDownwardIcon sx={{ fontSize: 14, color: 'error.main' }} />
            )}
            <Typography variant="caption" sx={{ color: isUp ? 'success.main' : 'error.main' }}>
              {Math.abs(trendPercent!).toFixed(1)}%
            </Typography>
          </Stack>
        )
      )}
    </Paper>
  )
}
