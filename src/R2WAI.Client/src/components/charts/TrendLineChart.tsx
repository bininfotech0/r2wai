import { useTheme } from '@mui/material'
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis } from 'recharts'

export interface TrendPoint {
  label: string
  value: number
}

interface TrendLineChartProps {
  data: TrendPoint[]
  height?: number
}

export function TrendLineChart({ data, height = 200 }: TrendLineChartProps) {
  const theme = useTheme()
  return (
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
        <XAxis dataKey="label" tick={{ fontSize: 12 }} stroke={theme.palette.text.secondary} />
        <Tooltip />
        <Line
          type="monotone"
          dataKey="value"
          stroke={theme.palette.primary.main}
          strokeWidth={2}
          dot={false}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}
