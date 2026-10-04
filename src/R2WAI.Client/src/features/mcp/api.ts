import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type { McpCommitResult, McpConnectionDto, McpConnectionInput, McpTestResult, McpToolCandidate } from './types'

const postJson = sharedPostJson

export function listMcpConnections() {
  return fetchJson<McpConnectionDto[]>('/mcp-connections')
}

export function getMcpConnection(id: string) {
  return fetchJson<McpConnectionDto>(`/mcp-connections/${id}`)
}

export function createMcpConnection(input: McpConnectionInput) {
  return postJson<McpConnectionDto>('/mcp-connections', input)
}

export async function updateMcpConnection(id: string, input: McpConnectionInput): Promise<McpConnectionDto> {
  const response = await authFetch(`/api/v1/mcp-connections/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update MCP server')
  return (await response.json()) as McpConnectionDto
}

export async function deleteMcpConnection(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/mcp-connections/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete MCP server')
}

export function toggleMcpConnection(id: string) {
  return postJson<{ id: string; isActive: boolean }>(`/mcp-connections/${id}/toggle`, {})
}

export function testMcpConnection(id: string) {
  return postJson<McpTestResult>(`/mcp-connections/${id}/test`, {})
}

export function discoverMcpTools(id: string) {
  return fetchJson<McpToolCandidate[]>(`/mcp-connections/${id}/discover`)
}

export function commitMcpTools(id: string, tools: McpToolCandidate[]) {
  return postJson<McpCommitResult>(`/mcp-connections/${id}/commit`, { tools })
}
