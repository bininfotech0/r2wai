// Mirrors R2WAI.Application.Features.Governance.DTOs.GlobalPolicyDto.
// Type is a client-side convention (matches the Blazor Governance.razor
// precedent), not a backend enum — GlobalPolicy.Type is a free string.
export const POLICY_TYPES = ['AiUsage', 'DataRetention', 'Pii', 'ToolExecution', 'Approval', 'Knowledge', 'Auth', 'AgentRuntime'] as const
export type PolicyType = (typeof POLICY_TYPES)[number]

export interface GlobalPolicyDto {
  id: string
  type: string
  name: string
  content: string | null
  isActive: boolean
  createdAt: string
  modifiedAt: string | null
}

export interface UpsertPolicyInput {
  type: string
  name: string
  content?: string
  isActive: boolean
}

// Mirrors R2WAI.Application.Features.Governance.DTOs.AuthSecurityStatusDto.
export interface AuthSecurityStatusDto {
  totalUsers: number
  mfaEnabledUsers: number
  ssoConfigured: boolean
  requireMfaPolicyActive: boolean
}
