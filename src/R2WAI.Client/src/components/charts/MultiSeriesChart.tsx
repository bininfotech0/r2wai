import { useTheme } from '@mui/material'
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

export interface SeriesPoint {
  label: string
  [seriesKey: string]: string | number
}

export interface SeriesDefinition {
  key: string
  label: string
  color?: string
}

interface MultiSeriesChartProps {
  data: SeriesPoint[]
  series: SeriesDefinition[]
  height?: number
  /** Renders the y axis in whole units; off for latency/percentage data. */
  integerAxis?: boolean
}

/**
 * Multi-series trend chart for the dashboard and analytics surfaces.
 *
 * Extends the existing single-series TrendLineChart rather than replacing it: most call
 * sites still plot one series, and keeping both means no existing chart changes shape.
 *
 * Renders an explicit empty frame rather than a bare div when there is no data, so a
 * tenant with a brand-new account sees "no data in this range" instead of an unexplained
 * blank card.
 */
export function MultiSeriesChart({ data, series, height = 240, integerAxis = true }: MultiSeriesChartProps) {
  const theme = useTheme()

  if (data.length === 0) {
    return (
      <StackedEmpty height={height} />
    )
  }

  return (
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke={theme.palette.divider} vertical={false} />
        <XAxis
          dataKey="label"
          tick={{ fontSize: 12 }}
          stroke={theme.palette.text.secondary}
          minTickGap={24}
        />
        <YAxis
          tick={{ fontSize: 12 }}
          stroke={theme.palette.text.secondary}
          width={44}
          allowDecimals={!integerAxis}
        />
        <Tooltip
          contentStyle={{
            background: theme.palette.background.paper,
            border: `1px solid ${theme.palette.divider}`,
            borderRadius: 8,
            fontSize: 12,
          }}
        />
        {series.length > 1 && <Legend wrapperStyle={{ fontSize: 12 }} />}
        {series.map((s) => (
          <Line
            key={s.key}
            type="monotone"
            dataKey={s.key}
            name={s.label}
            stroke={s.color ?? theme.palette.primary.main}
            strokeWidth={2}
            dot={false}
            connectNulls
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  )
}

function StackedEmpty({ height }: { height: number }) {
  return (
    <div
      style={{
        height,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: 'rgba(128,128,128,0.7)',
        fontSize: 13,
      }}
    >
      No activity recorded in this range yet
    </div>
  )
}
