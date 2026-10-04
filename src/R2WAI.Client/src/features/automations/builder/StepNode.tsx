import { Handle, Position, type NodeProps } from '@xyflow/react'
import { Chip, Paper, Typography } from '@mui/material'
import type { BuilderNodeData } from './graphConversion'

export type StatusHubNodeStatus = 'idle' | 'started' | 'completed' | 'failed'

const STATUS_COLOR: Record<StatusHubNodeStatus, string> = {
  idle: 'transparent',
  started: '#F59E0B',
  completed: '#22C55E',
  failed: '#EF4444',
}

export function StepNode({ data, selected }: NodeProps & { data: BuilderNodeData & { status?: StatusHubNodeStatus } }) {
  const isBookend = data.kind !== 'step'
  const status = data.status ?? 'idle'

  return (
    <Paper
      variant="outlined"
      sx={{
        px: 2,
        py: 1.25,
        minWidth: isBookend ? 100 : 160,
        borderColor: selected ? 'primary.main' : status !== 'idle' ? STATUS_COLOR[status] : 'divider',
        borderWidth: selected || status !== 'idle' ? 2 : 1,
        borderRadius: isBookend ? 4 : 1.5,
        textAlign: 'center',
        bgcolor: isBookend ? 'action.hover' : 'background.paper',
      }}
    >
      {data.kind !== 'start' && <Handle type="target" position={Position.Top} />}
      <Typography variant="body2" sx={{ fontWeight: isBookend ? 600 : 500 }}>
        {data.label}
      </Typography>
      {!isBookend && data.stepType && (
        <Chip label={data.stepType} size="small" variant="outlined" sx={{ mt: 0.5, height: 18, fontSize: '0.65rem' }} />
      )}
      {data.kind !== 'end' && <Handle type="source" position={Position.Bottom} />}
    </Paper>
  )
}

export const nodeTypes = { stepNode: StepNode }
