// Mirrors R2WAI.Application.Features.Chatbots.DTOs.ChatbotDto (camelCase).
export type ChatbotStatus = 'Draft' | 'Active' | 'Paused'

export interface ChatbotDto {
  id: string
  assistantId: string | null
  name: string
  description: string | null
  welcomeMessage: string | null
  suggestedQuestions: string | null
  modelConfigurationId: string | null
  knowledgeBaseId: string | null
  promptTemplate: string | null
  voiceEnabled: boolean
  allowedOrigins: string | null
  status: ChatbotStatus
  createdAt: string
  embedScript: string | null
  widgetSettings: string | null
  widgetLastSeenAt: string | null
  widgetLastSeenOrigin: string | null
  totalMessagesServed: number
  publishedAssistantVersionNumber: number | null
  publishedAt: string | null
}

export interface ChatbotUsageDto {
  totalMessagesServed: number
  tenantDailyCap: number | null
  tenantDailyUsed: number
  positiveFeedbackCount: number
  negativeFeedbackCount: number
}

// Only settable at creation — CreateChatbotCommandHandler is the only caller
// of Chatbot.AssignAssistant/LinkKnowledgeBase/LinkModelConfiguration; there
// is no re-link endpoint, so these are immutable after create.
export interface CreateChatbotInput {
  name: string
  assistantId?: string
  knowledgeBaseId?: string
  voiceEnabled: boolean
}

// UpdateChatbotCommand deliberately excludes assistantId/knowledgeBaseId/
// modelConfigurationId — only these fields can change after creation.
export interface UpdateChatbotInput {
  name: string
  description?: string
  welcomeMessage?: string
  suggestedQuestions?: string
  promptTemplate?: string
  voiceEnabled: boolean
  allowedOrigins?: string
}

// SuggestedQuestions is a `jsonb` column server-side (ChatbotConfiguration.cs)
// but UpdateChatbotCommand accepts it as a raw string with no validation —
// sending anything that isn't valid JSON (including "", which Npgsql writes
// as zero bytes, not the JSON string "\"\"") crashes the update with a 500
// (Postgres 22P02 "invalid input syntax for type json"). The real, intended
// shape is a JSON-encoded array of question strings; these helpers keep the
// UI's plain "one per line" textarea from ever sending an invalid payload.
export function parseSuggestedQuestionsText(json: string | null): string {
  if (!json) return ''
  try {
    const parsed = JSON.parse(json) as unknown
    return Array.isArray(parsed) ? parsed.filter((q): q is string => typeof q === 'string').join('\n') : ''
  } catch {
    return ''
  }
}

export function encodeSuggestedQuestionsText(text: string): string | undefined {
  const lines = text
    .split('\n')
    .map((l) => l.trim())
    .filter(Boolean)
  return lines.length > 0 ? JSON.stringify(lines) : undefined
}

// Same shape/rationale as suggestedQuestions above — AllowedOrigins is also a `jsonb` column
// (a JSON-encoded string[] of exact origins, e.g. "https://acme.example"), and the UI offers a
// plain "one per line" textarea rather than asking an admin to hand-write JSON.
export function parseAllowedOriginsText(json: string | null): string {
  if (!json) return ''
  try {
    const parsed = JSON.parse(json) as unknown
    return Array.isArray(parsed) ? parsed.filter((o): o is string => typeof o === 'string').join('\n') : ''
  } catch {
    return ''
  }
}

export function encodeAllowedOriginsText(text: string): string | undefined {
  const lines = text
    .split('\n')
    .map((l) => l.trim())
    .filter(Boolean)
  return lines.length > 0 ? JSON.stringify(lines) : undefined
}

export function buildFullChatbotUpdatePayload(
  chatbot: ChatbotDto,
  overrides: Partial<UpdateChatbotInput>,
): UpdateChatbotInput {
  return {
    name: chatbot.name,
    description: chatbot.description ?? undefined,
    welcomeMessage: chatbot.welcomeMessage ?? undefined,
    suggestedQuestions: chatbot.suggestedQuestions ?? undefined,
    promptTemplate: chatbot.promptTemplate ?? undefined,
    voiceEnabled: chatbot.voiceEnabled,
    allowedOrigins: chatbot.allowedOrigins ?? undefined,
    ...overrides,
  }
}

export type ChatbotChannelType = 'Teams' | 'Slack' | 'WhatsApp' | 'Sms'

export const CHATBOT_CHANNEL_TYPES: ChatbotChannelType[] = ['Teams', 'Slack', 'WhatsApp', 'Sms']

export interface ChatbotChannelDto {
  channel: ChatbotChannelType
  configuration: string | null
  connectedAt: string
}

export interface WebhookKeyInfoDto {
  keyPrefix: string | null
  hasKey: boolean
  webhookUrl: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// The two [AllowAnonymous] endpoints an embedded widget calls — no token,
// no API key, just the chatbot's id in the URL. See ChatbotEmbedDialog.
export interface ChatbotPublicInfo {
  name: string
  welcomeMessage: string | null
  voiceEnabled: boolean
}
