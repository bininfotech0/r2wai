// Mirrors the PendingApprovalDto shape ApprovalService.GetPendingPagedAsync
// returns (src/R2WAI.Infrastructure/Services/ApprovalService.cs) — the
// entity itself has no RiskLevel field; EscalationLevel is the real signal
// driving urgency and is used here in its place (0 = normal, higher = more
// overdue/escalated), not a fabricated risk rating.
export interface PendingApprovalDto {
  id: string
  // The three workflow fields are null for a request that was not raised by a workflow step.
  workflowInstanceId: string | null
  workflowId: string | null
  workflowName: string | null
  // What is being decided, in words. Preferred over workflowName wherever a title is shown.
  subject: string | null
  // Real FK, one hop via the workflow definition. No Capability field exists — ApprovalRequest
  // has no ToolDefinition/BusinessCapability link anywhere, so there's nothing real to show there.
  applicationId: string | null
  applicationName: string | null
  requesterId: string
  requesterFirstName: string | null
  requesterLastName: string | null
  status: string
  requestedAt: string
  dueAt: string | null
  escalationLevel: number
  data: string | null
}

// GET /approvals?status=Approved|Rejected returns raw ApprovalRequest entity
// fields (not a DTO) — no requester/workflow name join, unlike the pending
// path. Shown as a plainer history list for that reason.
export interface ApprovalHistoryItem {
  id: string
  workflowInstanceId: string | null
  workflowId: string | null
  subject: string | null
  requesterId: string
  approverId: string | null
  approverRole: string | null
  status: string
  comments: string | null
  requestedAt: string
  respondedAt: string | null
  dueAt: string | null
  escalationLevel: number
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
