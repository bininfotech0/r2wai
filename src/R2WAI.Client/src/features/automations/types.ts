import { toSupportedTrigger } from './trigger'

// Mirrors R2WAI.Application.Features.Workflows.DTOs.WorkflowDto (camelCase).
export interface WorkflowDto {
  id: string
  applicationId: string | null
  name: string
  description: string | null
  type: string | null
  trigger: string | null
  steps: string | null // JSON-encoded WorkflowStep[]
  isActive: boolean
  version: number
  versionStatus: 'Draft' | 'Published' | 'Archived'
  isArchived: boolean
  createdAt: string
  modifiedAt: string | null
}

// The step types StepActivityFactory.ClassifyStepType recognizes. 'Action' is the no-op placeholder
// (it only logs a line); it stays in the type for existing data but is never offered for new steps.
export type StepType = 'Action' | 'Approval' | 'AI Generate' | 'Email' | 'API Call' | 'Delay' | 'Condition' | 'Transform'

/** The executable step types a user can choose. */
export const STEP_TYPES: Exclude<StepType, 'Action'>[] = ['Approval', 'AI Generate', 'Email', 'API Call', 'Delay', 'Condition', 'Transform']

/**
 * Mirrors StepActivityFactory.ClassifyStepType (including its legacy name-matching fallback), so the
 * publish checklist flags exactly the steps the server's publish gate rejects.
 */
export function isPlaceholderStep(step: Pick<WorkflowStep, 'name' | 'type' | 'action'>): boolean {
  const key = (step.type ?? step.action ?? '').trim().toLowerCase()
  if (key === 'action') return true
  if (['approval', 'ai generate', 'email', 'api call', 'condition', 'delay', 'transform', 'log'].includes(key)) return false
  const legacy = `${step.name} ${step.action ?? ''}`.toLowerCase()
  return !legacy.includes('approval') && !legacy.includes('ai')
}

// Mirrors R2WAI.Api.Workflows.StepActivityFactory's StepConfigDto — the only
// fields each step type's activity actually reads at execution time.
export interface WorkflowStepConfig {
  aiPrompt?: string
  aiMaxTokens?: number
  aiTemperature?: number
  emailTo?: string
  emailCc?: string
  emailSubject?: string
  emailBody?: string
  apiUrl?: string
  apiMethod?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH'
  apiBody?: string
  apiHeaders?: string // JSON object string, e.g. {"Authorization":"Bearer ..."}
  delayDuration?: number
  delayUnit?: 'minutes' | 'hours' | 'days'
  conditionExpression?: string
  transformInput?: string
  transformOperation?: string
  transformExpression?: string
  transformOutput?: string
  /** Explicit branching — fans out to these step names; falls back to next-in-order if empty. */
  nextSteps?: string[]
}

// Mirrors R2WAI.Application.Features.Workflows.DTOs.WorkflowStepDto (camelCase).
export interface WorkflowStep {
  order: number
  name: string
  assignedRole?: string
  action?: string
  type?: StepType
  config?: WorkflowStepConfig
  status?: string
  comments?: string
  completedAt?: string
}

export interface CreateWorkflowInput {
  name: string
  description?: string
  type?: string
  steps?: string
  trigger?: string
}

export interface UpdateWorkflowInput {
  name: string
  description?: string
  type?: string
  trigger?: string | null
  steps?: string
}

/**
 * The API's PUT /workflows/{id} does a blind overwrite of description/type/
 * trigger/steps (Workflow.UpdateDetails has no null-coalescing against the
 * existing entity, same pattern as Assistants — see assistants/types.ts).
 * Every update must send the full current field set with just the intended
 * change layered on top. A stored trigger that can't start a run (a removed event
 * label, or a bare "Schedule" with no cron) is dropped — the server rejects it, and
 * it never fired anyway — so saving an older automation makes it Manual.
 */
export function buildFullWorkflowUpdatePayload(
  workflow: WorkflowDto,
  overrides: Partial<UpdateWorkflowInput>,
): UpdateWorkflowInput {
  return {
    name: workflow.name,
    description: workflow.description ?? undefined,
    type: workflow.type ?? undefined,
    trigger: toSupportedTrigger(workflow.trigger),
    steps: workflow.steps ?? undefined,
    ...overrides,
  }
}

export interface WorkflowDraftCondition {
  field: string
  operator: string
  value: string
}

export interface WorkflowDraft {
  name: string | null
  trigger: string | null
  /** Step types in order; `steps` adds the model's own name for each. */
  actions: string[]
  steps?: { name: string; type: Exclude<StepType, 'Action'> }[]
  conditions: WorkflowDraftCondition[]
}

export interface WorkflowTemplateStep {
  name: string
  action: string
  assignedRole: string
  order: number
}

export interface WorkflowTemplate {
  id: string
  name: string
  description: string
  type: string
  steps: WorkflowTemplateStep[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
