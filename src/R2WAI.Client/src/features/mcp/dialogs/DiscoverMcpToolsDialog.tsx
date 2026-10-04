import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material'
import { ApiRequestError } from '../../../lib/api/fetchJson'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { commitMcpTools, discoverMcpTools } from '../api'
import type { McpConnectionDto } from '../types'

interface DiscoverMcpToolsDialogProps {
  open: boolean
  onClose: () => void
  connection: McpConnectionDto | null
}

/**
 * Lists a connected MCP server's advertised tools for review, then commits the selected ones as
 * inactive ToolDefinition rows (McpConnectionsController.Discover/Commit) — same two-step
 * analyze-then-import shape as ImportOpenApiDialog, except nothing here is ever callable until an
 * admin separately activates it from Tools & APIs: an MCP server's tool list is server-controlled,
 * so discovery alone must not grant capability.
 */
export function DiscoverMcpToolsDialog({ open, onClose, connection }: DiscoverMcpToolsDialogProps) {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [selected, setSelected] = useState<Set<string>>(new Set())

  const discoverMutation = useMutation({
    mutationFn: () => discoverMcpTools(connection!.id),
  })

  useEffect(() => {
    if (open && connection) discoverMutation.mutate()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, connection?.id])

  const commitMutation = useMutation({
    mutationFn: () => {
      const tools = (discoverMutation.data ?? []).filter((t) => selected.has(t.name))
      return commitMcpTools(connection!.id, tools)
    },
    onSuccess: (result) => {
      notify(
        `Committed ${result.ids.length} tool${result.ids.length === 1 ? '' : 's'} — review and activate them in Tools & APIs`,
        'success',
      )
      void queryClient.invalidateQueries({ queryKey: ['capabilities'] })
      handleClose()
    },
    onError: () => notify('Failed to commit selected tools', 'error'),
  })

  function handleClose() {
    setSelected(new Set())
    discoverMutation.reset()
    commitMutation.reset()
    onClose()
  }

  function toggle(name: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Discover Tools — {connection?.name}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          {discoverMutation.isPending && <Typography variant="body2">Contacting the server…</Typography>}
          {discoverMutation.isError && (
            <Alert severity="error">
              {discoverMutation.error instanceof ApiRequestError
                ? discoverMutation.error.message
                : 'Failed to reach this MCP server.'}
            </Alert>
          )}
          {discoverMutation.data && discoverMutation.data.length === 0 && (
            <Alert severity="info">This server advertised no tools.</Alert>
          )}
          {discoverMutation.data && discoverMutation.data.length > 0 && (
            <>
              <Typography variant="subtitle2">
                {discoverMutation.data.length} tool{discoverMutation.data.length === 1 ? '' : 's'} found — committed
                tools stay inactive until reviewed
              </Typography>
              <List dense>
                {discoverMutation.data.map((tool) => (
                  <ListItem key={tool.name} onClick={() => toggle(tool.name)} sx={{ cursor: 'pointer' }}>
                    <ListItemIcon>
                      <Checkbox edge="start" checked={selected.has(tool.name)} tabIndex={-1} disableRipple />
                    </ListItemIcon>
                    <ListItemText primary={tool.name} secondary={tool.description ?? 'No description'} />
                  </ListItem>
                ))}
              </List>
            </>
          )}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose}>Cancel</Button>
        <Button
          variant="contained"
          disabled={selected.size === 0 || commitMutation.isPending}
          onClick={() => commitMutation.mutate()}
        >
          {commitMutation.isPending ? 'Committing…' : `Commit ${selected.size || ''} Tool${selected.size === 1 ? '' : 's'}`}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
