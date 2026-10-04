import { describe, it, expect } from 'vitest'
import {
  buildOnboardingItems,
  resolveOnboarding,
  type OnboardingSignals,
} from './onboarding'

const NOTHING_DONE: OnboardingSignals = {
  totalAssistants: 0,
  totalKnowledgeBases: 0,
  totalDocuments: 0,
  integrationCount: 0,
  hasPublishedChatbot: false,
  conversationSamples: 0,
}

function done(key: string): boolean {
  return buildOnboardingItems(NOTHING_DONE).find((i) => i.key === key)?.isComplete ?? false
}

describe('buildOnboardingItems', () => {
  it('reports nothing complete for a genuinely empty workspace', () => {
    expect(buildOnboardingItems(NOTHING_DONE).every((i) => !i.isComplete)).toBe(true)
  })

  it('treats a single assistant as completing the assistant step', () => {
    expect(done('assistant')).toBe(false)
    expect(buildOnboardingItems({ ...NOTHING_DONE, totalAssistants: 1 })[0].isComplete).toBe(true)
  })

  it('does not count a negative or absent count as progress', () => {
    // Defensive: a metrics endpoint that ever returned a signed/garbage value must not be
    // able to tick a box.
    expect(done('assistant')).toBe(false)
    expect(buildOnboardingItems({ ...NOTHING_DONE, integrationCount: -3 })[2].isComplete).toBe(false)
    expect(buildOnboardingItems({ ...NOTHING_DONE, conversationSamples: -1 })[3].isComplete).toBe(false)
  })

  it('accepts knowledge from either knowledge-base or document counts', () => {
    // Both come from /operations/metrics, so either alone is real measured evidence.
    expect(buildOnboardingItems({ ...NOTHING_DONE, totalKnowledgeBases: 2 })[1].isComplete).toBe(true)
    expect(buildOnboardingItems({ ...NOTHING_DONE, totalDocuments: 5 })[1].isComplete).toBe(true)
  })

  it('does not treat publishing as a chatbot merely existing', () => {
    // hasPublishedChatbot is the only publish signal — a Draft chatbot is not a publish.
    expect(done('publish')).toBe(false)
    expect(buildOnboardingItems({ ...NOTHING_DONE, hasPublishedChatbot: true })[4].isComplete).toBe(true)
  })

  it('keeps every item pointing at a real route', () => {
    const paths = buildOnboardingItems(NOTHING_DONE).map((i) => i.path)
    expect(paths).toEqual(['/assistants', '/knowledge', '/integrations', '/playground', '/deploy'])
  })
})

describe('resolveOnboarding', () => {
  it('returns null and suppresses the widget once every step is complete', () => {
    const all: OnboardingSignals = {
      totalAssistants: 3,
      totalKnowledgeBases: 1,
      totalDocuments: 4,
      integrationCount: 2,
      hasPublishedChatbot: true,
      conversationSamples: 40,
    }
    expect(resolveOnboarding(all)).toBeNull()
  })

  it('still renders when exactly one step is outstanding', () => {
    // Everything done except an integration — the one item left, so the widget must remain
    // visible rather than disappearing as it does for a fully-provisioned tenant.
    const almost: OnboardingSignals = {
      totalAssistants: 1,
      totalKnowledgeBases: 1,
      totalDocuments: 1,
      integrationCount: 0,
      hasPublishedChatbot: true,
      conversationSamples: 10,
    }
    const resolved = resolveOnboarding(almost)
    expect(resolved).not.toBeNull()
    expect(resolved?.done).toBe(4)
    expect(resolved?.total).toBe(5)
    expect(resolved?.items.filter((i) => !i.isComplete).map((i) => i.key)).toEqual(['tools'])
  })

  it('reports zero progress for a new tenant rather than hiding itself', () => {
    const resolved = resolveOnboarding(NOTHING_DONE)
    expect(resolved?.done).toBe(0)
    expect(resolved?.total).toBe(5)
  })
})
