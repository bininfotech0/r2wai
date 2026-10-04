import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ReactFlow,
  ReactFlowProvider,
  Background,
  Controls,
  addEdge,
  useNodesState,
  useEdgesState,
  useReactFlow,
  type Connection,
  type Edge,
  type Node,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import AddOutlined from '@mui/icons-material/AddOutlined'
import { ErrorState } from '../../../components/ErrorState'
import { EmptyState } from '../../../components/EmptyState'
import { SchemaForm, type SchemaFormValue } from '../../../components/SchemaForm'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { useUnsavedChangesGuard } from '../../../lib/useUnsavedChangesGuard'
import { executeWorkflow, getWorkflow, publishWorkflow, updateWorkflow } from '../api'
import { buildFullWorkflowUpdatePayload, type StepType, type WorkflowStepConfig } from '../types'
import { NODE_PALETTE, type PaletteNodeDef } from '../builder/nodePalette'
import { graphToSteps, stepsToGraph, type BuilderEdge, type BuilderGraph, type BuilderNode, type BuilderNodeData } from '../builder/graphConversion'
import { nodeTypes, type StatusHubNodeStatus } from '../builder/StepNode'
import { STEP_CONFIG_SCHEMAS } from '../stepSchemas'
import { useStatusHub } from '../builder/useStatusHub'

type FlowNode = Node<BuilderNodeData & { status?: StatusHubNodeStatus }>
type FlowEdge = Edge

function toFlowNodes(nodes: BuilderNode[]): FlowNode[] {
  return nodes.map((n) => ({ id: n.id, position: n.position, data: n.data, type: 'stepNode' }))
}
function toFlowEdges(edges: BuilderEdge[]): FlowEdge[] {
  return edges.map((e) => ({ id: e.id, source: e.source, target: e.target }))
}
function toBuilderGraph(nodes: FlowNode[], edges: FlowEdge[]): BuilderGraph {
  return {
    nodes: nodes.map((n) => ({ id: n.id, position: n.position, data: n.data })),
    edges: edges.map((e) => ({ id: e.id, source: e.source, target: e.target })),
  }
}

let nodeIdCounter = 0

