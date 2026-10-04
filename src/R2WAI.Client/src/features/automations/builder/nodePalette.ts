import type { StepType } from '../types'

export interface PaletteNodeDef {
  label: string
  /** Structural markers (Start/Schedule/End) carry no stepType and never
   * appear in the serialized Steps JSON — see graphConversion.ts. */
  stepType?: StepType
  kind: 'start' | 'end' | 'step'
}

export interface PaletteCategory {
  category: string
  items: PaletteNodeDef[]
}

/**
 * The step palette for the advanced builder.
 * "Tool" and "API" both currently produce an "API Call" step — the workflow
 * engine (StepActivityFactory) has no distinct tool-invocation activity yet,
 * only raw HTTP calls, so there's no honest way to differentiate them at
 * execution time. "Decision" is Intelligence's framing of the same
 * "Condition" step Logic offers — same execution, different palette intent.
 */
export const NODE_PALETTE: PaletteCategory[] = [
  {
    category: 'Intelligence',
    items: [
      { label: 'AI', stepType: 'AI Generate', kind: 'step' },
      { label: 'Decision', stepType: 'Condition', kind: 'step' },
    ],
  },
  {
    category: 'Logic',
    items: [
      { label: 'Condition', stepType: 'Condition', kind: 'step' },
      { label: 'Transform', stepType: 'Transform', kind: 'step' },
    ],
  },
  {
    category: 'Actions',
    items: [
      { label: 'Tool', stepType: 'API Call', kind: 'step' },
      { label: 'API', stepType: 'API Call', kind: 'step' },
      { label: 'Notify', stepType: 'Email', kind: 'step' },
    ],
  },
  {
    category: 'Human',
    items: [{ label: 'Approval', stepType: 'Approval', kind: 'step' }],
  },
  {
    category: 'Flow',
    items: [{ label: 'Wait', stepType: 'Delay', kind: 'step' }],
  },
]
