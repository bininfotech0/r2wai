import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Chip, List, ListItem, ListItemText, Stack, Switch, Typography } from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import RefreshIcon from '@mui/icons-material/Refresh'
import {
  createApiKey,
  createWebhook,
  deleteApiKey,
  deleteWebhook,
  listApiKeys,
  listWebhooks,
  regenerateApiKey,
  toggleApiKey,
  toggleWebhook,
} from '../../admin/api'
import { CreateApiKeyDialog } from '../../admin/dialogs/CreateApiKeyDialog'
import { CreateEditWebhookDialog } from '../../admin/dialogs/CreateEditWebhookDialog'
import { RevealKeyDialog } from '../../admin/dialogs/RevealKeyDialog'
import { ConfirmDialog } from '../../../components/dialogs/ConfirmDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'

/** What a pending destructive action is aimed at. */
type DeletionTarget =
  | { kind: 'key'; id: string; name: string }
  | { kind: 'webhook'; id: string; name: string; url: string }

export function DeveloperTab() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [keyDialogOpen, setKeyDialogOpen] = useState(false)
  const [revealedKey, setRevealedKey] = useState<string | null>(null)
  const [webhookDialogOpen, setWebhookDialogOpen] = useState(false)
  // Deletes are staged rather than fired straight from the trash icon: revoking a live API
  // key breaks callers immediately and silently, and there is no undo or re-reveal — the
  // full secret is only ever returned once, at creation or regeneration.
  const [deletion, setDeletion] = useState<DeletionTarget | null>(null)

  const keysQuery = useQuery({ queryKey: ['settings-api-keys'], queryFn: () => listApiKeys(1, 50) })
  const webhooksQuery = useQuery({ queryKey: ['settings-webhooks'], queryFn: () => listWebhooks(1, 50) })

  const createKeyMutation = useMutation({
    mutationFn: createApiKey,
    onSuccess: (result) => {
      setKeyDialogOpen(false)
      setRevealedKey(result.key)
      void queryClient.invalidateQueries({ queryKey: ['settings-api-keys'] })
    },
    onError: () => notify('Failed to create API key', 'error'),
  })
  const toggleKeyMutation = useMutation({
    mutationFn: toggleApiKey,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings-api-keys'] }),
    onError: () => notify('Could not change this API key. It may have been deleted.', 'error'),
  })
  const regenerateKeyMutation = useMutation({
    mutationFn: regenerateApiKey,
    onSuccess: (result) => {
      setRevealedKey(result.key)
      void queryClient.invalidateQueries({ queryKey: ['settings-api-keys'] })
    },
    onError: () => notify('Could not regenerate this key. The old key is unchanged.', 'error'),
  })
  const deleteKeyMutation = useMutation({
    mutationFn: deleteApiKey,
    onSuccess: () => {
      notify('API key deleted', 'success')
      void queryClient.invalidateQueries({ queryKey: ['settings-api-keys'] })
    },
    onError: () => notify('Could not delete this API key', 'error'),
  })

  const createWebhookMutation = useMutation({
    mutationFn: createWebhook,
    onSuccess: () => {
      notify('Webhook created', 'success')
      setWebhookDialogOpen(false)
      void queryClient.invalidateQueries({ queryKey: ['settings-webhooks'] })
    },
    onError: () => notify('Failed to create webhook', 'error'),
  })
  const toggleWebhookMutation = useMutation({
    mutationFn: toggleWebhook,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings-webhooks'] }),
    onError: () => notify('Could not change this webhook', 'error'),
  })
  const deleteWebhookMutation = useMutation({
    mutationFn: deleteWebhook,
    onSuccess: () => {
      notify('Webhook deleted', 'success')
      void queryClient.invalidateQueries({ queryKey: ['settings-webhooks'] })
    },
    onError: () => notify('Could not delete this webhook', 'error'),
  })

  return (
    <Stack spacing={3} sx={{ maxWidth: 700 }}>
      <Box>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            API Keys
          </Typography>
          <Button size="small" startIcon={<AddIcon />} onClick={() => setKeyDialogOpen(true)}>
            New Key
          </Button>
        </Stack>
        {keysQuery.isLoading ? (
          <LoadingSkeleton variant="text" count={2} height={48} />
        ) : keysQuery.isError ? (
          <ErrorState title="Unable to load API keys" onRetry={() => void keysQuery.refetch()} />
        ) : (keysQuery.data?.items ?? []).length === 0 ? (
          <EmptyState title="No API keys yet" />
        ) : (
        <List dense>
          {(keysQuery.data?.items ?? []).map((k) => (
            <ListItem
              key={k.id}
              divider
              secondaryAction={
                <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                  <Switch size="small" checked={k.isActive} onChange={() => toggleKeyMutation.mutate(k.id)} />
                  <IconButton size="small" aria-label="Regenerate" onClick={() => regenerateKeyMutation.mutate(k.id)}>
                    <RefreshIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" aria-label={`Delete ${k.name}`} onClick={() => setDeletion({ kind: 'key', id: k.id, name: k.name })}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Stack>
              }
            >
              <ListItemText primary={k.name} secondary={`${k.keyPrefix}… · ${k.lastUsedAt ? `last used ${new Date(k.lastUsedAt).toLocaleDateString()}` : 'never used'}`} />
            </ListItem>
          ))}
        </List>
        )}
      </Box>

      <Box>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            Webhooks
          </Typography>
          <Button size="small" startIcon={<AddIcon />} onClick={() => setWebhookDialogOpen(true)}>
            New Webhook
          </Button>
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          Inbound only — external systems POST to the endpoint URL to trigger an automation.
        </Typography>
        {webhooksQuery.isLoading ? (
          <LoadingSkeleton variant="text" count={2} height={48} />
        ) : webhooksQuery.isError ? (
          <ErrorState title="Unable to load webhooks" onRetry={() => void webhooksQuery.refetch()} />
        ) : (webhooksQuery.data?.items ?? []).length === 0 ? (
          <EmptyState title="No webhooks yet" />
        ) : (
        <List dense>
          {(webhooksQuery.data?.items ?? []).map((w) => (
            <ListItem
              key={w.id}
              divider
              secondaryAction={
                <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                  <Switch size="small" checked={w.isActive} onChange={() => toggleWebhookMutation.mutate(w.id)} />
                  <IconButton
                    size="small"
                    aria-label={`Delete ${w.name}`}
                    onClick={() => setDeletion({ kind: 'webhook', id: w.id, name: w.name, url: w.endpointUrl })}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Stack>
              }
            >
              <ListItemText
                primary={
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                    <span>{w.name}</span>
                    <Chip label={w.triggerType} size="small" variant="outlined" />
                  </Stack>
                }
                secondary={`${w.endpointUrl} · ${w.totalCalls} calls`}
              />
            </ListItem>
          ))}
        </List>
        )}
      </Box>

      <CreateApiKeyDialog
        open={keyDialogOpen}
        onClose={() => setKeyDialogOpen(false)}
        onSubmit={(values) => createKeyMutation.mutate(values)}
        isSubmitting={createKeyMutation.isPending}
      />
      <RevealKeyDialog open={!!revealedKey} onClose={() => setRevealedKey(null)} apiKey={revealedKey} />
      <CreateEditWebhookDialog
        open={webhookDialogOpen}
        onClose={() => setWebhookDialogOpen(false)}
        onSubmit={(values) => createWebhookMutation.mutate(values)}
        isSubmitting={createWebhookMutation.isPending}
      />

      <ConfirmDialog
        open={deletion?.kind === 'key'}
        title="Revoke this API key?"
        message={
          deletion?.kind === 'key'
            ? `Any caller still using “${deletion.name}” will start receiving 401s immediately. The full secret cannot be shown again — use Regenerate to roll it, or create a new key instead.`
            : ''
        }
        confirmLabel="Revoke key"
        destructive
        busy={deleteKeyMutation.isPending}
        onClose={() => setDeletion(null)}
        onConfirm={() => {
          if (deletion?.kind === 'key') deleteKeyMutation.mutate(deletion.id)
          setDeletion(null)
        }}
      />

      <ConfirmDialog
        open={deletion?.kind === 'webhook'}
        title="Delete this webhook?"
        message={
          deletion?.kind === 'webhook'
            ? `Requests to ${deletion.url} will stop reaching the platform. Its recorded call history is deleted with it.`
            : ''
        }
        confirmLabel="Delete webhook"
        destructive
        busy={deleteWebhookMutation.isPending}
        onClose={() => setDeletion(null)}
        onConfirm={() => {
          if (deletion?.kind === 'webhook') deleteWebhookMutation.mutate(deletion.id)
          setDeletion(null)
        }}
      />
    </Stack>
  )
}
