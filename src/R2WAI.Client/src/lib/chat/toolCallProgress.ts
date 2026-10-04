import type { TimelineStep } from '../../components/Timeline'
import type { ToolCallProgressEvent } from './types'

/**
 * Reduces a raw started/completed event log into Timeline steps for the dense in-chat
 * "Checking {Capability}..." checklist (brief §7/§10, redesign plan Phase 2). Matches each
 * "completed" event to the most recent still-running step with the same tool name, so
 * sequential and repeated calls to the same tool both render correctly.
 */
export function reduceToolCallSteps(events: ToolCallProgressEvent[]): TimelineStep[] {
  const steps: TimelineStep[] = []
  events.forEach((event, index) => {
    if (event.status === 'started') {
      steps.push({ id: `${event.toolName}-${index}`, label: event.toolName, status: 'running' })
      return
    }
    const pending = [...steps].reverse().find((s) => s.label === event.toolName && s.status === 'running')
    if (pending) pending.status = event.success === false ? 'failed' : 'completed'
  })
  return steps
}
