// Mirrors R2WAI.Application.Common.Interfaces' user-memory DTOs (camelCase, enums as strings).

export type MemoryCategory = 'Profile' | 'Preference' | 'Instruction' | 'Fact' | 'Goal'
export type MemorySource = 'Manual' | 'Learned' | 'Agent'
export type MemoryStatus = 'Active' | 'PendingReview' | 'Paused'

export interface UserMemoryDto {
  id: string
  /** null = the Personal partition every agent can read; set = that agent's own notes. */
  assistantId: string | null
  category: MemoryCategory
  key: string | null
  content: string
  source: MemorySource
  status: MemoryStatus
  isPinned: boolean
  sourceConversationId: string | null
  createdAt: string
  modifiedAt: string | null
  lastUsedAt: string | null
}

export interface MemorySettings {
  memoryEnabled: boolean
  autoLearnEnabled: boolean
  reviewLearned: boolean
  agentNotesEnabled: boolean
}

export interface MemoryPartition {
  assistantId: string | null
  assistantName: string | null
  count: number
  pendingCount: number
}

export interface MemoryOverview {
  settings: MemorySettings
  partitions: MemoryPartition[]
  items: UserMemoryDto[]
  limit: number
}

export interface SaveMemoryInput {
  category: MemoryCategory
  key: string | null
  content: string
  isPinned: boolean
  paused: boolean
}

export const MEMORY_CONTENT_MAX = 500

export const CATEGORIES: { value: MemoryCategory; label: string; hint: string }[] = [
  { value: 'Profile', label: 'About me', hint: 'Who you are: your role, team or department.' },
  { value: 'Preference', label: 'Preference', hint: 'How you like answers: language, tone, format.' },
  { value: 'Instruction', label: 'Standing request', hint: 'Something agents should always do for you.' },
  { value: 'Fact', label: 'Fact', hint: 'Something stable about your work agents should know.' },
  { value: 'Goal', label: 'Goal', hint: 'What you are working towards.' },
]

export function categoryLabel(category: MemoryCategory): string {
  return CATEGORIES.find((c) => c.value === category)?.label ?? category
}

export const SOURCE_LABELS: Record<MemorySource, string> = {
  Manual: 'Added by you',
  Learned: 'Learned from a chat',
  Agent: 'Saved by the agent',
}

/** The partition id the Clear endpoint takes: 'personal' or an agent's id. */
export function partitionKey(assistantId: string | null): string {
  return assistantId ?? 'personal'
}
