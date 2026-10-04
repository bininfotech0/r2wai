import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link as RouterLink } from 'react-router-dom'
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  Grid,
  MenuItem,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'
import BuildOutlined from '@mui/icons-material/BuildOutlined'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import { StatCard } from '../../../components/StatCard'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { LivePreviewPane } from '../../assistants/components/LivePreviewPane'
import { listCapabilities } from '../../tools/api'
import { listKnowledgeBases } from '../../knowledge/api'
import { listWorkflows } from '../../automations/api'
import { deleteBusinessCapability, getBusinessCapability, setBusinessCapabilityStatus, updateBusinessCapability } from '../api'
import { BUSINESS_CAPABILITY_STATUSES } from '../types'
import { ResourcePicker } from './ResourcePicker'

const TABS = ['Overview', 'Tools', 'Knowledge', 'Workflow', 'Security', 'Test'] as const

interface CapabilityDetailDialogProps {
  capabilityId: string | null
  assistantId: string
  onClose: () => void
}

/**
 * The Capability's own sub-tab structure (brief §6). "APIs & Data" is deliberately not a tab here
 * yet — the client has no listing endpoint for ApplicationApi rows independent of Tools/Integrations
 * (they're the same ToolDefinition table under different DTOs, per tools/types.ts's own comment), so
 * a picker for it would have nothing real to list. ApplicationApiIds stays on the DTO for when that
 * exists; only the three resource types with a real, independent list (Tools, Knowledge, Workflow)
 * get a picker tab. "Rules"/"Versions" from the brief are deferred too (no backend concept yet).
 */
