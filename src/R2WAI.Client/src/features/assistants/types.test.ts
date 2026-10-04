import { describe, it, expect } from 'vitest'
import { buildFullUpdatePayload } from './types'
import type { AssistantDto } from './types'

const baseAssistant: AssistantDto = {
  id: 'a1',
  applicationId: null,
  name: 'HR Assistant',
  description: 'Helps with HR questions',
  type: 'HR',
  systemPrompt: 'You are an HR assistant.',
  modelConfigurationId: null,
  knowledgeBaseId: 'kb1',
  tools: '["cap1","cap2"]',
  settings: '{"responseStyle":"Friendly"}',
  isActive: true,
  publishStatus: 'Published',
  publishedVersion: 1,
  publishedAt: '2026-01-01T00:00:00Z',
  tags: 'hr,onboarding',
  avatarUrl: null,
  usageCount: 5,
  createdAt: '2026-01-01T00:00:00Z',
}

describe('buildFullUpdatePayload', () => {
  // The API's PUT /assistants/{id} blindly overwrites description/systemPrompt/
  // tools/settings (no null-coalescing against the existing entity server-side)
  // — a payload that omits a field clears it. This is the regression these
  // tests guard against.
  it('carries every existing field forward when only one field is overridden', () => {
    const payload = buildFullUpdatePayload(baseAssistant, { tools: '["cap1","cap2","cap3"]' })

    expect(payload.name).toBe('HR Assistant')
    expect(payload.description).toBe('Helps with HR questions')
    expect(payload.systemPrompt).toBe('You are an HR assistant.')
    expect(payload.settings).toBe('{"responseStyle":"Friendly"}')
    expect(payload.knowledgeBaseId).toBe('kb1')
    expect(payload.tags).toBe('hr,onboarding')
    // ...with only the intended field actually changed
    expect(payload.tools).toBe('["cap1","cap2","cap3"]')
  })

  it('lets an override replace a field entirely', () => {
    const payload = buildFullUpdatePayload(baseAssistant, { name: 'Renamed Assistant' })
    expect(payload.name).toBe('Renamed Assistant')
    expect(payload.description).toBe(baseAssistant.description)
  })

  it('converts null fields to undefined rather than sending literal nulls', () => {
    const withNulls: AssistantDto = { ...baseAssistant, description: null, tools: null, settings: null }
    const payload = buildFullUpdatePayload(withNulls, {})
    expect(payload.description).toBeUndefined()
    expect(payload.tools).toBeUndefined()
    expect(payload.settings).toBeUndefined()
  })
})
