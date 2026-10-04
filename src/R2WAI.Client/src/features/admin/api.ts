import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  ApiKeyCreatedResult,
  ApiKeyDto,
  CreateApiKeyInput,
  CreateRoleInput,
  CreateTenantInput,
  CreateUserInput,
  CreateWebhookInput,
  ModelConfigDto,
  ModelFormInput,
  PagedResult,
  RawPagedResult,
  RoleDto,
  TenantDto,
  UpdateApiKeyInput,
  UpdateRoleInput,
  UpdateTenantInput,
  UpdateUserInput,
  UpdateWebhookInput,
  UserDto,
  WebhookDto,
} from './types'

const postJson = sharedPostJson

async function putJson<T>(path: string, body: unknown): Promise<T> {
  const response = await authFetch(`/api/v1${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) throw new ApiRequestError(response.status, `PUT ${path} failed`)
  return (await response.json()) as T
}

// --- Users ---
export function listUsers(page: number, pageSize: number, search: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<UserDto>>(`/admin/users?${params.toString()}`)
}
export function createUser(input: CreateUserInput) {
  return postJson<UserDto>('/admin/users', input)
}
export function updateUser(id: string, input: UpdateUserInput) {
  return putJson<UserDto>(`/admin/users/${id}`, input)
}
export function assignUserRoles(id: string, roleIds: string[]) {
  return putJson<UserDto>(`/admin/users/${id}/roles`, { roleIds })
}
export async function deleteUser(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/admin/users/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete user')
}
export function inviteUser(email: string) {
  return postJson<{ message: string }>('/admin/users/invite', { email })
}
export function resetUserMfa(id: string) {
  return postJson<{ id: string; mfaEnabled: boolean; message: string }>(`/admin/users/${id}/mfa-reset`, {})
}
export function unlockUser(id: string) {
  return postJson<{ id: string; message: string }>(`/admin/users/${id}/unlock`, {})
}

// --- Roles ---
export function listRoles() {
  return listRolesPage(1, 100)
}
export function listRolesPage(page = 1, pageSize = 20) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<PagedResult<RoleDto>>(`/admin/roles?${params.toString()}`)
}
export function createRole(input: CreateRoleInput) {
  return postJson<RoleDto>('/admin/roles', input)
}
export function updateRole(id: string, input: UpdateRoleInput) {
  return putJson<RoleDto>(`/admin/roles/${id}`, input)
}
export async function deleteRole(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/admin/roles/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete role')
}

// --- Models ---
export function listModels() {
  return listModelsPage(1, 50, '')
}

export function listModelsPage(page = 1, pageSize = 20, search = '') {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<ModelConfigDto>>(`/admin/models?${params.toString()}`)
}
export function createModel(input: ModelFormInput) {
  return postJson<ModelConfigDto>('/admin/models', input)
}
export function updateModel(id: string, input: ModelFormInput) {
  return putJson<ModelConfigDto>(`/admin/models/${id}`, { id, ...input })
}
export async function deleteModel(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/admin/models/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete model')
}
export function testModelConnection(id: string) {
  return postJson<{ success: boolean; message: string }>(`/admin/models/${id}/test`, {})
}

// --- API Keys ---
export function listApiKeys(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<RawPagedResult<ApiKeyDto>>(`/admin/api-keys?${params.toString()}`)
}
export function createApiKey(input: CreateApiKeyInput) {
  return postJson<ApiKeyCreatedResult>('/admin/api-keys', input)
}
export function updateApiKey(id: string, input: UpdateApiKeyInput) {
  return putJson<ApiKeyDto>(`/admin/api-keys/${id}`, input)
}
export function toggleApiKey(id: string) {
  return postJson<{ id: string; isActive: boolean }>(`/admin/api-keys/${id}/toggle`, {})
}
export function regenerateApiKey(id: string) {
  return postJson<ApiKeyCreatedResult>(`/admin/api-keys/${id}/regenerate`, {})
}
export async function deleteApiKey(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/admin/api-keys/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete API key')
}

// --- Webhooks ---
export function listWebhooks(page: number, pageSize: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<RawPagedResult<WebhookDto>>(`/admin/webhooks?${params.toString()}`)
}
export function createWebhook(input: CreateWebhookInput) {
  return postJson<{ id: string; name: string; endpointUrl: string }>('/admin/webhooks', input)
}
export function updateWebhook(id: string, input: UpdateWebhookInput) {
  return putJson<{ id: string; name: string }>(`/admin/webhooks/${id}`, input)
}
export function toggleWebhook(id: string) {
  return postJson<{ id: string; isActive: boolean }>(`/admin/webhooks/${id}/toggle`, {})
}
export async function deleteWebhook(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/admin/webhooks/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete webhook')
}

// --- Tenants (platform-wide, SystemAdmin only) ---
export function listTenants(page: number, pageSize: number, search: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<TenantDto>>(`/admin/tenants?${params.toString()}`)
}
export function createTenant(input: CreateTenantInput) {
  return postJson<TenantDto>('/admin/tenants', input)
}
export function updateTenant(id: string, input: UpdateTenantInput) {
  return putJson<TenantDto>(`/admin/tenants/${id}`, input)
}
export async function deleteTenant(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/admin/tenants/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete tenant')
}
