/** Stable assistant-related cache keys shared by queries and invalidations. */
export const queryKeys = {
  assistants: {
    all: ['assistants'] as const,
    list: (page: number, pageSize: number, search: string, status: string, sort: string) =>
      ['assistants', page, pageSize, search, status, sort] as const,
    detail: (id: string | undefined) => ['assistants', id] as const,
    publishReadiness: (id: string | undefined) => ['assistants', id, 'publish-readiness'] as const,
    dashboardBreakdown: ['assistants', 'dashboard-breakdown'] as const,
    playgroundList: ['assistants', 'playground-list'] as const,
    forResources: ['assistants-for-resources'] as const,
    forUsageCount: ['assistants-for-usage-count'] as const,
    forKnowledgeBaseUsage: (knowledgeBaseId: string | undefined) => ['assistants-for-kb-usage', knowledgeBaseId] as const,
    forChatbot: ['assistants-for-chatbot'] as const,
    forTestCase: ['assistants-for-testcase'] as const,
    chatbots: (assistantId: string | undefined) => ['chatbots-for-assistant', assistantId] as const,
  },
}
