// Mirrors R2WAI.Api.Controllers.McpConnectionsController's DTOs (camelCase).
export interface McpConnectionDto {
  id: string
  name: string
  endpointUrl: string
  authHeaderName: string | null
  hasCredential: boolean
  isActive: boolean
  lastTestStatus: 'Connected' | 'Error' | null
  lastTestedAt: string | null
}

export interface McpConnectionInput {
  name: string
  endpointUrl: string
  authHeaderName?: string
  // Blank on an edit of a connection that already has a credential means "leave the stored one
  // alone" — same convention as CreateEditApplicationApiDialog/CreateEditIntegrationDialog.
  credential?: string
}

export interface McpToolCandidate {
  name: string
  description: string | null
}

export interface McpTestResult {
  success: boolean
  message: string
  toolCount?: number
}

export interface McpCommitResult {
  ids: string[]
}
