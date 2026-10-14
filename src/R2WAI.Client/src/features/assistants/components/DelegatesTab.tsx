import { queryKeys } from '../../../lib/api/queryKeys'
import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Checkbox,
  Chip,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { listAssistants, updateAssistant } from '../api'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { EmptyState } from '../../../components/EmptyState'
import { buildFullUpdatePayload, type AssistantDto } from '../types'
import { parseEnabledIds } from '../toolsJson'

/**
 * Real multi-agent delegation (see AgentDelegationPlugin's own doc comment): which other agents
 * this one may hand a task off to via delegate_to_agent/delegate_to_agents. Unlike Tools, an empty
 * selection means NO delegation at all — there is no "every assistant" default for a capability
 * this new and this privileged.
 */
export function DelegatesTab({ assistantId, delegatableAssistantIds }: { assistantId: string; delegatableAssistantIds: string | null }) {
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [search, setSearch] = useState('')

  const delegateIds = useMemo(() => parseEnabledIds(delegatableAssistantIds), [delegatableAssistantIds])

  // Published only: an unpublished draft isn't a safe or stable delegation target yet, and
  // excludes this assistant itself — delegating to yourself isn't a real capability.
  const assistantsQuery = useQuery({
    queryKey: ['assistants', 'delegatable-candidates'],
    queryFn: () => listAssistants(1, 200, '', 'Published'),
  })

  const mutation = useMutation({
    mutationFn: (nextIds: string[]) => {
      const current = queryClient.getQueryData<AssistantDto>(queryKeys.assistants.detail(assistantId))
      if (!current) throw new Error('Assistant not loaded')
      return updateAssistant(assistantId, buildFullUpdatePayload(current, { delegatableAssistantIds: JSON.stringify(nextIds) }))
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(assistantId) })
    },
    onError: () => notify('Failed to update delegates', 'error'),
  })

  const candidates = (assistantsQuery.data?.items ?? []).filter((a) => a.id !== assistantId)
  const delegates = candidates.filter((a) => delegateIds.includes(a.id))
  const filtered = candidates.filter((a) => a.name.toLowerCase().includes(search.toLowerCase()))

  function toggle(id: string) {
    const next = delegateIds.includes(id) ? delegateIds.filter((x) => x !== id) : [...delegateIds, id]
    mutation.mutate(next)
  }

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="body2" color="text.secondary">
          Other agents this one may hand a task off to.
        </Typography>
        <Button size="small" startIcon={<AddIcon />} onClick={() => setDrawerOpen(true)}>
          Add Delegate
        </Button>
      </Stack>

      {delegates.length === 0 ? (
        <EmptyState title="No delegates yet" description="This agent can only answer directly — it won't hand any part of a request off to another agent." />
      ) : (
        <Stack spacing={1}>
          {delegates.map((a) => (
            <Stack
              key={a.id}
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center', p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}
            >
              <Box sx={{ flexGrow: 1 }}>
                <Typography variant="body2">{a.name}</Typography>
                {a.description && (
                  <Typography variant="caption" color="text.secondary">
                    {a.description}
                  </Typography>
                )}
              </Box>
              <Chip label={a.type} size="small" variant="outlined" />
              <Button size="small" onClick={() => toggle(a.id)}>
                Remove
              </Button>
            </Stack>
          ))}
        </Stack>
      )}

      <Drawer anchor="right" open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        <Box sx={{ width: 360, p: 2 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
            Add Delegate
          </Typography>
          <TextField
            fullWidth
            size="small"
            placeholder="Search published agents…"
            slotProps={{ htmlInput: { 'aria-label': 'Search agents' } }}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            sx={{ mb: 1 }}
          />
          <List dense>
            {filtered.map((a) => (
              <ListItemButton key={a.id} onClick={() => toggle(a.id)}>
                <ListItemIcon>
                  <Checkbox edge="start" checked={delegateIds.includes(a.id)} tabIndex={-1} disableRipple />
                </ListItemIcon>
                <ListItemText primary={a.name} secondary={a.description} />
              </ListItemButton>
            ))}
            {filtered.length === 0 && (
              <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
                No published agents found.
              </Typography>
            )}
          </List>
        </Box>
      </Drawer>
    </Stack>
  )
}
