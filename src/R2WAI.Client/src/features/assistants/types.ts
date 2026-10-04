// Mirrors R2WAI.Application.Features.Assistants.DTOs.AssistantDto (camelCase).
export type AssistantType =
  | 'General'
  | 'HR'
  | 'IT'
  | 'Procurement'
  | 'Finance'
  | 'Legal'
  | 'CoachingCenter'
  | 'WordPress'
  | 'Strapi'
  | 'Joomla'
  | 'Drupal'
  | 'Shopify'
  | 'Salesforce'
  | 'SAP'

// The 6 types AI-first generation (/assistants/generate-config) can produce.
export const GENERATABLE_TYPES: AssistantType[] = ['General', 'HR', 'IT', 'Finance', 'Procurement', 'Legal']

export interface AssistantDto {
  id: string
  applicationId: string | null
  name: string
  description: string | null
  type: AssistantType
  systemPrompt: string | null
  modelConfigurationId: string | null
  knowledgeBaseId: string | null
  tools: string | null // JSON-encoded string[] of enabled capability ids
  settings: string | null // JSON-encoded behavior settings, see BehaviorSettings below
  isActive: boolean
  publishStatus: 'Draft' | 'Published' | 'Archived'
  publishedVersion: number
  publishedAt: string | null
  tags: string | null
  avatarUrl: string | null
  usageCount: number
  createdAt: string
}

export interface BehaviorSettings {
  responseStyle?: 'Professional' | 'Friendly' | 'Concise' | 'Detailed'
  answerLength?: 'Brief' | 'Balanced' | 'Thorough'
  citationsEnabled?: boolean
  askClarification?: boolean
  temperature?: number
  maxOutputTokens?: number
  // R2WAI 2.0 §6 — only meaningful when a Knowledge Base is linked. Undefined/'Standard' keeps
  // today's single-pass retrieval; 'Agentic' adds a bounded evidence-check + one rewritten retry.
  retrievalMode?: 'Standard' | 'Agentic'
}

export interface CreateAssistantInput {
  name: string
  type: AssistantType
  modelConfigurationId?: string
  knowledgeBaseId?: string
}

export interface UpdateAssistantInput {
  name: string
  description?: string
  type?: AssistantType
  systemPrompt?: string
  modelConfigurationId?: string
  // Same omitted-vs-null problem as knowledgeBaseId below — "reset to tenant default" needs its
  // own explicit signal, since sending modelConfigurationId: undefined is indistinguishable from
  // not touching the field at all.
  unlinkModelConfiguration?: boolean
  knowledgeBaseId?: string
  // knowledgeBaseId can only link/switch a KB — the server can't tell "field omitted" from
  // "explicit null" once JSON-bound, so detaching needs its own explicit signal.
  unlinkKnowledgeBase?: boolean
  tools?: string
  settings?: string
  isActive?: boolean
  tags?: string
  avatarUrl?: string
}

/**
 * The API's PUT /assistants/{id} does a blind overwrite of description/
 * systemPrompt/tools/settings (AssistantDefinition.UpdateDetails has no
 * null-coalescing against the existing entity) — omitting a field from the
 * payload clears it, it does not preserve it. Every update call must send
 * the full current field set with just the intended change layered on top;
 * this builds that payload from the currently-loaded AssistantDto so no
 * call site can accidentally wipe fields it didn't mean to touch.
 */
export function buildFullUpdatePayload(
  assistant: AssistantDto,
  overrides: Partial<UpdateAssistantInput>,
): UpdateAssistantInput {
  return {
    name: assistant.name,
    description: assistant.description ?? undefined,
    type: assistant.type,
    systemPrompt: assistant.systemPrompt ?? undefined,
    knowledgeBaseId: assistant.knowledgeBaseId ?? undefined,
    tools: assistant.tools ?? undefined,
    settings: assistant.settings ?? undefined,
    tags: assistant.tags ?? undefined,
    avatarUrl: assistant.avatarUrl ?? undefined,
    ...overrides,
  }
}

export interface GeneratedAssistantConfig {
  name: string
  type: AssistantType
  description: string
  systemPrompt: string
  isAiGenerated: boolean
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

/** GET /assistants' response — PagedResult<AssistantDto> plus per-status counts scoped to the
 * same search/applicationId filters as items (not narrowed by publishStatus), so switching the
 * status tab never has to guess at the other tabs' counts. */
export interface AssistantsPagedResult extends PagedResult<AssistantDto> {
  statusCounts: Record<string, number>
}
