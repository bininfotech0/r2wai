/**
 * Onboarding checklist derivation.
 *
 * Extracted from the component so the "never claim progress that has not been made" rule
 * is testable. The rule matters: this is the first thing a new tenant sees, and a
 * checklist that ticks itself off optimisticly destroys the only signal telling someone
 * what to do next. So every predicate below reads a *measured* quantity, and the whole
 * checklist is suppressed unless every one of its inputs actually loaded.
 */

/** The live tenant state each checklist item is derived from. All fields are measured. */
export interface OnboardingSignals {
  totalAssistants: number
  /** Both knowledge counts come from /operations/metrics — no second list request needed. */
  totalKnowledgeBases: number
  totalDocuments: number
  integrationCount: number
  /** True when at least one chatbot has been published Active to some channel. */
  hasPublishedChatbot: boolean
  /** Completed chat turns the tenant has generated — the playground was genuinely used. */
  conversationSamples: number
}

export interface OnboardingItem {
  key: string
  label: string
  description: string
  actionLabel: string
  path: string
  isComplete: boolean
}

export const ONBOARDING_ITEMS: readonly Omit<OnboardingItem, 'isComplete'>[] = [
  {
    key: 'assistant',
    label: 'Create your first AI assistant',
    description: 'An assistant is the agent itself — it holds the model, prompt and behaviour.',
    actionLabel: 'Create assistant',
    path: '/assistants',
  },
  {
    key: 'knowledge',
    label: 'Add knowledge to ground it',
    description: 'Upload documents or point it at a website so answers come from your content.',
    actionLabel: 'Add knowledge',
    path: '/knowledge',
  },
  {
    key: 'tools',
    label: 'Connect a tool or integration',
    description: 'Let the assistant act — read from a REST API, query a database, or run a workflow.',
    actionLabel: 'Browse integrations',
    path: '/integrations',
  },
  {
    key: 'test',
    label: 'Test it in the playground',
    description: 'Try real questions against your knowledge and tools before anyone else sees it.',
    actionLabel: 'Open playground',
    path: '/playground',
  },
  {
    key: 'publish',
    label: 'Publish to a channel',
    description: 'Embed it on your website as a chat widget, then copy one script tag.',
    actionLabel: 'Publish',
    path: '/deploy',
  },
]

/**
 * Knowledge counts from the metrics rollup, which reports both the knowledge-base and
 * document totals. An earlier version of this also fetched `/knowledgebases` as a fallback
 * "in case they disagree" — that pulled up to 100 full records onto the dashboard purely to
 * run `.length > 0`, on every load, for no information the metrics endpoint didn't already
 * carry. A momentary disagreement between two reads resolves itself and must not un-tick a
 * completed step.
 */
function knowledgeDone(s: OnboardingSignals): boolean {
  return s.totalKnowledgeBases > 0 || s.totalDocuments > 0
}

export function buildOnboardingItems(signals: OnboardingSignals): OnboardingItem[] {
  return ONBOARDING_ITEMS.map((item) => ({
    ...item,
    isComplete:
      item.key === 'assistant'
        ? signals.totalAssistants > 0
        : item.key === 'knowledge'
          ? knowledgeDone(signals)
          : item.key === 'tools'
            ? signals.integrationCount > 0
            : item.key === 'test'
              ? signals.conversationSamples > 0
              : signals.hasPublishedChatbot,
  }))
}

/**
 * The checklist hides itself once every step is done, so a mature tenant never carries a
 * permanently-finished widget. Returns null when there is nothing worth showing.
 */
export function resolveOnboarding(signals: OnboardingSignals): {
  items: OnboardingItem[]
  done: number
  total: number
} | null {
  const items = buildOnboardingItems(signals)
  const done = items.filter((i) => i.isComplete).length
  if (done === items.length) return null
  return { items, done, total: items.length }
}
