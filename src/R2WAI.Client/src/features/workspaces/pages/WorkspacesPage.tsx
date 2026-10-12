import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Grid,
  IconButton,
  Menu,
  MenuItem,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import WorkspacesOutlined from '@mui/icons-material/WorkspacesOutlined'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { PageHeader } from '../../../components/PageHeader'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { FormDialog } from '../../../components/dialogs/FormDialog'
import { apiErrorMessage, describeApiError } from '../../../lib/api/fetchJson'
import { useAuth } from '../../../lib/auth/useAuth'
import { getPersona } from '../../../lib/nav/roleNav'
import { getNavIcon } from '../../../lib/nav/navIcons'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { setActiveWorkspaceId, useActiveWorkspaceId } from '../../../lib/workspace/activeWorkspace'
import { createWorkspace, deleteWorkspace, listWorkspaces, updateWorkspace } from '../api'
import type { WorkspaceDto, WorkspaceInput, WorkspaceResourceCounts } from '../types'

const QUERY_KEY = ['workspaces']

// Same order and icons as the rail, so a count reads as "this many of that destination".
const RESOURCES: { key: keyof Omit<WorkspaceResourceCounts, 'total'>; label: string; icon: string }[] = [
  { key: 'agents', label: 'Agents', icon: 'SmartToy' },
  { key: 'knowledge', label: 'Knowledge', icon: 'MenuBook' },
  { key: 'automations', label: 'Automations', icon: 'AccountTree' },
  { key: 'chatbots', label: 'Published', icon: 'Forum' },
  { key: 'connections', label: 'Connections', icon: 'Apps' },
]

type Editing = { mode: 'create' } | { mode: 'edit'; workspace: WorkspaceDto }

export function WorkspacesPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const { user } = useAuth()
  const canManage = getPersona(user) === 'Admin' || getPersona(user) === 'SuperAdmin'
  const navigate = useNavigate()
  const activeId = useActiveWorkspaceId()
  const [searchParams, setSearchParams] = useSearchParams()

  // The top-bar switcher's "New workspace" lands here with ?new=1.
  const [editing, setEditing] = useState<Editing | null>(() =>
    canManage && searchParams.get('new') === '1' ? { mode: 'create' } : null)
  const closeEditor = () => {
    setEditing(null)
    if (searchParams.has('new')) setSearchParams({}, { replace: true })
  }
  const [deleting, setDeleting] = useState<WorkspaceDto | null>(null)

  const query = useQuery({ queryKey: QUERY_KEY, queryFn: listWorkspaces })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: QUERY_KEY })

  const saveMutation = useMutation({
    mutationFn: (input: WorkspaceInput) =>
      editing?.mode === 'edit' ? updateWorkspace(editing.workspace.id, input) : createWorkspace(input),
    onSuccess: () => {
      notify(editing?.mode === 'edit' ? 'Workspace updated' : 'Workspace created', 'success')
      closeEditor()
      refresh()
    },
    onError: (error) => notify(apiErrorMessage(error, 'Failed to save workspace'), 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteWorkspace(id),
    onSuccess: () => {
      notify('Workspace deleted', 'success')
      setDeleting(null)
      refresh()
    },
    onError: (error) => {
      notify(apiErrorMessage(error, 'Failed to delete workspace'), 'error')
      setDeleting(null)
    },
  })

  const workspaces = query.data ?? []

  // Open = make it the active workspace and start from Home, as in Copilot Studio's environments.
  const openWorkspace = (workspace: WorkspaceDto) => {
    if (workspace.id !== activeId) {
      setActiveWorkspaceId(workspace.id)
      void queryClient.invalidateQueries()
      notify(`Switched to ${workspace.name}`, 'success')
    }
    navigate('/')
  }

  return (
    <Box>
      <PageHeader
        title="Workspaces"
        description="A workspace groups the agents, knowledge, automations, published chatbots and connections that belong to one team or use case."
        actions={canManage && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing({ mode: 'create' })}>
            New workspace
          </Button>
        )}
      />

      {query.isLoading ? (
        <LoadingSkeleton count={3} />
      ) : query.error ? (
        <ErrorState {...describeApiError(query.error, 'Unable to load workspaces.')} onRetry={() => void query.refetch()} />
      ) : workspaces.length === 0 ? (
        <EmptyState
          icon={WorkspacesOutlined}
          title="No workspaces yet"
          description="Create a workspace to group the resources one team or use case works with."
          actionLabel={canManage ? 'New workspace' : undefined}
          onAction={canManage ? () => setEditing({ mode: 'create' }) : undefined}
        />
      ) : (
        <Grid container spacing={2}>
          {workspaces.map((workspace) => (
            <Grid key={workspace.id} size={{ xs: 12, md: 6, xl: 4 }}>
              <WorkspaceCard
                workspace={workspace}
                canManage={canManage}
                isCurrent={workspace.id === activeId}
                onOpen={() => openWorkspace(workspace)}
                onEdit={() => setEditing({ mode: 'edit', workspace })}
                onDelete={() => setDeleting(workspace)}
              />
            </Grid>
          ))}
        </Grid>
      )}

      {editing && (
        <WorkspaceFormDialog
          initial={editing.mode === 'edit' ? editing.workspace : undefined}
          isSubmitting={saveMutation.isPending}
          onClose={closeEditor}
          onSubmit={(input) => saveMutation.mutate(input)}
        />
      )}

      <ConfirmDeleteDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={() => deleting && deleteMutation.mutate(deleting.id)}
        title="Delete workspace"
        message={`Delete "${deleting?.name ?? ''}"? Only an empty workspace can be deleted.`}
        isDeleting={deleteMutation.isPending}
      />
    </Box>
  )
}

