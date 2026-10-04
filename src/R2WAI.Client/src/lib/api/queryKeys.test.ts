import { describe, expect, it } from 'vitest'
import { queryKeys } from './queryKeys'

describe('assistant query keys', () => {
  it('preserves current list, detail, and publish-readiness cache identities', () => {
    expect(queryKeys.assistants.all).toEqual(['assistants'])
    expect(queryKeys.assistants.list(2, 25, 'sales', 'Published', 'updatedAt')).toEqual([
      'assistants', 2, 25, 'sales', 'Published', 'updatedAt',
    ])
    expect(queryKeys.assistants.detail('agent-1')).toEqual(['assistants', 'agent-1'])
    expect(queryKeys.assistants.publishReadiness('agent-1')).toEqual([
      'assistants', 'agent-1', 'publish-readiness',
    ])
  })

  it('preserves the existing assistant-adjacent lookup keys', () => {
    expect(queryKeys.assistants.dashboardBreakdown).toEqual(['assistants', 'dashboard-breakdown'])
    expect(queryKeys.assistants.playgroundList).toEqual(['assistants', 'playground-list'])
    expect(queryKeys.assistants.forResources).toEqual(['assistants-for-resources'])
    expect(queryKeys.assistants.forUsageCount).toEqual(['assistants-for-usage-count'])
    expect(queryKeys.assistants.forKnowledgeBaseUsage('kb-1')).toEqual(['assistants-for-kb-usage', 'kb-1'])
    expect(queryKeys.assistants.forChatbot).toEqual(['assistants-for-chatbot'])
    expect(queryKeys.assistants.forTestCase).toEqual(['assistants-for-testcase'])
    expect(queryKeys.assistants.chatbots('agent-1')).toEqual(['chatbots-for-assistant', 'agent-1'])
  })
})
