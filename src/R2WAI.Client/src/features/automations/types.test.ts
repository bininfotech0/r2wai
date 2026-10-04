import { describe, it, expect } from 'vitest'
import { buildFullWorkflowUpdatePayload } from './types'
import type { WorkflowDto } from './types'

const baseWorkflow: WorkflowDto = {
  id: 'w1',
  applicationId: null,
  name: 'Employee Onboarding',
  description: 'Onboards new hires',
  type: 'Process',
  trigger: 'Form Submitted',
  steps: '[{"order":0,"name":"Create Task","type":"Action"}]',
  isActive: true,
  version: 1,
  versionStatus: 'Published',
  isArchived: false,
  createdAt: '2026-01-01T00:00:00Z',
  modifiedAt: null,
}

describe('buildFullWorkflowUpdatePayload', () => {
  // Workflow.UpdateDetails blindly overwrites description/type/trigger/steps
  // server-side (no null-coalescing), same pattern as Assistants — these
  // tests guard against a partial update silently wiping other fields.
  it('carries every existing field forward when only steps changes', () => {
    const payload = buildFullWorkflowUpdatePayload(baseWorkflow, { steps: '[]' })
    expect(payload.name).toBe('Employee Onboarding')
    expect(payload.description).toBe('Onboards new hires')
    expect(payload.type).toBe('Process')
    expect(payload.trigger).toBe('Form Submitted')
    expect(payload.steps).toBe('[]')
  })

  it('carries every existing field forward when only trigger changes', () => {
    const payload = buildFullWorkflowUpdatePayload(baseWorkflow, { trigger: 'Webhook' })
    expect(payload.trigger).toBe('Webhook')
    expect(payload.steps).toBe(baseWorkflow.steps)
    expect(payload.description).toBe(baseWorkflow.description)
  })

  it('converts null fields to undefined rather than sending literal nulls', () => {
    const withNulls: WorkflowDto = { ...baseWorkflow, description: null, trigger: null, type: null }
    const payload = buildFullWorkflowUpdatePayload(withNulls, {})
    expect(payload.description).toBeUndefined()
    expect(payload.trigger).toBeUndefined()
    expect(payload.type).toBeUndefined()
  })
})
