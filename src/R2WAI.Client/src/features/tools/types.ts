// Mirrors R2WAI.Application.Features.Capabilities.DTOs.CapabilityDto (camelCase).
export type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical'

export const RISK_LEVELS: RiskLevel[] = ['Low', 'Medium', 'High', 'Critical']

export interface CapabilityDto {
  id: string
  applicationId: string | null
  applicationApiId: string | null
  // Resolved server-side from ToolDefinition.ApplicationApi.Name — null for a standalone
  // tool with no linked Integration (a direct EndpointUrl, no ApplicationApiId).
  applicationApiName: string | null
  name: string
  description: string | null
  httpMethod: string | null
  endpointPath: string | null
  isActive: boolean
  lastTestStatus: 'Connected' | 'Error' | null
  lastTestedAt: string | null
  riskLevel: RiskLevel
  requiredRole: string | null
  confirmationRequired: boolean
  approvalRequired: boolean
  auditRequired: boolean
  createdAt: string
  modifiedAt: string | null
}

export interface CapabilityFormInput {
  applicationId?: string
  applicationApiId?: string
  name: string
  description?: string
  httpMethod?: string
  endpointPath?: string
  riskLevel: RiskLevel
  requiredRole?: string
  confirmationRequired: boolean
  approvalRequired: boolean
  auditRequired: boolean
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface TestConnectionResult {
  success: boolean
  message: string
}

export interface ToolVersionDto {
  id: string
  toolDefinitionId: string
  versionNumber: number
  configSnapshot: string
  isPublished: boolean
  note: string | null
  publishedByUserId: string | null
  publishedAt: string | null
  createdAt: string
}