interface WorkspaceCardProps {
  workspace: WorkspaceDto
  canManage: boolean
  /** The workspace the switcher currently has selected. */
  isCurrent: boolean
  onOpen: () => void
  onEdit: () => void
  onDelete: () => void
}

function WorkspaceCard({ workspace, canManage, isCurrent, onOpen, onEdit, onDelete }: WorkspaceCardProps) {
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const deleteBlockedReason = workspace.isDefault
    ? 'The default workspace cannot be deleted'
    : workspace.resources.total > 0
      ? 'Move or delete its contents first'
      : null

  return (
    <Card
      variant="outlined"
      sx={{ height: '100%', display: 'flex', flexDirection: 'column', borderColor: isCurrent ? 'primary.main' : undefined }}
    >
      <CardContent sx={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
          <Box
            sx={{
              display: 'grid',
              placeItems: 'center',
              width: 40,
              height: 40,
              borderRadius: 2,
              flexShrink: 0,
              color: 'primary.main',
              bgcolor: 'action.hover',
            }}
          >
            <WorkspacesOutlined fontSize="small" />
          </Box>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
              <Typography variant="subtitle1" noWrap title={workspace.name} sx={{ fontWeight: 650 }}>
                {workspace.name}
              </Typography>
              {workspace.isDefault && <Chip label="Default" size="small" variant="outlined" />}
              {isCurrent && <Chip label="Current" size="small" color="primary" />}
            </Stack>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
              {workspace.description || 'No description'}
            </Typography>
          </Box>
          {canManage && (
            <>
              <IconButton
                size="small"
                aria-label={`Actions for ${workspace.name}`}
                onClick={(event) => setMenuAnchor(event.currentTarget)}
              >
                <MoreVertIcon fontSize="small" />
              </IconButton>
              <Menu anchorEl={menuAnchor} open={menuAnchor !== null} onClose={() => setMenuAnchor(null)}>
                <MenuItem
                  onClick={() => {
                    setMenuAnchor(null)
                    onEdit()
                  }}
                >
                  Rename
                </MenuItem>
                <Tooltip title={deleteBlockedReason ?? ''} placement="left">
                  <span>
                    <MenuItem
                      disabled={deleteBlockedReason !== null}
                      sx={{ color: 'error.main' }}
                      onClick={() => {
                        setMenuAnchor(null)
                        onDelete()
                      }}
                    >
                      Delete
                    </MenuItem>
                  </span>
                </Tooltip>
              </Menu>
            </>
          )}
        </Stack>

        <Box
          component="dl"
          sx={{
            m: 0,
            mt: 'auto',
            display: 'grid',
            gridTemplateColumns: 'repeat(5, minmax(0, 1fr))',
            gap: 1,
            pt: 2,
            borderTop: 1,
            borderColor: 'divider',
          }}
        >
          {RESOURCES.map(({ key, label, icon }) => {
            const Icon = getNavIcon(icon)
            return (
              <Box key={key} sx={{ minWidth: 0 }}>
                <Typography component="dt" variant="caption" color="text.secondary" noWrap sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                  <Icon sx={{ fontSize: 14 }} aria-hidden />
                  {label}
                </Typography>
                <Typography component="dd" variant="h6" sx={{ m: 0, fontVariantNumeric: 'tabular-nums' }}>
                  {workspace.resources[key]}
                </Typography>
              </Box>
            )
          })}
        </Box>

        <Box>
          <Button size="small" variant={isCurrent ? 'outlined' : 'contained'} onClick={onOpen}>
            {isCurrent ? 'Go to Home' : 'Open'}
          </Button>
        </Box>
      </CardContent>
    </Card>
  )
}

interface WorkspaceFormDialogProps {
  initial?: WorkspaceDto
  isSubmitting: boolean
  onClose: () => void
  onSubmit: (input: WorkspaceInput) => void
}

function WorkspaceFormDialog({ initial, isSubmitting, onClose, onSubmit }: WorkspaceFormDialogProps) {
  const [name, setName] = useState(initial?.name ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [touched, setTouched] = useState(false)
  const nameError = touched && !name.trim() ? 'Name is required' : undefined

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (!name.trim()) return
    onSubmit({ name: name.trim(), description: description.trim() || null })
  }

  return (
    <FormDialog
      open
      onClose={onClose}
      title={initial ? 'Rename workspace' : 'New workspace'}
      onSubmit={submit}
      isSubmitting={isSubmitting}
      submitLabel={initial ? 'Save' : 'Create'}
    >
      <TextField
        label="Name"
        value={name}
        onChange={(event) => setName(event.target.value)}
        onBlur={() => setTouched(true)}
        error={Boolean(nameError)}
        helperText={nameError ?? 'For example: HR Services, Citizen Helpdesk'}
        slotProps={{ htmlInput: { maxLength: 200 } }}
        required
        autoFocus
      />
      <TextField
        label="Description"
        value={description}
        onChange={(event) => setDescription(event.target.value)}
        multiline
        minRows={2}
        slotProps={{ htmlInput: { maxLength: 2000 } }}
      />
    </FormDialog>
  )
}
