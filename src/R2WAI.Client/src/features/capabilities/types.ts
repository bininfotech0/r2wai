// Mirrors R2WAI.Application.Features.BusinessCapabilities.DTOs.BusinessCapabilityDto (camelCase).
// Deliberately a distinct concept from tools/types.ts's CapabilityDto (a single governed
// tool/API operation, internal-only, always shown to users as "Tool") — this is the brief's
// user-facing "Capability": a business-oriented bundle of Tools/Knowledge/Workflows under one
// assistant (e.g. "Invoice Management").
export type BusinessCapabilityStatus = 'Draft' | 'Active' | 'Disabled'

export const BUSINESS_CAPABILITY_STATUSES: BusinessCapabilityStatus[] = ['Draft', 'Active', 'Disabled']

export interface BusinessCapabilityDto {
  id: string
  assistantId: string
  name: string
  description: string | null
  icon: string | null
  status: BusinessCapabilityStatus
  toolIds: string[]
  knowledgeBaseIds: string[]
  workflowIds: string[]
  applicationApiIds: string[]
  toolCount: number
  knowledgeBaseCount: number
  workflowCount: number
  apiCount: number
  createdAt: string
  modifiedAt: string | null
}

export interface BusinessCapabilityFormInput {
  assistantId: string
  name: string
  description?: string
  icon?: string
  toolIds?: string[]
  knowledgeBaseIds?: string[]
  workflowIds?: string[]
  applicationApiIds?: string[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
