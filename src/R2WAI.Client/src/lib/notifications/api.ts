import { fetchJson } from '../api/fetchJson'
import { authFetch } from '../auth/authClient'

export interface NotificationRecord {
  id: string
  title: string
  message: string
  type: string
  link: string | null
  isRead: boolean
  createdAt: string
}

export interface NotificationListResult {
  items: NotificationRecord[]
  totalCount: number
  page: number
  pageSize: number
  unreadCount: number
}

export function listNotifications(page = 1, pageSize = 30) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return fetchJson<NotificationListResult>(`/notifications?${params.toString()}`)
}

export async function markNotificationRead(id: string): Promise<void> {
  await authFetch(`/api/v1/notifications/${id}/read`, { method: 'POST' })
}

export async function markAllNotificationsRead(): Promise<void> {
  await authFetch('/api/v1/notifications/read-all', { method: 'POST' })
}
