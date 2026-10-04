import { describe, it, expect } from 'vitest'
import { reduceToolCallSteps } from './toolCallProgress'

describe('reduceToolCallSteps', () => {
  it('renders a single tool call started then completed', () => {
    const steps = reduceToolCallSteps([
      { toolName: 'Get Leave Balance', status: 'started' },
      { toolName: 'Get Leave Balance', status: 'completed', success: true },
    ])
    expect(steps).toEqual([{ id: 'Get Leave Balance-0', label: 'Get Leave Balance', status: 'completed' }])
  })

  it('marks a failed completion distinctly', () => {
    const steps = reduceToolCallSteps([
      { toolName: 'Submit Invoice', status: 'started' },
      { toolName: 'Submit Invoice', status: 'completed', success: false },
    ])
    expect(steps[0].status).toBe('failed')
  })

  it('leaves a still-running call as running', () => {
    const steps = reduceToolCallSteps([{ toolName: 'Search Knowledge Base', status: 'started' }])
    expect(steps[0].status).toBe('running')
  })

  it('matches sequential calls to the same tool independently', () => {
    const steps = reduceToolCallSteps([
      { toolName: 'Get Leave Balance', status: 'started' },
      { toolName: 'Get Leave Balance', status: 'completed', success: true },
      { toolName: 'Get Leave Balance', status: 'started' },
      { toolName: 'Get Leave Balance', status: 'completed', success: false },
    ])
    expect(steps).toHaveLength(2)
    expect(steps[0].status).toBe('completed')
    expect(steps[1].status).toBe('failed')
  })
})
