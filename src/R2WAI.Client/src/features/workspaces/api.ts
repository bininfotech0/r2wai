import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson } from '../../lib/api/fetchJson'
import type { WorkspaceDto, WorkspaceInput } from './types'

export function listWorkspaces() {
  return fetchJson<WorkspaceDto[]>('/workspaces')
}

export function createWorkspace(input: WorkspaceInput) {
  return postJson<WorkspaceDto>('/workspaces', input)
}

export function updateWorkspace(id: string, input: WorkspaceInput) {
  return fetchJson<WorkspaceDto>(`/workspaces/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id, ...input }),
  })
}

export async function deleteWorkspace(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/workspaces/${id}`, { method: 'DELETE' })
  if (response.ok) return
  // The server refuses the default workspace and non-empty ones with a validation problem;
  // re-run through fetchJson's parser so the reason reaches the snackbar.
  let message = 'Failed to delete workspace'
  try {
    const body = (await response.json()) as { errors?: Record<string, string[]>; detail?: string; title?: string }
    message = Object.values(body.errors ?? {}).flat()[0] ?? body.detail ?? body.title ?? message
  } catch {
    // Non-JSON body — keep the generic message.
  }
  throw new ApiRequestError(response.status, message)
}
