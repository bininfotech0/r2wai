import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Paper, Stack, Switch, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { createApiKey, listApiKeys, toggleApiKey } from '../../admin/api'
import { RevealKeyDialog } from '../../admin/dialogs/RevealKeyDialog'
import { EmptyState } from '../../../components/EmptyState'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import type { AssistantDto } from '../types'

/**
 * The client side of PublishedAssistantsController (backend-only until now — implementation plan
 * Phase 6 closes that gap). Reuses the existing generic API key infrastructure (DeveloperTab's
 * create/list/toggle) rather than a parallel one, scoped to this one assistant via the
 * `assistant:{id}` convention PublishedAssistantsController.CanAccessAssistant already checks —
 * same reuse-not-reinvent pattern the Channels tab above this uses for chatbot creation.
 */
export function AssistantApiAccessSection({ assistant }: { assistant: AssistantDto }) {
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const [revealedKey, setRevealedKey] = useState<string | null>(null)
  const scopeTag = `assistant:${assistant.id}`

  const keysQuery = useQuery({
    queryKey: ['api-keys-for-assistant', assistant.id],
    queryFn: () => listApiKeys(1, 100),
    enabled: assistant.publishStatus === 'Published',
  })
  const scopedKeys = (keysQuery.data?.items ?? []).filter((k) => (k.scopes ?? '').includes(scopeTag))

  const createMutation = useMutation({
    mutationFn: () => createApiKey({ name: `${assistant.name} — API access`, scopes: ['read', scopeTag] }),
    onSuccess: (result) => {
      setRevealedKey(result.key)
      void queryClient.invalidateQueries({ queryKey: ['api-keys-for-assistant', assistant.id] })
    },
    onError: () => notify('Failed to create API key', 'error'),
  })

  const toggleMutation = useMutation({
    mutationFn: toggleApiKey,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['api-keys-for-assistant', assistant.id] }),
    onError: () => notify('Could not change this API key. It may have been deleted.', 'error'),
  })

  if (assistant.publishStatus !== 'Published') {
    return (
      <Alert severity="warning" variant="outlined">
        Publish this assistant to enable direct API access.
      </Alert>
    )
  }

  const curlExample = `curl -X POST ${window.location.origin}/api/v1/published-assistants/${assistant.id}/chat \\
  -H "X-Api-Key: <your-key>" \\
  -H "Content-Type: application/json" \\
  -d '{"message":"Hello"}'`

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack spacing={1.5}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Direct API access
          </Typography>
          <Button size="small" startIcon={<AddIcon />} disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
            New API key
          </Button>
        </Stack>
        <Typography variant="caption" color="text.secondary">
          Calls this assistant directly as a REST API, pinned to published version {assistant.publishedVersion}
          — editing the assistant afterward doesn't change what this endpoint serves until you publish again.
        </Typography>
        <Box
          component="pre"
          sx={{ fontSize: 12, bgcolor: 'action.hover', p: 1.5, borderRadius: 1, overflow: 'auto', m: 0 }}
        >
          {curlExample}
        </Box>
        {scopedKeys.length === 0 && <EmptyState title="No API keys for this assistant yet" />}
        {scopedKeys.map((k) => (
          <Stack key={k.id} direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
            <Typography variant="body2">
              {k.name} — {k.keyPrefix}…
            </Typography>
            <Switch size="small" checked={k.isActive} onChange={() => toggleMutation.mutate(k.id)} />
          </Stack>
        ))}
      </Stack>
      <RevealKeyDialog open={!!revealedKey} onClose={() => setRevealedKey(null)} apiKey={revealedKey} />
    </Paper>
  )
}
