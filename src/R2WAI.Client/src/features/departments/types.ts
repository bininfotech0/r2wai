// Mirrors R2WAI.Application.Features.Applications.DTOs.DepartmentDto.
export interface DepartmentDto {
  id: string
  name: string
  code: string
  description: string | null
  headUserId: string | null
  isActive: boolean
  createdAt: string
  modifiedAt: string | null
}

export interface CreateDepartmentInput {
  name: string
  code: string
  description?: string
}

export interface UpdateDepartmentInput {
  name: string
  description?: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
