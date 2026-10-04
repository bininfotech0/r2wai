import { describe, it, expect } from 'vitest'
import { graphToSteps, stepsToGraph } from './graphConversion'
import type { WorkflowStep } from '../types'

const linearSteps: WorkflowStep[] = [
  { order: 0, name: 'Validate Data', type: 'Condition', config: { conditionExpression: 'amount > 0' } },
  { order: 1, name: 'Create Tasks', type: 'Action', action: 'Create Tasks' },
  { order: 2, name: 'Send Email', type: 'Email', config: { emailTo: 'hr@company.com', emailSubject: 'Welcome' } },
]

const branchingSteps: WorkflowStep[] = [
  { order: 0, name: 'Check Amount', type: 'Condition', config: { conditionExpression: 'amount > 1000', nextSteps: ['Manager Approval', 'Auto Approve'] } },
  { order: 1, name: 'Manager Approval', type: 'Approval' },
  { order: 2, name: 'Auto Approve', type: 'Action', action: 'Auto Approve' },
]

describe('stepsToGraph / graphToSteps round-trip', () => {
  it('preserves step names, types, and order for a linear chain', () => {
    const graph = stepsToGraph(linearSteps)
    const roundTripped = graphToSteps(graph)

    expect(roundTripped.map((s) => s.name)).toEqual(['Validate Data', 'Create Tasks', 'Send Email'])
    expect(roundTripped.map((s) => s.type)).toEqual(['Condition', 'Action', 'Email'])
    expect(roundTripped.map((s) => s.order)).toEqual([0, 1, 2])
  })

  it('preserves step config fields', () => {
    const graph = stepsToGraph(linearSteps)
    const roundTripped = graphToSteps(graph)

    expect(roundTripped[0].config?.conditionExpression).toBe('amount > 0')
    expect(roundTripped[2].config?.emailTo).toBe('hr@company.com')
    expect(roundTripped[2].config?.emailSubject).toBe('Welcome')
  })

  it('preserves an explicit branch (one step fanning out to two)', () => {
    const graph = stepsToGraph(branchingSteps)
    const roundTripped = graphToSteps(graph)

    const checkAmount = roundTripped.find((s) => s.name === 'Check Amount')
    expect(checkAmount?.config?.nextSteps).toEqual(expect.arrayContaining(['Manager Approval', 'Auto Approve']))
    expect(checkAmount?.config?.nextSteps).toHaveLength(2)
  })

  it('is stable under a second round-trip (graph -> steps -> graph -> steps)', () => {
    const graph1 = stepsToGraph(linearSteps)
    const steps1 = graphToSteps(graph1)
    const graph2 = stepsToGraph(steps1)
    const steps2 = graphToSteps(graph2)

    expect(steps2.map((s) => s.name)).toEqual(steps1.map((s) => s.name))
    expect(steps2.map((s) => s.type)).toEqual(steps1.map((s) => s.type))
    expect(steps2.map((s) => s.config?.nextSteps)).toEqual(steps1.map((s) => s.config?.nextSteps))
  })

  it('creates a Start node connected to the entry step and an End node connected to the terminal step', () => {
    const graph = stepsToGraph(linearSteps)
    const startNode = graph.nodes.find((n) => n.data.kind === 'start')!
    const endNode = graph.nodes.find((n) => n.data.kind === 'end')!

    const startEdge = graph.edges.find((e) => e.source === startNode.id)!
    const firstStepNode = graph.nodes.find((n) => n.id === startEdge.target)!
    expect(firstStepNode.data.label).toBe('Validate Data')

    const endEdge = graph.edges.find((e) => e.target === endNode.id)!
    const lastStepNode = graph.nodes.find((n) => n.id === endEdge.source)!
    expect(lastStepNode.data.label).toBe('Send Email')
  })

  it('round-trips an empty workflow to an empty step list', () => {
    const graph = stepsToGraph([])
    expect(graphToSteps(graph)).toEqual([])
  })

  it('carries canvas position through config for a stable layout on reload', () => {
    const graph = stepsToGraph(linearSteps)
    const moved = {
      ...graph,
      nodes: graph.nodes.map((n) => (n.data.label === 'Create Tasks' ? { ...n, position: { x: 999, y: 42 } } : n)),
    }
    const steps = graphToSteps(moved)
    const reloadedGraph = stepsToGraph(steps)
    const node = reloadedGraph.nodes.find((n) => n.data.label === 'Create Tasks')!
    expect(node.position).toEqual({ x: 999, y: 42 })
  })
})