export function CapabilityDetailDialog({ capabilityId, assistantId, onClose }: CapabilityDetailDialogProps) {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [tab, setTab] = useState<(typeof TABS)[number]>('Overview')
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')

  const query = useQuery({
    queryKey: ['business-capabilities', capabilityId],
    queryFn: () => getBusinessCapability(capabilityId!),
    enabled: !!capabilityId,
  })
  const capability = query.data

  useEffect(() => {
    if (!capability) return
    setName(capability.name)
    setDescription(capability.description ?? '')
  }, [capability?.id])

  useEffect(() => {
    if (capabilityId) setTab('Overview')
  }, [capabilityId])

  const toolsQuery = useQuery({
    queryKey: ['capabilities', 'all'],
    queryFn: () => listCapabilities(1, 200, ''),
    enabled: !!capability,
  })
  const knowledgeQuery = useQuery({
    queryKey: ['knowledgebases', 'all'],
    queryFn: () => listKnowledgeBases(1, 100, ''),
    enabled: !!capability,
  })
  const workflowsQuery = useQuery({
    queryKey: ['workflows', 'all'],
    queryFn: () => listWorkflows(1, 100, ''),
    enabled: !!capability,
  })

  function invalidate() {
    void queryClient.invalidateQueries({ queryKey: ['business-capabilities'] })
  }

  const saveDetailsMutation = useMutation({
    mutationFn: () =>
      updateBusinessCapability(capability!.id, {
        assistantId,
        name,
        description: description || undefined,
        icon: capability!.icon ?? undefined,
        toolIds: capability!.toolIds,
        knowledgeBaseIds: capability!.knowledgeBaseIds,
        workflowIds: capability!.workflowIds,
        applicationApiIds: capability!.applicationApiIds,
      }),
    onSuccess: () => {
      notify('Saved', 'success')
      invalidate()
    },
    onError: () => notify('Failed to save', 'error'),
  })

  const statusMutation = useMutation({
    mutationFn: (status: string) => setBusinessCapabilityStatus(capability!.id, status),
    onSuccess: () => {
      notify('Status updated', 'success')
      invalidate()
    },
    onError: () => notify('Failed to update status', 'error'),
  })

  const resourcesMutation = useMutation({
    mutationFn: (next: { toolIds: string[]; knowledgeBaseIds: string[]; workflowIds: string[] }) =>
      updateBusinessCapability(capability!.id, {
        assistantId,
        name: capability!.name,
        description: capability!.description ?? undefined,
        icon: capability!.icon ?? undefined,
        applicationApiIds: capability!.applicationApiIds,
        ...next,
      }),
    onSuccess: () => invalidate(),
    onError: () => notify('Failed to update', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: () => deleteBusinessCapability(capability!.id),
    onSuccess: () => {
      notify('Capability deleted', 'success')
      setDeleteOpen(false)
      invalidate()
      onClose()
    },
    onError: () => notify('Failed to delete', 'error'),
  })

  function toggleTool(id: string) {
    if (!capability) return
    const next = capability.toolIds.includes(id) ? capability.toolIds.filter((x) => x !== id) : [...capability.toolIds, id]
    resourcesMutation.mutate({ toolIds: next, knowledgeBaseIds: capability.knowledgeBaseIds, workflowIds: capability.workflowIds })
  }
  function toggleKnowledge(id: string) {
    if (!capability) return
    const next = capability.knowledgeBaseIds.includes(id)
      ? capability.knowledgeBaseIds.filter((x) => x !== id)
      : [...capability.knowledgeBaseIds, id]
    resourcesMutation.mutate({ toolIds: capability.toolIds, knowledgeBaseIds: next, workflowIds: capability.workflowIds })
  }
  function toggleWorkflow(id: string) {
    if (!capability) return
    const next = capability.workflowIds.includes(id) ? capability.workflowIds.filter((x) => x !== id) : [...capability.workflowIds, id]
    resourcesMutation.mutate({ toolIds: capability.toolIds, knowledgeBaseIds: capability.knowledgeBaseIds, workflowIds: next })
  }

  return (
    <Dialog open={!!capabilityId} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        {capability?.name ?? 'Capability'}
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        {!capability ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Box>
            <Tabs value={tab} onChange={(_, v: (typeof TABS)[number]) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
              {TABS.map((t) => (
                <Tab key={t} label={t} value={t} />
              ))}
            </Tabs>

            {tab === 'Overview' && (
              <Stack spacing={2}>
                <Grid container spacing={1.5}>
                  <Grid size={4}>
                    <StatCard label="Tools" value={capability.toolCount} icon={BuildOutlined} color="primary" />
                  </Grid>
                  <Grid size={4}>
                    <StatCard
                      label="Knowledge"
                      value={capability.knowledgeBaseCount}
                      icon={MenuBookOutlined}
                      color="secondary"
                    />
                  </Grid>
                  <Grid size={4}>
                    <StatCard
                      label="Workflows"
                      value={capability.workflowCount}
                      icon={AccountTreeOutlined}
                      color="info"
                    />
                  </Grid>
                </Grid>
                <TextField label="Name" fullWidth value={name} onChange={(e) => setName(e.target.value)} />
                <TextField
                  label="Description"
                  fullWidth
                  multiline
                  minRows={2}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
                <TextField
                  select
                  label="Status"
                  value={capability.status}
                  onChange={(e) => statusMutation.mutate(e.target.value)}
                  sx={{ maxWidth: 220 }}
                >
                  {BUSINESS_CAPABILITY_STATUSES.map((s) => (
                    <MenuItem key={s} value={s}>
                      {s}
                    </MenuItem>
                  ))}
                </TextField>
                <Stack direction="row" spacing={1}>
                  <Button
                    variant="contained"
                    disabled={saveDetailsMutation.isPending || !name.trim()}
                    onClick={() => saveDetailsMutation.mutate()}
                  >
                    {saveDetailsMutation.isPending ? 'Saving…' : 'Save changes'}
                  </Button>
                  <Button color="error" onClick={() => setDeleteOpen(true)}>
                    Delete
                  </Button>
                </Stack>
              </Stack>
            )}

            {tab === 'Tools' && (
              <ResourcePicker
                items={(toolsQuery.data?.items ?? []).map((t) => ({ id: t.id, name: t.name, description: t.description }))}
                selectedIds={capability.toolIds}
                onToggle={toggleTool}
                searchPlaceholder="Search tools…"
                emptyMessage="No tools configured yet — add one under Tools & APIs."
              />
            )}

            {tab === 'Knowledge' && (
              <ResourcePicker
                items={(knowledgeQuery.data?.items ?? []).map((k) => ({ id: k.id, name: k.name, description: k.description }))}
                selectedIds={capability.knowledgeBaseIds}
                onToggle={toggleKnowledge}
                searchPlaceholder="Search knowledge bases…"
                emptyMessage="No knowledge bases yet — add one under Knowledge."
              />
            )}

            {tab === 'Workflow' && (
              <ResourcePicker
                items={(workflowsQuery.data?.items ?? []).map((w) => ({ id: w.id, name: w.name, description: w.description }))}
                selectedIds={capability.workflowIds}
                onToggle={toggleWorkflow}
                searchPlaceholder="Search automations…"
                emptyMessage="No automations yet — create one under Automations."
              />
            )}

            {tab === 'Security' && (
              <Stack spacing={2}>
                <Typography variant="body2" color="text.secondary">
                  Capability-level security policy doesn't exist separately from the platform's
                  tenant-wide policies — every tool this capability links to is still governed by
                  the same Tool Gateway authorization and audit rules as anywhere else it's used.
                </Typography>
                <Button component={RouterLink} to="/security" variant="outlined" size="small" sx={{ alignSelf: 'flex-start' }}>
                  Open Security & Policies
                </Button>
              </Stack>
            )}

            {tab === 'Test' && (
              <Box sx={{ height: 420, display: 'flex', flexDirection: 'column' }}>
                <Typography variant="caption" color="text.secondary" sx={{ mb: 1 }}>
                  Capabilities don't run in isolation — this tests the assistant this capability
                  belongs to, using whichever tools/knowledge/workflows are currently enabled.
                </Typography>
                <LivePreviewPane assistantId={assistantId} />
              </Box>
            )}
          </Box>
        )}
      </DialogContent>

      <ConfirmDeleteDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={() => deleteMutation.mutate()}
        title="Delete Capability"
        message={`Delete "${capability?.name}"? This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Dialog>
  )
}
