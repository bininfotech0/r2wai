// Mirrors R2WAI.Domain.Enums.ToolType exactly — no Rest/OpenApi/Sftp members
// exist server-side; "REST" in the UI maps to Http.
export type ToolType = 'Http' | 'Email' | 'Database' | 'Script' | 'Custom' | 'SemanticKernelFunction'

export const TOOL_TYPES: ToolType[] = ['Http', 'Email', 'Database', 'Script', 'Custom', 'SemanticKernelFunction']

export const TOOL_TYPE_LABELS: Record<ToolType, string> = {
  Http: 'REST / HTTP',
  Email: 'Email',
  Database: 'Database',
  Script: 'Script',
  Custom: 'Custom',
  SemanticKernelFunction: 'Semantic Kernel Function',
}

// Mirrors R2WAI.Application.Features.Integrations.DTOs.IntegrationDto (camelCase).
// Integrations and Tools/Capabilities are the same ToolDefinition table under
// two different DTOs — there is no IntegrationId FK on a Tool.
export interface IntegrationDto {
  id: string
  name: string
  description: string | null
  type: string // ToolType name
  endpointUrl: string | null
  configuration: string | null // opaque JSON, see IntegrationConfiguration below
  isActive: boolean
  // Real, persisted result of the last "Test" call — null means never tested (distinct from Error).
  // Survives a page reload, unlike a session-only test result.
  lastTestStatus: 'Connected' | 'Error' | null
  lastTestedAt: string | null
  createdAt: string
}

export type AuthType = 'None' | 'Bearer' | 'ApiKey' | 'Basic' | 'OAuth2'

// The real, authoritative shape DynamicToolExecutor.ResolveDirectEndpoint
// reads back out of IntegrationDto.configuration.
export interface IntegrationConfiguration {
  AuthType: AuthType
  Token?: string
  ApiKey?: string
  ApiKeyHeaderName?: string
  Username?: string
  Password?: string
}

export interface CreateIntegrationInput {
  name: string
  type: ToolType
  description?: string
  endpointUrl?: string
  configuration?: string
}

export type UpdateIntegrationInput = CreateIntegrationInput

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

// Curated suggestions only — "Connect" pre-fills the same real create form every hand-built
// integration uses. Nothing here is a live connection until the admin enters credentials and
// Test succeeds, same as creating one from scratch.
export interface IntegrationCatalogEntryDto {
  id: string
  name: string
  category: string
  description: string
  suggestedType: ToolType
  suggestedAuthType: AuthType
  suggestedEndpointUrl: string | null
}

// Mirrors R2WAI.Application.Common.Interfaces.OpenApiOperationCandidate /
// OpenApiAnalyzeResult (Track B Phase 3a — backend now live).
export interface OpenApiOperationCandidate {
  method: string
  path: string
  suggestedName: string
  summary?: string
}

export interface OpenApiAnalyzeResult {
  baseUrl: string
  operations: OpenApiOperationCandidate[]
}

export interface OpenApiCommitResult {
  ids: string[]
}
