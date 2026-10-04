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

// The real, executable step types StepActivityFactory.ClassifyStepType
// recognizes — anything else falls back to a no-op placeholder activity.
export type StepType = 'Action' | 'Approval' | 'AI Generate' | 'Email' | 'API Call' | 'Delay' | 'Condition' | 'Transform'

export const STEP_TYPES: StepType[] = ['Action', 'Approval', 'AI Generate', 'Email', 'API Call', 'Delay', 'Condition', 'Transform']

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
 * change layered on top.
 */
export function buildFullWorkflowUpdatePayload(
  workflow: WorkflowDto,
  overrides: Partial<UpdateWorkflowInput>,
): UpdateWorkflowInput {
  return {
    name: workflow.name,
    description: workflow.description ?? undefined,
    type: workflow.type ?? undefined,
    trigger: workflow.trigger ?? undefined,
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
  actions: string[]
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
