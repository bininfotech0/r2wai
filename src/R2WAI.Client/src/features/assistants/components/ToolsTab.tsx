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
import { fetchJson } from '../../../lib/api/fetchJson'
import { updateAssistant } from '../api'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { CapabilityFlowDiagram } from '../../../components/CapabilityFlowDiagram'
import { EmptyState } from '../../../components/EmptyState'
import { buildFullUpdatePayload, type AssistantDto } from '../types'
import type { CapabilityDto, PagedResult } from '../../tools/types'

function parseEnabledIds(toolsJson: string | null): string[] {
  if (!toolsJson) return []
  try {
    const parsed = JSON.parse(toolsJson) as unknown
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === 'string') : []
  } catch {
    return []
  }
}

/**
 * What can this assistant do? Enabled tools are stored as a JSON array of
 * capability ids on AssistantDefinition.Tools. The "+Add Tool" drawer is the
 * Tool Gateway's user-facing surface — CapabilityFlowDiagram (also on Home)
 * explains the pipeline those calls actually go through server-side.
 */
export function ToolsTab({ assistantId, toolsJson }: { assistantId: string; toolsJson: string | null }) {
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [search, setSearch] = useState('')

  const enabledIds = useMemo(() => parseEnabledIds(toolsJson), [toolsJson])

  const capabilitiesQuery = useQuery({
    queryKey: ['capabilities', 'all'],
    queryFn: () => fetchJson<PagedResult<CapabilityDto>>('/capabilities?page=1&pageSize=200'),
  })

  // Reads the currently-cached assistant to build a full (not partial)
  // update payload — see buildFullUpdatePayload's doc comment.
  const mutation = useMutation({
    mutationFn: (nextIds: string[]) => {
      const current = queryClient.getQueryData<AssistantDto>(queryKeys.assistants.detail(assistantId))
      if (!current) throw new Error('Assistant not loaded')
      return updateAssistant(assistantId, buildFullUpdatePayload(current, { tools: JSON.stringify(nextIds) }))
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(assistantId) })
    },
    onError: () => notify('Failed to update tools', 'error'),
  })

  const allCapabilities = capabilitiesQuery.data?.items ?? []
  const enabledCapabilities = allCapabilities.filter((c) => enabledIds.includes(c.id))
  const filtered = allCapabilities.filter((c) => c.name.toLowerCase().includes(search.toLowerCase()))

  function toggle(id: string) {
    const next = enabledIds.includes(id) ? enabledIds.filter((x) => x !== id) : [...enabledIds, id]
    mutation.mutate(next)
  }

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="body2" color="text.secondary">
          What can this assistant do?
        </Typography>
        <Button size="small" startIcon={<AddIcon />} onClick={() => setDrawerOpen(true)}>
          Add Tool
        </Button>
      </Stack>

      {enabledCapabilities.length === 0 ? (
        <EmptyState title="No tools enabled yet" />
      ) : (
        <Stack spacing={1}>
          {enabledCapabilities.map((cap) => (
            <Stack
              key={cap.id}
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center', p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}
            >
              <Box sx={{ flexGrow: 1 }}>
                <Typography variant="body2">{cap.name}</Typography>
                {cap.description && (
                  <Typography variant="caption" color="text.secondary">
                    {cap.description}
                  </Typography>
                )}
              </Box>
              <Chip label={cap.riskLevel} size="small" variant="outlined" />
              <Chip label={cap.confirmationRequired ? 'Confirmation required' : 'No confirmation'} size="small" variant="outlined" />
              <Chip label={cap.isActive ? 'Active' : 'Disabled'} size="small" color={cap.isActive ? 'success' : 'default'} />
              <Button size="small" onClick={() => toggle(cap.id)}>
                Remove
              </Button>
            </Stack>
          ))}
        </Stack>
      )}

      <CapabilityFlowDiagram />

      <Drawer anchor="right" open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        <Box sx={{ width: 360, p: 2 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
            Add Tool
          </Typography>
          <TextField
            fullWidth
            size="small"
            placeholder="Search tools…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            sx={{ mb: 1 }}
          />
          <List dense>
            {filtered.map((cap) => (
              <ListItemButton key={cap.id} onClick={() => toggle(cap.id)}>
                <ListItemIcon>
                  <Checkbox edge="start" checked={enabledIds.includes(cap.id)} tabIndex={-1} disableRipple />
                </ListItemIcon>
                <ListItemText primary={cap.name} secondary={cap.description} />
              </ListItemButton>
            ))}
            {filtered.length === 0 && (
              <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
                No tools found.
              </Typography>
            )}
          </List>
        </Box>
      </Drawer>
    </Stack>
  )
}
