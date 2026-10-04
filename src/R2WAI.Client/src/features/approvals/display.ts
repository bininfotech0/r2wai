import type { ApprovalHistoryItem, PendingApprovalDto } from './types'

const NEUTRAL_TITLE = 'Confirmation request'

// A confirmation is not necessarily raised by a workflow, so its title comes from what is being
// decided (subject) first, and only falls back to the workflow's name for an older request.
export function approvalTitle(a: Pick<PendingApprovalDto, 'subject' | 'workflowName'>): string {
  return a.subject || a.workflowName || NEUTRAL_TITLE
}

// GET /approvals?status=... returns the raw entity, which has a workflow id but no workflow name.
export function historyTitle(h: Pick<ApprovalHistoryItem, 'subject' | 'workflowId'>): string {
  if (h.subject) return h.subject
  return h.workflowId ? `Workflow ${h.workflowId.slice(0, 8)}…` : NEUTRAL_TITLE
}

// "View Details" opens the workflow run behind the request; a request with no workflow has none.
export function hasWorkflowRun(a: Pick<PendingApprovalDto, 'workflowInstanceId'>): boolean {
  return !!a.workflowInstanceId
}
