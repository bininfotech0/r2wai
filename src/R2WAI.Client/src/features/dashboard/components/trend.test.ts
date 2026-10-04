import { describe, expect, it } from 'vitest'
import { trendSeries } from './trend'
import type { DailyTrendPoint } from '../types'

const day = (d: string): DailyTrendPoint => ({ date: d, conversations: 1, runs: 2 })

describe('trendSeries', () => {
  it('unwraps the { days } envelope the endpoint actually returns', () => {
    // Regression: /operations/daily-trend?days=30 returns {"days":[...]}, not a bare array.
    // Calling .map on the envelope threw "(a.data ?? []).map is not a function" and
    // white-screened the dashboard.
    const result = trendSeries({ days: [day('2026-09-01'), day('2026-09-02')] })
    expect(result).toHaveLength(2)
    expect(result[0].date).toBe('2026-09-01')
  })

  it('still accepts a bare array, so a shape change degrades the chart instead of the page', () => {
    expect(trendSeries([day('2026-09-01')])).toHaveLength(1)
  })

  it('returns an empty series for absent data or an empty envelope', () => {
    expect(trendSeries(undefined)).toEqual([])
    expect(trendSeries(null)).toEqual([])
    expect(trendSeries({})).toEqual([])
    expect(trendSeries({ days: [] })).toEqual([])
  })
})
