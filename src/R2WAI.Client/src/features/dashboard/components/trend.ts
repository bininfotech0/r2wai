import type { DailyTrendPoint } from '../types'

/**
 * `/operations/daily-trend?days=N` returns the series wrapped in an envelope —
 * `{ "days": [ { date, conversations, runs }, ... ] }` — mirroring
 * R2WAI.Application.Features.Operations.DTOs.DailyTrendDto. It does NOT return a bare array.
 *
 * Reading it as an array throws on `.map` and takes down the entire dashboard, so the envelope
 * is unwrapped in one tested place instead of being re-derived at each call site. A bare array
 * is still accepted, because the failure mode here is an unrenderable dashboard rather than a
 * merely imperfect chart.
 */
export function trendSeries(
  data: { days?: DailyTrendPoint[] } | DailyTrendPoint[] | null | undefined,
): DailyTrendPoint[] {
  if (!data) return []
  if (Array.isArray(data)) return data
  return data.days ?? []
}