function BuilderCanvas() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const { screenToFlowPosition } = useReactFlow()
  const wrapperRef = useRef<HTMLDivElement>(null)
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))

  const [nodes, setNodes, onNodesChange] = useNodesState<FlowNode>([])
  const [edges, setEdges, onEdgesChange] = useEdgesState<FlowEdge>([])
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null)
  const [isDirty, setIsDirty] = useState(false)
  const [instanceId, setInstanceId] = useState<string | null>(null)
  const [loaded, setLoaded] = useState(false)
  useUnsavedChangesGuard(isDirty)

  const query = useQuery({
    queryKey: ['workflows', id],
    queryFn: () => getWorkflow(id!),
    enabled: !!id,
  })
  const workflow = query.data

  useEffect(() => {
    if (!workflow || loaded) return
    const graph = stepsToGraph(
      workflow.steps
        ? (JSON.parse(workflow.steps) as { order: number; name: string; type?: StepType; action?: string; assignedRole?: string; config?: WorkflowStepConfig }[])
        : [],
    )
    setNodes(toFlowNodes(graph.nodes))
    setEdges(toFlowEdges(graph.edges))
    setLoaded(true)
  }, [workflow, loaded, setNodes, setEdges])

  const { connectionState, stepEvents } = useStatusHub(instanceId)

  useEffect(() => {
    if (stepEvents.length === 0) return
    const latest = stepEvents[stepEvents.length - 1]
    const status: StatusHubNodeStatus = latest.status === 'started' ? 'started' : latest.status === 'completed' ? 'completed' : 'failed'
    setNodes((prev) => prev.map((n) => (n.data.label === latest.stepName ? { ...n, data: { ...n.data, status } } : n)))
  }, [stepEvents, setNodes])

  const onConnect = useCallback(
    (connection: Connection) => {
      setEdges((prev) => addEdge(connection, prev))
      setIsDirty(true)
    },
    [setEdges],
  )

  const addPaletteNode = useCallback((def: PaletteNodeDef, position: { x: number; y: number }) => {
    const newNodeId = `node-${nodeIdCounter++}`
    const data: BuilderNodeData = {
      label: `${def.label} Step`,
      stepType: def.stepType,
      kind: def.kind,
    }
    setNodes((prev) => [...prev, { id: newNodeId, position, data, type: 'stepNode' }])
    setSelectedNodeId(newNodeId)
    setIsDirty(true)
  }, [setNodes])

  const addPaletteNodeAtCanvasCenter = useCallback((def: PaletteNodeDef) => {
    const bounds = wrapperRef.current?.getBoundingClientRect()
    if (!bounds) return
    const addedCount = nodes.filter((node) => node.data.kind === 'step').length
    const offset = (addedCount % 6) * 36
    const position = screenToFlowPosition({
      x: bounds.left + bounds.width / 2 + offset,
      y: bounds.top + bounds.height / 2 + offset,
    })
    addPaletteNode(def, position)
  }, [addPaletteNode, nodes, screenToFlowPosition])

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault()
      const raw = event.dataTransfer.getData('application/r2wai-node')
      if (!raw || !wrapperRef.current) return
      const def = JSON.parse(raw) as PaletteNodeDef
      const position = screenToFlowPosition({ x: event.clientX, y: event.clientY })
      addPaletteNode(def, position)
    },
    [addPaletteNode, screenToFlowPosition],
  )

  const selectedNode = nodes.find((n) => n.id === selectedNodeId) ?? null

  const saveMutation = useMutation({
    mutationFn: () => {
      const steps = graphToSteps(toBuilderGraph(nodes, edges))
      return updateWorkflow(id!, buildFullWorkflowUpdatePayload(workflow!, { steps: JSON.stringify(steps) }))
    },
    onSuccess: () => {
      notify('Automation saved', 'success')
      setIsDirty(false)
      void queryClient.invalidateQueries({ queryKey: ['workflows', id] })
    },
    onError: () => notify('Failed to save', 'error'),
  })

  const testMutation = useMutation({
    mutationFn: () => executeWorkflow(id!),
    onSuccess: (result) => {
      setInstanceId(result.instanceId)
      notify('Test run started. Live updates will appear here when connected.', 'info')
    },
    onError: () => notify('Failed to start test run', 'error'),
  })

  const publishMutation = useMutation({
    mutationFn: () => publishWorkflow(id!),
    onSuccess: () => {
      notify('Automation published', 'success')
      void queryClient.invalidateQueries({ queryKey: ['workflows', id] })
      void queryClient.invalidateQueries({ queryKey: ['workflows'] })
    },
    onError: () => notify('Failed to publish', 'error'),
  })

  const otherStepNames = useMemo(
    () => nodes.filter((n) => n.data.kind === 'step' && n.id !== selectedNodeId).map((n) => n.data.label),
    [nodes, selectedNodeId],
  )

  function updateSelectedNode(patch: Partial<BuilderNodeData>) {
    setNodes((prev) => prev.map((n) => (n.id === selectedNodeId ? { ...n, data: { ...n.data, ...patch } } : n)))
    setIsDirty(true)
  }

  if (query.isError) {
    return <ErrorState title="Unable to load automation builder" description="The workflow editor could not retrieve this automation." onRetry={() => void query.refetch()} />
  }

  if (query.isLoading) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1, py: 8 }} role="status" aria-label="Loading automation">
        <CircularProgress />
        <Typography variant="body2" color="text.secondary">Loading automation…</Typography>
      </Box>
    )
  }

  if (!workflow) {
    return (
      <EmptyState
        title="Automation not found"
        description="It may have been removed, or you may not have access to it."
        actionLabel="Back to automations"
        onAction={() => navigate('/automations')}
      />
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: { xs: 'auto', md: 'calc(100vh - 120px)' }, minHeight: { xs: 520, md: 560 } }}>
      <Stack
        direction={{ xs: 'column', lg: 'row' }}
        spacing={1}
        sx={{ alignItems: { xs: 'stretch', lg: 'center' }, justifyContent: 'space-between', mb: 1.5 }}
      >
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 1 }}>
          <Button component={RouterLink} to={`/automations/${id}`} startIcon={<ArrowBackIcon />} size="small">
            {workflow.name}
          </Button>
          <Chip label={workflow.versionStatus} size="small" />
        </Stack>
        <Stack direction="row" spacing={0} sx={{ alignItems: 'center', justifyContent: { xs: 'flex-start', lg: 'flex-end' }, flexWrap: 'wrap', gap: 1 }}>
          {connectionState !== 'connected' && (
            <Chip size="small" label={`Live updates: ${connectionState}`} color="warning" variant="outlined" />
          )}
          <Button onClick={() => testMutation.mutate()} disabled={testMutation.isPending || isDirty}>
            {testMutation.isPending ? 'Starting…' : 'Test'}
          </Button>
          <Button variant="outlined" disabled={!isDirty || saveMutation.isPending} onClick={() => saveMutation.mutate()}>
            {saveMutation.isPending ? 'Saving…' : 'Save'}
          </Button>
          <Button variant="contained" onClick={() => publishMutation.mutate()} disabled={publishMutation.isPending || isDirty}>
            Publish
          </Button>
        </Stack>
      </Stack>

      {isDirty && (
        <Alert severity="warning" sx={{ mb: 1.5 }}>
          You have unsaved changes. Save them before testing or publishing.
        </Alert>
      )}

      {isMobile && (
        <Alert severity="info" sx={{ mb: 1.5 }}>
          View-only on mobile — open on a tablet or desktop to edit the automation.
        </Alert>
      )}

      <Box sx={{ display: 'flex', flexGrow: 1, gap: 1.5, minHeight: 0 }}>
        {!isMobile && (
        <Paper variant="outlined" sx={{ width: 200, p: 1.5, overflowY: 'auto' }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
            Drag or click a step to add it. Start and end are automatic; set the trigger in details.
          </Typography>
          {NODE_PALETTE.map((cat) => (
            <Box key={cat.category} sx={{ mb: 2 }}>
              <Typography variant="caption" sx={{ fontWeight: 700, color: 'text.secondary' }}>
                {cat.category.toUpperCase()}
              </Typography>
              <Stack spacing={0.5} sx={{ mt: 0.5 }}>
                {cat.items.map((item) => (
                  <Button
                    key={item.label}
                    type="button"
                    fullWidth
                    size="small"
                    variant="outlined"
                    draggable
                    onDragStart={(e) => e.dataTransfer.setData('application/r2wai-node', JSON.stringify(item))}
                    onClick={() => addPaletteNodeAtCanvasCenter(item)}
                    startIcon={<AddOutlined fontSize="small" />}
                    sx={{ justifyContent: 'flex-start', textTransform: 'none', cursor: 'grab' }}
                  >
                    {item.label}
                  </Button>
                ))}
              </Stack>
            </Box>
          ))}
        </Paper>
        )}

        <Box
          ref={wrapperRef}
          sx={{ flexGrow: 1, minWidth: 0 }}
          onDrop={isMobile ? undefined : onDrop}
          onDragOver={isMobile ? undefined : (e) => e.preventDefault()}
        >
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={(changes) => {
              onNodesChange(changes)
              if (changes.some((change) => change.type === 'position' || change.type === 'add' || change.type === 'remove' || change.type === 'replace')) {
                setIsDirty(true)
              }
            }}
            onEdgesChange={(changes) => {
              onEdgesChange(changes)
              if (changes.some((change) => change.type === 'add' || change.type === 'remove' || change.type === 'replace')) {
                setIsDirty(true)
              }
            }}
            onConnect={onConnect}
            onNodeClick={(_, node) => setSelectedNodeId(node.id)}
            onPaneClick={() => setSelectedNodeId(null)}
            nodeTypes={nodeTypes}
            nodesDraggable={!isMobile}
            nodesConnectable={!isMobile}
            elementsSelectable={!isMobile}
            fitView
          >
            <Background />
            <Controls />
          </ReactFlow>
        </Box>

        {!isMobile && (
        <Paper variant="outlined" sx={{ width: 300, p: 2, overflowY: 'auto' }}>
          {!selectedNode ? (
            <Typography variant="body2" color="text.secondary">
              Select a node to edit its properties.
            </Typography>
          ) : selectedNode.data.kind !== 'step' ? (
            <Typography variant="body2" color="text.secondary">
              {selectedNode.data.label} has no configurable properties.
            </Typography>
          ) : (
            <Stack spacing={2}>
              <TextField
                label="Name"
                size="small"
                value={selectedNode.data.label}
                onChange={(e) => updateSelectedNode({ label: e.target.value })}
              />
              <Typography variant="caption" color="text.secondary">
                Type: {selectedNode.data.stepType}
              </Typography>
              {selectedNode.data.stepType && Object.keys(STEP_CONFIG_SCHEMAS[selectedNode.data.stepType].properties).length > 0 && (
                <SchemaForm
                  schema={STEP_CONFIG_SCHEMAS[selectedNode.data.stepType]}
                  value={(selectedNode.data.config as SchemaFormValue) ?? {}}
                  onChange={(config) => updateSelectedNode({ config: config as WorkflowStepConfig })}
                />
              )}
              {otherStepNames.length > 0 && (
                <Typography variant="caption" color="text.secondary">
                  Connect this node to another step on the canvas to define what runs next.
                </Typography>
              )}
            </Stack>
          )}
        </Paper>
        )}
      </Box>
    </Box>
  )
}

export function AutomationBuilderPage() {
  return (
    <ReactFlowProvider>
      <BuilderCanvas />
    </ReactFlowProvider>
  )
}
