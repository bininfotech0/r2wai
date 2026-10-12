/** Mirrors R2WAI.Application.Features.Workspaces.DTOs.WorkspaceResourceCounts. */
export interface WorkspaceResourceCounts {
  agents: number
  knowledge: number
  automations: number
  chatbots: number
  connections: number
  total: number
}

export interface WorkspaceDto {
  id: string
  name: string
  description: string | null
  /** Every tenant has exactly one; it receives anything created without a workspace and cannot be deleted. */
  isDefault: boolean
  resources: WorkspaceResourceCounts
  createdAt: string
  modifiedAt: string | null
}

export interface WorkspaceInput {
  name: string
  description: string | null
}
