import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Chip, CircularProgress, IconButton, List, ListItem, ListItemText, Stack, Switch, Tooltip, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import EditIcon from '@mui/icons-material/Edit'
import TravelExploreIcon from '@mui/icons-material/TravelExplore'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import ErrorIcon from '@mui/icons-material/Error'
import HelpOutlineIcon from '@mui/icons-material/HelpOutlineOutlined'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { PageHeader } from '../../../components/PageHeader'
import { TooltipIconButton } from '../../../components/TooltipIconButton'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createMcpConnection, deleteMcpConnection, listMcpConnections, testMcpConnection, toggleMcpConnection, updateMcpConnection } from '../api'
import { CreateEditMcpConnectionDialog } from '../dialogs/CreateEditMcpConnectionDialog'
import { DiscoverMcpToolsDialog } from '../dialogs/DiscoverMcpToolsDialog'
import type { McpConnectionDto, McpConnectionInput } from '../types'

const QUERY_KEY = 'mcp-connections'

function StatusChip({ connection }: { connection: McpConnectionDto }) {
  if (!connection.isActive) return <Chip size="small" label="Disabled" variant="outlined" />
  if (connection.lastTestStatus === 'Connected') return <Chip size="small" color="success" icon={<CheckCircleIcon />} label="Connected" />
  if (connection.lastTestStatus === 'Error') return <Chip size="small" color="error" icon={<ErrorIcon />} label="Error" />
  return <Chip size="small" variant="outlined" icon={<HelpOutlineIcon />} label="Not tested" />
}

export function McpConnectionsPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<McpConnectionDto | null>(null)
  const [deleting, setDeleting] = useState<McpConnectionDto | null>(null)
  const [discovering, setDiscovering] = useState<McpConnectionDto | null>(null)

  const query = useQuery({ queryKey: [QUERY_KEY], queryFn: listMcpConnections })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })

  const createMutation = useMutation({
    mutationFn: (input: McpConnectionInput) => createMcpConnection(input),
    onSuccess: () => {
      notify('MCP server connected', 'success')
      setDialogOpen(false)
      void invalidate()
    },
    onError: () => notify('Failed to connect MCP server', 'error'),
  })

  const updateMutation = useMutation({
    mutationFn: (args: { id: string; input: McpConnectionInput }) => updateMcpConnection(args.id, args.input),
    onSuccess: () => {
      notify('MCP server updated', 'success')
      setDialogOpen(false)
      setEditing(null)
      void invalidate()
    },
    onError: () => notify('Failed to update MCP server', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteMcpConnection(id),
    onSuccess: () => {
      notify('MCP server removed', 'success')
      setDeleting(null)
      void invalidate()
    },
    onError: () => notify('Failed to remove MCP server', 'error'),
  })

  const toggleMutation = useMutation({
    mutationFn: (id: string) => toggleMcpConnection(id),
    onSuccess: () => void invalidate(),
    onError: () => notify('Failed to update status', 'error'),
  })

  const testMutation = useMutation({
    mutationFn: (id: string) => testMcpConnection(id),
    onSuccess: (result) => {
      notify(result.message, result.success ? 'success' : 'error')
      void invalidate()
    },
    onError: (error) => notify(error instanceof Error ? error.message : 'Connection test failed', 'error'),
  })

  const connections = query.data ?? []

  return (
    <Box>
      <PageHeader
        title="MCP Servers"
        description="Connect Model Context Protocol servers and review their advertised tools before anything becomes agent-callable."
        actions={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => {
              setEditing(null)
              setDialogOpen(true)
            }}
          >
            Connect MCP Server
          </Button>
        }
      />

      {query.isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      )}

      {query.isError && (
        <ErrorState title="Unable to load MCP servers" description="Your saved connections could not be retrieved." onRetry={() => void query.refetch()} />
      )}

      {query.isSuccess && connections.length === 0 && (
        <EmptyState title="No MCP servers connected yet" description="Connect one to discover the tools it advertises." />
      )}

      {query.isSuccess && connections.length > 0 && (
        <List>
          {connections.map((c) => (
            <ListItem
              key={c.id}
              divider
              secondaryAction={
                <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                  <Switch
                    checked={c.isActive}
                    onChange={() => toggleMutation.mutate(c.id)}
                    disabled={toggleMutation.isPending}
                  />
                  <Tooltip title="Test connection">
                    <span>
                      <Button
                        size="small"
                        onClick={() => testMutation.mutate(c.id)}
                        disabled={testMutation.isPending}
                      >
                        Test
                      </Button>
                    </span>
                  </Tooltip>
                  <Tooltip title="Discover tools">
                    <span>
                      <IconButton size="small" aria-label="Discover tools" onClick={() => setDiscovering(c)}>
                        <TravelExploreIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <TooltipIconButton
                    size="small"
                    aria-label="Edit connection"
                    onClick={() => {
                      setEditing(c)
                      setDialogOpen(true)
                    }}
                  >
                    <EditIcon fontSize="small" />
                  </TooltipIconButton>
                  <TooltipIconButton size="small" aria-label="Delete connection" onClick={() => setDeleting(c)}>
                    <DeleteIcon fontSize="small" />
                  </TooltipIconButton>
                </Stack>
              }
            >
              <ListItemText
                primary={
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                    <span>{c.name}</span>
                    <StatusChip connection={c} />
                  </Stack>
                }
                secondary={
                  <Typography variant="body2" color="text.secondary" component="span">
                    {c.endpointUrl}
                    {c.lastTestedAt && ` · tested ${new Date(c.lastTestedAt).toLocaleString()}`}
                  </Typography>
                }
              />
            </ListItem>
          ))}
        </List>
      )}

      <CreateEditMcpConnectionDialog
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false)
          setEditing(null)
        }}
        onSubmit={(values) => {
          if (editing) updateMutation.mutate({ id: editing.id, input: values })
          else createMutation.mutate(values)
        }}
        connection={editing}
        isSubmitting={createMutation.isPending || updateMutation.isPending}
      />

      <DiscoverMcpToolsDialog open={!!discovering} onClose={() => setDiscovering(null)} connection={discovering} />

      <ConfirmDeleteDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Remove MCP Server"
        message={`Remove "${deleting?.name}"? Tools already committed from it are not deleted. This cannot be undone.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}
