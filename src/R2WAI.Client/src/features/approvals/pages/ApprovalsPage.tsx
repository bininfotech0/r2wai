import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Grid,
  Pagination,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import InboxOutlined from '@mui/icons-material/InboxOutlined'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { PageHeader } from '../../../components/PageHeader'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { RunInspectorDrawer } from '../../runs/components/RunInspectorDrawer'
import type { RunItem } from '../../runs/types'
import { approveRequest, listApprovalHistory, listPendingApprovals, rejectRequest } from '../api'
import { approvalTitle, hasWorkflowRun, historyTitle } from '../display'
import type { ApprovalHistoryItem, PendingApprovalDto } from '../types'

const ESCALATION_COLOR: Record<number, 'default' | 'warning' | 'error'> = { 0: 'default', 1: 'warning', 2: 'error' }

function escalationColor(level: number) {
  return ESCALATION_COLOR[Math.min(level, 2)] ?? 'error'
}

function requesterName(a: PendingApprovalDto) {
  const name = [a.requesterFirstName, a.requesterLastName].filter(Boolean).join(' ')
  return name || 'Unknown requester'
}

// The Data field is an arbitrary string set by whoever configured the Approval step in the
// automation builder — not a guaranteed JSON shape. Only rendered as key/value rows when it
// genuinely parses to a plain object; otherwise shown as plain text rather than assuming a
// structure that isn't actually there.
function parseDataSummary(data: string | null): Array<[string, string]> | string | null {
  if (!data) return null
  try {
    const parsed: unknown = JSON.parse(data)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return Object.entries(parsed as Record<string, unknown>).map(([k, v]) => [k, typeof v === 'string' ? v : JSON.stringify(v)])
    }
  } catch {
    // Not JSON — fall through to plain text.
  }
  return data
}

// Null for a request that was not raised by a workflow step: there is no run to open.
function toRunItem(a: PendingApprovalDto): RunItem | null {
  if (!a.workflowInstanceId) return null
  return {
    id: a.workflowInstanceId,
    type: 'Automation',
    name: approvalTitle(a),
    // Real, not a guess: an Approval step always means Elsa suspended the instance on a
    // bookmark awaiting this decision — RunInspectorDrawer re-fetches the live instance/steps
    // itself, this is only the header's initial label.
    status: 'Suspended',
    startedAt: a.requestedAt,
    completedAt: null,
    applicationId: null,
    applicationName: a.applicationName,
    linkedId: null,
    linkedName: null,
    userId: null,
    userName: requesterName(a),
    correlationId: null,
  }
}

