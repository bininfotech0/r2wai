import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  AiStatsDto,
  AuditLogDto,
  CreateTestCaseInput,
  DailyTrendPoint,
  ErrorLogItem,
  MetricsDto,
  PagedResult,
  ReportSummary,
  TestCaseDto,
  TestRunDto,
  UpdateTestCaseInput,
  UsageAnalyticsDto,
} from './types'

const postJson = sharedPostJson

export function getMetrics() {
  return fetchJson<MetricsDto>('/operations/metrics')
}

export function getDailyTrend(days = 7) {
  return fetchJson<{ days: DailyTrendPoint[] }>(`/operations/daily-trend?days=${days}`)
}

export function getAiStats() {
  return fetchJson<AiStatsDto>('/operations/ai-stats')
}

export function getErrorLogs(level?: string, correlationId?: string, pageSize = 50) {
  const params = new URLSearchParams({ pageSize: String(pageSize) })
  if (level) params.set('level', level)
  if (correlationId) params.set('correlationId', correlationId)
  return fetchJson<{ items: ErrorLogItem[] }>(`/operations/errors?${params.toString()}`)
}

export interface AuditLogFilters {
  userId?: string
  applicationId?: string
  entityType?: string
  action?: string
  from?: string
  to?: string
}

export function listAuditLogs(filters: AuditLogFilters, page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  for (const [key, value] of Object.entries(filters)) {
    if (value) params.set(key, value)
  }
  return fetchJson<PagedResult<AuditLogDto>>(`/operations/audit-logs?${params.toString()}`)
}

export async function downloadAuditLogsExport(format: 'csv' | 'json', filters: AuditLogFilters): Promise<Blob> {
  const params = new URLSearchParams({ format })
  for (const [key, value] of Object.entries(filters)) {
    if (value) params.set(key, value)
  }
  const response = await authFetch(`/api/v1/operations/audit-logs/export?${params.toString()}`)
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to export audit logs')
  return response.blob()
}

export function listReports() {
  return fetchJson<{ items: ReportSummary[]; total: number }>('/operations/reports')
}

export function generateReport(type: 'cost' | 'assistant') {
  return postJson<Record<string, unknown>>('/operations/reports/generate', { type })
}

export async function downloadReport(type: string, format: 'csv' | 'json'): Promise<Blob> {
  const response = await authFetch(`/api/v1/operations/reports/download?type=${type}&format=${format}`)
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to download report')
  return response.blob()
}

export function getUsageAnalytics(days = 30) {
  return fetchJson<UsageAnalyticsDto>(`/admin/analytics/usage?days=${days}`)
}

export function getUsageTrends(days = 30) {
  return fetchJson<{ days: unknown[] }>(`/admin/analytics/trends?days=${days}`)
}

// --- Test Cases / Test Runs ---

export function listTestCases(page: number, pageSize: number, search?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<TestCaseDto>>(`/testcases?${params.toString()}`)
}

export function createTestCase(input: CreateTestCaseInput) {
  return postJson<TestCaseDto>('/testcases', input)
}

export async function updateTestCase(id: string, input: UpdateTestCaseInput): Promise<TestCaseDto> {
  const response = await authFetch(`/api/v1/testcases/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update test case')
  return (await response.json()) as TestCaseDto
}

export async function deleteTestCase(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/testcases/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete test case')
}

export function runTestCase(id: string) {
  return postJson<TestRunDto>(`/testcases/${id}/run`, {})
}

export function runAllTestCases(assistantId?: string) {
  return postJson<TestRunDto>('/testcases/run-all', { assistantId })
}

export function listTestRuns(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<PagedResult<TestRunDto>>(`/testruns?${params.toString()}`)
}

export function getTestRun(id: string) {
  return fetchJson<TestRunDto>(`/testruns/${id}`)
}
