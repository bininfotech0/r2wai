import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson, postJson as sharedPostJson } from '../../lib/api/fetchJson'
import type {
  ChatbotChannelDto,
  ChatbotChannelType,
  ChatbotDto,
  ChatbotStatus,
  ChatbotUsageDto,
  CreateChatbotInput,
  PagedResult,
  UpdateChatbotInput,
  WebhookKeyInfoDto,
} from './types'

const postJson = sharedPostJson

export function listChatbots(page: number, pageSize: number, assistantId?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (assistantId) params.set('assistantId', assistantId)
  return fetchJson<PagedResult<ChatbotDto>>(`/chatbots?${params.toString()}`)
}

export function getChatbot(id: string) {
  return fetchJson<ChatbotDto>(`/chatbots/${id}`)
}

export function createChatbot(input: CreateChatbotInput) {
  return postJson<ChatbotDto>('/chatbots', input)
}

export async function updateChatbot(id: string, input: UpdateChatbotInput): Promise<ChatbotDto> {
  const response = await authFetch(`/api/v1/chatbots/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ id, ...input }),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to update chatbot')
  return (await response.json()) as ChatbotDto
}

export function setChatbotStatus(id: string, status: ChatbotStatus) {
  return postJson<ChatbotDto>(`/chatbots/${id}/status`, { status })
}

export async function updateChatbotWidget(id: string, embedScript: string, widgetSettings: string): Promise<ChatbotDto> {
  const response = await authFetch(`/api/v1/chatbots/${id}/widget`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ embedScript, widgetSettings }),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to save widget settings')
  return (await response.json()) as ChatbotDto
}

export async function deleteChatbot(id: string): Promise<void> {
  const response = await authFetch(`/api/v1/chatbots/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to delete chatbot')
}

export function listChatbotChannels(id: string) {
  return fetchJson<ChatbotChannelDto[]>(`/chatbots/${id}/channels`)
}

export async function addChatbotChannel(id: string, channel: ChatbotChannelType, configuration: string): Promise<void> {
  // Unlike the other POST endpoints in this file, this route responds 200
  // with an empty body — postJson's unconditional response.json() would
  // throw on that and silently fail the mutation, so this stays a plain
  // authFetch that only checks response.ok.
  const response = await authFetch(`/api/v1/chatbots/${id}/channels/${channel}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(JSON.parse(configuration || '{}')),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to connect channel')
}

export async function removeChatbotChannel(id: string, channel: ChatbotChannelType): Promise<void> {
  const response = await authFetch(`/api/v1/chatbots/${id}/channels/${channel}`, { method: 'DELETE' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to remove channel')
}

export function getWebhookKeyInfo(id: string) {
  return fetchJson<WebhookKeyInfoDto>(`/chatbots/${id}/webhook-key`)
}

export function getChatbotUsage(id: string) {
  return fetchJson<ChatbotUsageDto>(`/chatbots/${id}/usage`)
}

export function regenerateWebhookKey(id: string) {
  return postJson<{ key: string; keyPrefix: string; message: string }>(`/chatbots/${id}/webhook-key/regenerate`, {})
}
