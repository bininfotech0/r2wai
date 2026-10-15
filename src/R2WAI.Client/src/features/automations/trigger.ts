/**
 * Client mirror of the server's WorkflowTriggerSpec (R2WAI.Application/Features/Workflows). Only
 * triggers that actually start a run exist: Manual (Run/Test, or an agent's start_workflow tool),
 * Webhook, and Schedule. A schedule is stored in the Trigger field as `schedule:<cron>|<IANA tz>`.
 * The old event labels ("Payment Received", "Form Submitted"…) never fired and parse as unsupported.
 */
export type TriggerKind = 'manual' | 'webhook' | 'schedule' | 'unsupported'

export interface ParsedTrigger {
  kind: TriggerKind
  cron?: string
  timeZone?: string
}

export const SCHEDULE_PREFIX = 'schedule:'

export const SCHEDULE_PRESETS: { label: string; cron: string }[] = [
  { label: 'Every hour', cron: '0 * * * *' },
  { label: 'Every day at 09:00', cron: '0 9 * * *' },
  { label: 'Weekdays at 09:00', cron: '0 9 * * 1-5' },
  { label: 'Every Monday at 09:00', cron: '0 9 * * 1' },
  { label: 'First day of the month at 09:00', cron: '0 9 1 * *' },
]

export function parseTrigger(raw: string | null | undefined): ParsedTrigger {
  const value = raw?.trim() ?? ''
  const lower = value.toLowerCase()
  if (!value || lower === 'manual') return { kind: 'manual' }
  if (lower === 'webhook') return { kind: 'webhook' }
  if (lower === 'schedule') return { kind: 'schedule' }
  if (lower.startsWith(SCHEDULE_PREFIX)) {
    const body = value.slice(SCHEDULE_PREFIX.length)
    const bar = body.lastIndexOf('|')
    const cron = (bar >= 0 ? body.slice(0, bar) : body).trim()
    const timeZone = (bar >= 0 ? body.slice(bar + 1) : '').trim() || 'UTC'
    return { kind: 'schedule', cron: cron || undefined, timeZone }
  }
  return { kind: 'unsupported' }
}

export function formatSchedule(cron: string, timeZone: string): string {
  return `${SCHEDULE_PREFIX}${cron.trim()}|${timeZone.trim()}`
}

/** A quick shape check (5 space-separated fields); the server does the real cron validation. */
export function looksLikeCron(cron: string): boolean {
  return /^\S+(\s+\S+){4}$/.test(cron.trim())
}

/** The stored value if a run can actually be started by it, otherwise undefined (= Manual on save). */
export function toSupportedTrigger(raw: string | null | undefined): string | undefined {
  const parsed = parseTrigger(raw)
  if (parsed.kind === 'unsupported' || (parsed.kind === 'schedule' && !parsed.cron)) return undefined
  return raw?.trim() || undefined
}

export function describeTrigger(raw: string | null | undefined): string {
  const parsed = parseTrigger(raw)
  switch (parsed.kind) {
    case 'manual':
      return 'Manual'
    case 'webhook':
      return 'Webhook'
    case 'schedule': {
      if (!parsed.cron) return 'Schedule (not set)'
      const preset = SCHEDULE_PRESETS.find((p) => p.cron === parsed.cron)
      return `${preset?.label ?? parsed.cron} (${parsed.timeZone})`
    }
    default:
      return `Unsupported: ${raw}`
  }
}

export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
}
