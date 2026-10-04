// Mirrors R2WAI.Application.Features.Applications.DTOs.ApplicationDto (camelCase).
// Backed by the ConnectedApplication entity (named that, not "Application",
// because the R2WAI.Application project namespace shadows the plain name).
// UI-facing label is "Connected System" (not "Application") per product decision — same
// convention as "Capability"/"Tool": the backend name is unchanged to avoid a large,
// risky rename across the entity/DTO/controller/route layer for a purely cosmetic ask.
// Briefly labeled "Workspace" earlier in the same product effort, then renamed again once
// "Workspace" was redefined as the bigger Assistant/Capability/Knowledge container concept —
// this entity was never that; it's the integration plumbing underneath it, correctly demoted
// out of primary nav into Manage (see roleNav.ts) and relabeled to stop implying otherwise.
export type ApplicationEnvironment = 'Development' | 'Staging' | 'Production'
export type ApplicationStatus = 'Draft' | 'Discovering' | 'Configuring' | 'Testing' | 'Published' | 'Disabled' | 'Archived'
export type ApplicationAction = 'StartDiscovery' | 'MarkConfiguring' | 'MarkTesting' | 'Publish' | 'Disable' | 'Enable' | 'Archive'

export const APPLICATION_ENVIRONMENTS: ApplicationEnvironment[] = ['Development', 'Staging', 'Production']

// StartDiscovery is deliberately excluded here — it's no longer a bare status flip, it's driven by
// the "Discover from OpenAPI Spec" action (see discoverApplication), which transitions
// Discovering → Configuring itself once parsing completes.
export const APPLICATION_ACTIONS: ApplicationAction[] = [
  'MarkConfiguring',
  'MarkTesting',
  'Publish',
  'Disable',
  'Enable',
  'Archive',
]

export interface ApplicationDto {
  id: string
  departmentId: string | null
  name: string
  code: string
  description: string | null
  baseUrl: string | null
  environment: ApplicationEnvironment
  status: ApplicationStatus
  managerUserId: string | null
  publishedAt: string | null
  createdAt: string
  modifiedAt: string | null
}

export interface CreateApplicationInput {
  // Optional organisational classification — omit to create a connected system that belongs to
  // no department. The backend still rejects a supplied id outside the caller's tenant.
  departmentId?: string
  name: string
  code: string
  description?: string
  baseUrl?: string
  environment: ApplicationEnvironment
}

export interface UpdateApplicationInput {
  name: string
  description?: string
  baseUrl?: string
  environment: ApplicationEnvironment
}

export type ApiAuthScheme = 'None' | 'ApiKey' | 'OAuth2' | 'Jwt' | 'EntraId'

export const API_AUTH_SCHEMES: ApiAuthScheme[] = ['None', 'ApiKey', 'OAuth2', 'Jwt', 'EntraId']

export interface ApplicationApiDto {
  id: string
  applicationId: string
  name: string
  baseUrl: string
  authScheme: ApiAuthScheme
  credentialRef: string | null
  // Never the decrypted secret — just whether one is stored, so the edit dialog can show
  // "configured" without round-tripping the credential (Track B Phase 3b).
  hasCredential: boolean
  credentialHeaderName: string | null
  openApiSource: string | null
  isActive: boolean
}

export interface CreateApplicationApiInput {
  name: string
  baseUrl: string
  authScheme: ApiAuthScheme
  credentialRef?: string
  // Plaintext on the wire (HTTPS + auth) — encrypted server-side before storage, never returned.
  // Leave undefined on edit to keep the existing stored secret unchanged.
  credentialSecret?: string
  credentialHeaderName?: string
  openApiSource?: string
  isActive: boolean
}

export type UpdateApplicationApiInput = CreateApplicationApiInput

export interface ApplicationConfigurationDto {
  timeoutSeconds: number
  maxRetries: number
  ragThreshold: number
  modelId: string | null
  systemPromptTemplate: string | null
}

export interface ApplicationVersionDto {
  id: string
  versionNumber: number
  configSnapshot: string
  isPublished: boolean
  note: string | null
  publishedByUserId: string | null
  publishedAt: string | null
  createdAt: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ApplicationDiscoveryResultDto {
  applicationApiId: string
  baseUrl: string
  operationsDiscovered: number
  capabilitiesCreated: number
  capabilitiesSkippedAsExisting: number
}
