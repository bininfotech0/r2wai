import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson } from '../../lib/api/fetchJson'
import type { CreateDepartmentInput, DepartmentDto, PagedResult, UpdateDepartmentInput } from './types'

export function listDepartments(page: number, pageSize: number, search: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return fetchJson<PagedResult<DepartmentDto>>(`/departments?${params.toString()}`)
}

export async function createDepartment(input: CreateDepartmentInput): Promise<DepartmentDto> {
  const response = await authFetch('/api/v1/departments', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to create department')
  return (await response.json()) as DepartmentDto
}

export async function updateDepartment(id: string, input: UpdateDepartmentInput): Promise<DepartmentDto> {
  const response = await authFetch(`/api/v1/departments/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update department')
  return (await response.json()) as DepartmentDto
}

export async function deleteDepartment(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/departments/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete department')
}
