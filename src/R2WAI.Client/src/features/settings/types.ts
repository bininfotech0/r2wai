// Mirrors R2WAI.Application.Features.Admin.DTOs.SettingsDto (camelCase).
// TenantSettings/Features are opaque JSON blobs the backend validates only
// for well-formedness, not structure — see api.ts for the merge-safe update
// helper this requires (confirmed real: SaveContentModeration in
// AdminController.cs already writes a "contentModeration" key into this same
// TenantSettings string; a naive overwrite here would silently destroy it).
export interface SettingsDto {
  tenantId: string
  tenantName: string
  tenantSlug: string
  tenantDomain: string | null
  tenantSettings: string | null
  features: string | null
  aiModels: string | null
}

export interface UpdateSettingsInput {
  tenantName?: string
  tenantSlug?: string
  tenantDomain?: string
  tenantSettings?: string
  features?: string
}

// The "limits" key this feature owns inside the TenantSettings blob.
export interface TenantLimits {
  maxUsers: number
  maxAssistants: number
  maxWorkflows: number
  maxDocuments: number
  maxStorageMb: number
}

export const DEFAULT_LIMITS: TenantLimits = {
  maxUsers: 50,
  maxAssistants: 10,
  maxWorkflows: 50,
  maxDocuments: 1000,
  maxStorageMb: 5000,
}

// The Features blob's shape.
export interface TenantFeatureFlags {
  assistants: boolean
  workflows: boolean
  approvals: boolean
  knowledge: boolean
  integrations: boolean
  analytics: boolean
}

export const DEFAULT_FEATURE_FLAGS: TenantFeatureFlags = {
  assistants: true,
  workflows: true,
  approvals: true,
  knowledge: true,
  integrations: true,
  analytics: true,
}
