import { describe, it, expect } from 'vitest'
import { approvalTitle, hasWorkflowRun, historyTitle } from './display'

describe('approvalTitle', () => {
  it('prefers the subject of the action being confirmed', () => {
    expect(approvalTitle({ subject: 'Submit supplier ABC Industries', workflowName: 'Supplier update' })).toBe(
      'Submit supplier ABC Industries',
    )
  })

  it('falls back to the workflow name for an approval raised by a workflow step', () => {
    expect(approvalTitle({ subject: null, workflowName: 'Supplier update' })).toBe('Supplier update')
  })

  it('has a neutral title when the request has neither', () => {
    expect(approvalTitle({ subject: null, workflowName: null })).toBe('Confirmation request')
    expect(approvalTitle({ subject: '', workflowName: '' })).toBe('Confirmation request')
  })
})

describe('historyTitle', () => {
  it('uses the subject, then the workflow id, then a neutral title', () => {
    expect(historyTitle({ subject: 'Submit supplier ABC Industries', workflowId: null })).toBe('Submit supplier ABC Industries')
    expect(historyTitle({ subject: null, workflowId: '5f1c0d7e-1111-2222-3333-444444444444' })).toBe('Workflow 5f1c0d7e…')
    expect(historyTitle({ subject: null, workflowId: null })).toBe('Confirmation request')
  })
})

describe('hasWorkflowRun', () => {
  it('is true only when the request belongs to a workflow instance', () => {
    expect(hasWorkflowRun({ workflowInstanceId: '5f1c0d7e-0000-0000-0000-000000000001' })).toBe(true)
    expect(hasWorkflowRun({ workflowInstanceId: null })).toBe(false)
  })
})