export function ApprovalsPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [tab, setTab] = useState<'Pending' | 'Approved' | 'Rejected'>('Pending')
  const [page, setPage] = useState(1)
  const [acting, setActing] = useState<{ item: PendingApprovalDto; action: 'approve' | 'reject' } | null>(null)
  const [comments, setComments] = useState('')
  const [viewingRun, setViewingRun] = useState<RunItem | null>(null)

  const pendingQuery = useQuery({
    queryKey: ['approvals', 'pending', page],
    queryFn: () => listPendingApprovals(page, 50),
    enabled: tab === 'Pending',
  })
  const historyQuery = useQuery({
    queryKey: ['approvals', 'history', tab, page],
    queryFn: () => listApprovalHistory(tab as 'Approved' | 'Rejected', page, 50),
    enabled: tab === 'Approved' || tab === 'Rejected',
  })

  const actionMutation = useMutation({
    mutationFn: () =>
      acting!.action === 'approve' ? approveRequest(acting!.item.id, comments) : rejectRequest(acting!.item.id, comments),
    onSuccess: () => {
      notify(acting?.action === 'approve' ? 'Request approved' : 'Request rejected', 'success')
      setActing(null)
      setComments('')
      setPage(1)
      void queryClient.invalidateQueries({ queryKey: ['approvals'] })
    },
    onError: () => notify('Failed to submit decision', 'error'),
  })

  const pending = pendingQuery.data?.items ?? []
  const sortedPending = [...pending].sort((a, b) => {
    if (a.escalationLevel !== b.escalationLevel) return b.escalationLevel - a.escalationLevel
    const aDue = a.dueAt ? Date.parse(a.dueAt) : Infinity
    const bDue = b.dueAt ? Date.parse(b.dueAt) : Infinity
    return aDue - bDue
  })

  return (
    <Box>
      <PageHeader title="Confirmations" description="Requests waiting on a decision, sorted by escalation and due date." />

      <Tabs value={tab} onChange={(_, v) => { setTab(v); setPage(1) }} variant="scrollable" scrollButtons="auto" sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Tab label={`Pending${pendingQuery.data?.totalCount ? ` (${pendingQuery.data.totalCount})` : ''}`} value="Pending" />
        <Tab label="Approved" value="Approved" />
        <Tab label="Rejected" value="Rejected" />
      </Tabs>

      {tab === 'Pending' && (
        <Grid container spacing={2}>
          {pendingQuery.isLoading && <Grid size={12}><LoadingSkeleton count={3} /></Grid>}
          {pendingQuery.isError && <Grid size={12}><ErrorState title="Unable to load pending requests" description="Your requests are still safe. Try again to refresh the inbox." onRetry={() => void pendingQuery.refetch()} /></Grid>}
          {!pendingQuery.isLoading && !pendingQuery.isError && sortedPending.map((a) => (
            <Grid key={a.id} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                    <Typography variant="subtitle2" sx={{ flexGrow: 1 }} noWrap>
                      {approvalTitle(a)}
                    </Typography>
                    {a.escalationLevel > 0 && (
                      <Chip
                        label={a.escalationLevel >= 2 ? 'Escalated' : 'Overdue risk'}
                        size="small"
                        color={escalationColor(a.escalationLevel)}
                      />
                    )}
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    Requested by {requesterName(a)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                    {new Date(a.requestedAt).toLocaleString()}
                    {a.dueAt && ` · due ${new Date(a.dueAt).toLocaleString()}`}
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ mb: 1 }}>
                    <Chip label={`App: ${a.applicationName ?? '—'}`} size="small" variant="outlined" />
                  </Stack>
                  {(() => {
                    const summary = parseDataSummary(a.data)
                    if (!summary) return null
                    if (typeof summary === 'string') {
                      return (
                        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1, fontFamily: 'monospace' }} noWrap>
                          {summary}
                        </Typography>
                      )
                    }
                    return (
                      <Stack spacing={0.25} sx={{ mb: 1 }}>
                        {summary.slice(0, 4).map(([k, v]) => (
                          <Stack key={k} direction="row" spacing={1} sx={{ justifyContent: 'space-between' }}>
                            <Typography variant="caption" color="text.secondary">
                              {k}
                            </Typography>
                            <Typography variant="caption" noWrap sx={{ maxWidth: '60%' }}>
                              {v}
                            </Typography>
                          </Stack>
                        ))}
                        {summary.length > 4 && (
                          <Typography variant="caption" color="text.secondary">
                            +{summary.length - 4} more
                          </Typography>
                        )}
                      </Stack>
                    )
                  })()}
                  <Stack direction="row" spacing={1}>
                    <Button
                      size="small"
                      variant="contained"
                      onClick={() => setActing({ item: a, action: 'approve' })}
                    >
                      Approve
                    </Button>
                    <Button size="small" color="error" onClick={() => setActing({ item: a, action: 'reject' })}>
                      Reject
                    </Button>
                    {hasWorkflowRun(a) && (
                      <Button size="small" onClick={() => setViewingRun(toRunItem(a))}>
                        View Details
                      </Button>
                    )}
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          ))}
          {!pendingQuery.isLoading && !pendingQuery.isError && sortedPending.length === 0 && (
            <Grid size={12}>
              <EmptyState icon={InboxOutlined} title="Nothing pending" description="You're all caught up — no requests are waiting on a decision." />
            </Grid>
          )}
          {!pendingQuery.isLoading && !pendingQuery.isError && (pendingQuery.data?.totalCount ?? 0) > 50 && (
            <Grid size={12}>
              <Stack direction="row" sx={{ justifyContent: 'center', pt: 1 }}>
                <Pagination count={Math.ceil((pendingQuery.data?.totalCount ?? 0) / 50)} page={page} onChange={(_, nextPage) => setPage(nextPage)} color="primary" />
              </Stack>
            </Grid>
          )}
        </Grid>
      )}

      {(tab === 'Approved' || tab === 'Rejected') && (
        <Stack spacing={1}>
          {historyQuery.isLoading && <LoadingSkeleton variant="text" count={3} height={56} />}
          {historyQuery.isError && <ErrorState title={`Unable to load ${tab.toLowerCase()} requests`} onRetry={() => void historyQuery.refetch()} />}
          {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.items ?? []).map((h: ApprovalHistoryItem) => (
            <Card key={h.id} variant="outlined">
              <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 0.5, sm: 2 }} sx={{ alignItems: { xs: 'flex-start', sm: 'center' }, justifyContent: 'space-between' }}>
                  <Typography variant="body2">{historyTitle(h)}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {h.respondedAt ? new Date(h.respondedAt).toLocaleString() : '—'}
                  </Typography>
                </Stack>
                {h.comments && (
                  <Typography variant="caption" color="text.secondary">
                    "{h.comments}"
                  </Typography>
                )}
              </CardContent>
            </Card>
          ))}
          {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.items ?? []).length === 0 && (
            <EmptyState icon={InboxOutlined} title={`No ${tab.toLowerCase()} requests yet`} />
          )}
          {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.totalCount ?? 0) > 50 && (
            <Stack direction="row" sx={{ justifyContent: 'center', pt: 1 }}>
              <Pagination count={Math.ceil((historyQuery.data?.totalCount ?? 0) / 50)} page={page} onChange={(_, nextPage) => setPage(nextPage)} color="primary" />
            </Stack>
          )}
        </Stack>
      )}

      <Dialog open={!!acting} onClose={() => { if (!actionMutation.isPending) setActing(null) }} maxWidth="xs" fullWidth>
        <DialogTitle>{acting?.action === 'approve' ? 'Approve' : 'Reject'} Request</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {acting && <><strong>{approvalTitle(acting.item)}</strong><br />Requested by {requesterName(acting.item)}{acting.item.applicationName ? ` · ${acting.item.applicationName}` : ''}</>}
          </DialogContentText>
          <TextField
            label={acting?.action === 'reject' ? 'Reason (optional)' : 'Comments (optional)'}
            fullWidth
            multiline
            minRows={2}
            sx={{ mt: 1 }}
            value={comments}
            onChange={(e) => setComments(e.target.value)}
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setActing(null)} disabled={actionMutation.isPending}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color={acting?.action === 'reject' ? 'error' : 'primary'}
            onClick={() => actionMutation.mutate()}
            disabled={actionMutation.isPending}
          >
            {actionMutation.isPending ? 'Submitting…' : acting?.action === 'approve' ? 'Approve' : 'Reject'}
          </Button>
        </DialogActions>
      </Dialog>

      <RunInspectorDrawer run={viewingRun} onClose={() => setViewingRun(null)} />
    </Box>
  )
}
