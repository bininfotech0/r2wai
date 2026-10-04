import type { ReactNode } from 'react'
import { Box, Stack, Typography } from '@mui/material'
import CheckCircleOutlineOutlined from '@mui/icons-material/CheckCircleOutlineOutlined'
import ErrorOutlineOutlined from '@mui/icons-material/ErrorOutlineOutlined'
import HourglassEmptyOutlined from '@mui/icons-material/HourglassEmptyOutlined'
import RadioButtonUncheckedOutlined from '@mui/icons-material/RadioButtonUncheckedOutlined'

export interface TimelineStep {
  id: string
  /** Step/stage name, e.g. "Validate Invoice" or "Leave Management". */
  label: string
  /** Secondary detail shown next to the label, e.g. step type. */
  description?: string
  /** Any backend status string (Completed/Failed/Running/Pending/Skipped/...); matched case-insensitively. */
  status: string
  timestamp?: string
  durationSeconds?: number
  error?: string
}

const ICON_BY_STATUS: Record<string, ReactNode> = {
  completed: <CheckCircleOutlineOutlined fontSize="small" color="success" />,
  succeeded: <CheckCircleOutlineOutlined fontSize="small" color="success" />,
  failed: <ErrorOutlineOutlined fontSize="small" color="error" />,
  running: <HourglassEmptyOutlined fontSize="small" color="warning" />,
  started: <HourglassEmptyOutlined fontSize="small" color="warning" />,
  pending: <RadioButtonUncheckedOutlined fontSize="small" color="disabled" />,
  skipped: <RadioButtonUncheckedOutlined fontSize="small" color="disabled" />,
}

function iconForStatus(status: string): ReactNode {
  return ICON_BY_STATUS[status.toLowerCase()] ?? <RadioButtonUncheckedOutlined fontSize="small" color="disabled" />
}

interface TimelineProps {
  steps: TimelineStep[]
  emptyMessage?: string
  /** Compact single-line rows with no timestamp/duration — used for live in-chat progress (Phase 2). */
  dense?: boolean
}

/**
 * The shared "✓ step 1 / ◷ step 2" step-list component (brief §7/§10/§16),
 * extracted from RunInspectorDrawer's original hand-rolled version so it can
 * also back Runs and (later) in-chat tool-call progress.
 */
export function Timeline({ steps, emptyMessage = 'No steps recorded yet.', dense = false }: TimelineProps) {
  if (steps.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        {emptyMessage}
      </Typography>
    )
  }

  return (
    <Stack spacing={dense ? 0.5 : 1}>
      {steps.map((step) => (
        <Stack key={step.id} direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
          {iconForStatus(step.status)}
          <Box sx={{ flexGrow: 1, minWidth: 0 }}>
            <Typography variant="body2">
              {step.label}
              {step.description && (
                <Typography component="span" variant="caption" color="text.secondary">
                  {' '}
                  ({step.description})
                </Typography>
              )}
            </Typography>
            {!dense && step.timestamp && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                {new Date(step.timestamp).toLocaleString()}
                {step.durationSeconds != null && ` — ${step.durationSeconds}s`}
              </Typography>
            )}
            {step.error && (
              <Typography variant="caption" color="error">
                {step.error}
              </Typography>
            )}
          </Box>
        </Stack>
      ))}
    </Stack>
  )
}
