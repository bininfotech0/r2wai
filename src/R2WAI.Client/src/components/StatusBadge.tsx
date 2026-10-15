import { Chip } from '@mui/material'

export type StatusTone = 'success' | 'warning' | 'error' | 'info' | 'default'

/**
 * One status→color mapping shared across the app, replacing the scattered
 * per-page `Chip color={STATUS_COLOR[value]}` maps in Runs, Approvals,
 * Integrations, Tools, Departments, etc. Covers the status vocabulary
 * already in use across those pages; pass `tone` or `toneMap` to override
 * for a status this default doesn't know about.
 */
const DEFAULT_STATUS_TONES: Record<string, StatusTone> = {
  Active: 'success',
  Connected: 'success',
  Completed: 'success',
  Succeeded: 'success',
  Approved: 'success',
  Enabled: 'success',
  Published: 'success',
  Healthy: 'success',

  Pending: 'info',
  Draft: 'default',
  Started: 'info',
  Inactive: 'default',
  Disabled: 'default',
  Cancelled: 'default',
  Skipped: 'default',

  Running: 'info',
  Suspended: 'warning',
  Escalated: 'warning',
  Creating: 'info',
  Overdue: 'warning',

  Failed: 'error',
  Error: 'error',
  Rejected: 'error',
  Unhealthy: 'error',

  // Tool-call ledger outcomes.
  Denied: 'error',
  AwaitingApproval: 'warning',
  Prepared: 'warning',
  Unknown: 'warning',
}

// Multi-word status values as they arrive from the API, shown as words.
const STATUS_LABELS: Record<string, string> = {
  AwaitingApproval: 'Awaiting approval',
}

interface StatusBadgeProps {
  status: string
  /** Force a specific tone regardless of the status text (e.g. a risk label that isn't itself a status). */
  tone?: StatusTone
  /** Page-specific overrides/additions merged over the shared default map. */
  toneMap?: Record<string, StatusTone>
}

export function StatusBadge({ status, tone, toneMap }: StatusBadgeProps) {
  const resolved = tone ?? toneMap?.[status] ?? DEFAULT_STATUS_TONES[status] ?? 'default'
  return (
    <Chip
      label={STATUS_LABELS[status] ?? status}
      size="small"
      color={resolved === 'default' ? 'default' : resolved}
      variant="outlined"
    />
  )
}
