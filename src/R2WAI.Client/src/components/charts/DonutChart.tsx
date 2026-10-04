import { Box, Stack, Typography } from '@mui/material'
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'

export interface DonutSegment {
  label: string
  value: number
  color: string
}

interface DonutChartProps {
  segments: DonutSegment[]
  total: number
  centerLabel?: string
  height?: number
}

export function DonutChart({ segments, total, centerLabel, height = 160 }: DonutChartProps) {
  return (
    <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
      <Box sx={{ position: 'relative', width: height, height }}>
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={segments}
              dataKey="value"
              nameKey="label"
              innerRadius="65%"
              outerRadius="100%"
              paddingAngle={2}
              stroke="none"
            >
              {segments.map((segment) => (
                <Cell key={segment.label} fill={segment.color} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        </ResponsiveContainer>
        <Box
          sx={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            pointerEvents: 'none',
          }}
        >
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            {total}
          </Typography>
          {centerLabel && (
            <Typography variant="caption" color="text.secondary">
              {centerLabel}
            </Typography>
          )}
        </Box>
      </Box>
      <Stack spacing={0.75}>
        {segments.map((segment) => (
          <Stack key={segment.label} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Box sx={{ width: 10, height: 10, borderRadius: '50%', bgcolor: segment.color }} />
            <Typography variant="body2">{segment.label}</Typography>
            <Typography variant="body2" color="text.secondary">
              {segment.value}
            </Typography>
          </Stack>
        ))}
      </Stack>
    </Stack>
  )
}
