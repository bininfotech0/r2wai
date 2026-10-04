// Mirrors R2WAI.Application.Features.Admin.DTOs.UserDto (camelCase).
// Status is declared on the DTO but nothing in the backend ever sets it
// (no mutation exists) — always shown as unavailable, not fabricated.
export interface UserDto {
  id: string
  externalId: string
  email: string | null
  mobileNumber: string | null
  hasAadhaar: boolean
  firstName: string
  lastName: string
  fullName: string
  avatarUrl: string | null
  status: string | null
  lastLoginAt: string | null
  createdAt: string
  roles: string[]
}

export interface CreateUserInput {
  externalId: string
  email: string
  firstName: string
  lastName: string
  avatarUrl?: string
  password?: string
}

export interface UpdateUserInput {
  firstName: string
  lastName: string
  avatarUrl?: string
  mobileNumber?: string
}

// Mirrors R2WAI.Application.Features.Admin.DTOs.RoleDto. Roles are real
// per-tenant rows — seed is now just Admin/User/SystemAdmin (the 4 extra
// granular roles — Editor/Contributor/WorkflowManager/UserManager — were
// retired 2026-08-29, folded into Admin, since the nav already treated them
// identically). A tenant can still create its own custom roles via
// CreateRoleCommand; those appear here too, alongside the 3 system roles.
export interface RoleDto {
  id: string
  name: string
  description: string | null
  permissions: string | null
  isSystem: boolean
  createdAt: string
}

export interface CreateRoleInput {
  name: string
  description?: string
  permissions?: string
}

export type UpdateRoleInput = CreateRoleInput

// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.4 #67 — platform-wide, not scoped to the caller's own
// tenant (SystemAdmin only, enforced server-side).
export type TenantStatus = 'Active' | 'Suspended' | 'Trial' | 'Expired'

export interface TenantDto {
  id: string
  name: string
  slug: string
  domain: string | null
  status: TenantStatus
  createdAt: string
}

export interface CreateTenantInput {
  name: string
  slug: string
  domain?: string
}

export interface UpdateTenantInput extends CreateTenantInput {
  status?: TenantStatus
}

// Mirrors R2WAI.Application.Features.Admin.DTOs.ModelConfigDto. This is a
// flat "one model = one config" entity, not a "Model Profile" (no primary/
// fallback model, no routing rules, no usage quota fields exist server-side)
// — the plan's Model Profile framing would be a pure UI reframing with no
// backing data, so this is built as what it actually is.
export type DataClassification = 'Public' | 'Internal' | 'Confidential' | 'Restricted'

export const DATA_CLASSIFICATIONS: DataClassification[] = ['Public', 'Internal', 'Confidential', 'Restricted']

// The exact provider set TestModelConnection (AdminController.cs) knows how
// to reach — no live health-check endpoint exists, this is a static
// reference list, not a monitored "Providers" system.
export const MODEL_PROVIDERS = [
  'ollama', 'openai', 'azureopenai', 'deepseek', 'togetherai', 'fireworksai', 'groq', 'perplexity',
  'xai', 'openrouter', 'sambanova', 'cerebras', 'githubmodels', 'ai21labs', 'mistral', 'novitaai',
  'replicate', 'nvidianim',
] as const

export interface ModelConfigDto {
  id: string
  applicationId: string | null
  departmentId: string | null
  name: string
  provider: string
  modelId: string
  endpoint: string | null
  maxTokens: number | null
  temperature: number | null
  topP: number | null
  isDefault: boolean
  isActive: boolean
  hasApiKey: boolean
  dataClassification: DataClassification
  createdAt: string
}

export interface ModelFormInput {
  name: string
  provider: string
  modelId: string
  apiKey?: string
  endpoint?: string
  maxTokens?: number
  temperature?: number
  topP?: number
  isDefault: boolean
  applicationId?: string
  departmentId?: string
  dataClassification: DataClassification
}

// Mirrors ApiKeysController's anonymous list/detail projection (camelCase).
// Scopes/Roles are comma-joined strings server-side.
export interface ApiKeyDto {
  id: string
  name: string
  keyPrefix: string
  scopes: string | null
  roles: string | null
  isActive: boolean
  expiresAt: string | null
  lastUsedAt: string | null
  createdAt: string
  modifiedAt: string | null
  createdByUserId: string
}

export interface CreateApiKeyInput {
  name: string
  scopes?: string[]
  roles?: string[]
  expiresAt?: string
}

export type UpdateApiKeyInput = CreateApiKeyInput

export interface ApiKeyCreatedResult {
  id: string
  name: string
  key: string
  keyPrefix: string
  scopes: string | null
  roles: string | null
  expiresAt: string | null
  message: string
}

// Mirrors WebhooksController's anonymous list projection. These are
// *inbound* webhooks (external systems POST to EndpointUrl to trigger a
// workflow) — there is no outbound "notify me when X happens" concept.
export interface WebhookDto {
  id: string
  name: string
  endpointUrl: string
  triggerType: string
  workflowId: string | null
  isActive: boolean
  lastCalledAt: string | null
  totalCalls: number
  createdAt: string
}

export interface CreateWebhookInput {
  name: string
  slug?: string
  triggerType: string
  workflowId?: string
  secret?: string
}

export interface UpdateWebhookInput {
  name: string
  triggerType: string
  workflowId?: string
  secret?: string
}

// Users/Roles/Models go through MediatR (shared PagedResult<T>: totalCount).
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// ApiKeys/Webhooks are raw-DbContext controllers with their own anonymous
// paging shape (total, not totalCount) — not the shared PagedResult<T>.
export interface RawPagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
