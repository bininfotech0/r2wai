import type { StepType, WorkflowStep, WorkflowStepConfig } from '../types'

// Extends Record<string, unknown> — @xyflow/react's Node<T> constrains node
// data to that shape.
export interface BuilderNodeData extends Record<string, unknown> {
  label: string
  stepType?: StepType
  kind: 'start' | 'end' | 'step'
  config?: WorkflowStepConfig
  assignedRole?: string
}

export interface BuilderNode {
  id: string
  position: { x: number; y: number }
  data: BuilderNodeData
}

export interface BuilderEdge {
  id: string
  source: string
  target: string
}

export interface BuilderGraph {
  nodes: BuilderNode[]
  edges: BuilderEdge[]
}

const START_ID = 'start'
const END_ID = 'end'

/**
 * Pure, framework-free graph <-> WorkflowStep[] conversion — the core of
 * Phase 8's "round-trips identically" requirement. Start/End are synthetic
 * bookend nodes, always regenerated deterministically from the real steps'
 * topology rather than persisted, so they never cause a round-trip mismatch.
 * Every real step explicitly lists its own `nextSteps` (rather than relying
 * on the backend's next-in-order fallback) so the graph's exact shape is
 * unambiguous on reload, and node canvas positions ride along in
 * `config._canvasPosition` — an extra field StepActivityFactory's
 * StepConfigDto deserialization silently ignores (System.Text.Json skips
 * unmapped properties by default), so it never affects real execution.
 */
export function stepsToGraph(steps: WorkflowStep[]): BuilderGraph {
  const sorted = [...steps].sort((a, b) => a.order - b.order)
  const nodeIdByName = new Map(sorted.map((s, i) => [s.name, `step-${i}`]))

  const stepNodes: BuilderNode[] = sorted.map((step, i) => {
    const { nextSteps: _nextSteps, _canvasPosition, ...config } = (step.config ?? {}) as WorkflowStepConfig & {
      _canvasPosition?: { x: number; y: number }
    }
    return {
      id: nodeIdByName.get(step.name)!,
      position: _canvasPosition ?? { x: 300, y: 120 + i * 140 },
      data: {
        label: step.name,
        stepType: step.type,
        kind: 'step',
        assignedRole: step.assignedRole,
        config: Object.keys(config).length > 0 ? config : undefined,
      },
    }
  })

  const stepEdges: BuilderEdge[] = []
  const hasIncoming = new Set<string>()
  for (const step of sorted) {
    const sourceId = nodeIdByName.get(step.name)!
    const explicitNext = step.config?.nextSteps ?? []
    const targets = explicitNext.length > 0 ? explicitNext : []
    for (const targetName of targets) {
      const targetId = nodeIdByName.get(targetName)
      if (!targetId) continue
      stepEdges.push({ id: `${sourceId}->${targetId}`, source: sourceId, target: targetId })
      hasIncoming.add(targetId)
    }
  }
  // Steps saved before nextSteps was populated (or with none set) fall back
  // to next-in-order, mirroring WorkflowBridge's own fallback behavior.
  for (let i = 0; i < sorted.length; i++) {
    const step = sorted[i]
    const sourceId = nodeIdByName.get(step.name)!
    const alreadyHasOutgoing = stepEdges.some((e) => e.source === sourceId)
    if (!alreadyHasOutgoing && (step.config?.nextSteps?.length ?? 0) === 0 && i + 1 < sorted.length) {
      const targetId = nodeIdByName.get(sorted[i + 1].name)!
      stepEdges.push({ id: `${sourceId}->${targetId}`, source: sourceId, target: targetId })
      hasIncoming.add(targetId)
    }
  }

  const startTargets = stepNodes.filter((n) => !hasIncoming.has(n.id))
  const endSources = stepNodes.filter((n) => !stepEdges.some((e) => e.source === n.id))

  const startNode: BuilderNode = { id: START_ID, position: { x: 300, y: 0 }, data: { label: 'Start', kind: 'start' } }
  const endNode: BuilderNode = {
    id: END_ID,
    position: { x: 300, y: 120 + sorted.length * 140 },
    data: { label: 'End', kind: 'end' },
  }

  const bookendEdges: BuilderEdge[] = [
    ...startTargets.map((n) => ({ id: `${START_ID}->${n.id}`, source: START_ID, target: n.id })),
    ...endSources.map((n) => ({ id: `${n.id}->${END_ID}`, source: n.id, target: END_ID })),
  ]

  return {
    nodes: [startNode, ...stepNodes, endNode],
    edges: [...bookendEdges, ...stepEdges],
  }
}

export function graphToSteps(graph: BuilderGraph): WorkflowStep[] {
  const stepNodes = graph.nodes.filter((n) => n.data.kind === 'step')
  const nodeById = new Map(graph.nodes.map((n) => [n.id, n]))

  // Topological order via BFS from Start (or from any step with no incoming
  // edge from another step, if there's no explicit Start connection) — this
  // determines each step's `order` in the resulting array.
  const incomingCount = new Map(stepNodes.map((n) => [n.id, 0]))
  for (const edge of graph.edges) {
    if (nodeById.get(edge.source)?.data.kind === 'step' && incomingCount.has(edge.target)) {
      incomingCount.set(edge.target, (incomingCount.get(edge.target) ?? 0) + 1)
    }
  }

  const startEdges = graph.edges.filter((e) => e.source === START_ID)
  const queue: string[] = startEdges.length > 0
    ? startEdges.map((e) => e.target).filter((id) => nodeById.get(id)?.data.kind === 'step')
    : stepNodes.filter((n) => (incomingCount.get(n.id) ?? 0) === 0).map((n) => n.id)

  const visited = new Set<string>()
  const order: string[] = []
  while (queue.length > 0) {
    const id = queue.shift()!
    if (visited.has(id)) continue
    visited.add(id)
    order.push(id)
    for (const edge of graph.edges.filter((e) => e.source === id)) {
      if (nodeById.get(edge.target)?.data.kind === 'step' && !visited.has(edge.target)) {
        queue.push(edge.target)
      }
    }
  }
  // Any step unreachable from Start (disconnected island) still gets serialized, appended at the end.
  for (const n of stepNodes) {
    if (!visited.has(n.id)) order.push(n.id)
  }

  const nameById = new Map(order.map((id) => [id, nodeById.get(id)!.data.label]))

  return order.map((id, index) => {
    const node = nodeById.get(id)!
    const nextStepIds = graph.edges.filter((e) => e.source === id && nodeById.get(e.target)?.data.kind === 'step')
    const nextSteps = nextStepIds.map((e) => nameById.get(e.target)!).filter((n): n is string => !!n)

    const config: WorkflowStepConfig & { _canvasPosition?: { x: number; y: number } } = {
      ...(node.data.config ?? {}),
      nextSteps,
      _canvasPosition: node.position,
    }

    const step: WorkflowStep = {
      order: index,
      name: node.data.label,
      type: node.data.stepType,
      action: node.data.stepType === 'Action' ? node.data.label : undefined,
      assignedRole: node.data.assignedRole,
      config,
    }
    return step
  })
}
