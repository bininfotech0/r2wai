import { fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type { ApprovalHistoryItem, PagedResult, PendingApprovalDto } from './types'

export function listPendingApprovals(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<PagedResult<PendingApprovalDto>>(`/approvals/pending?${params.toString()}`)
}

export function listApprovalHistory(status: 'Approved' | 'Rejected', page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), status })
  return fetchJson<PagedResult<ApprovalHistoryItem>>(`/approvals?${params.toString()}`)
}

const postJson = sharedPostJson

export function approveRequest(id: string, comments?: string) {
  return postJson(`/approvals/${id}/approve`, { comments })
}

export function rejectRequest(id: string, comments?: string) {
  return postJson(`/approvals/${id}/reject`, { comments })
}
