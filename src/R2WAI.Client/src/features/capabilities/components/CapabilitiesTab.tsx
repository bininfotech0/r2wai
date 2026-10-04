import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Card, CardActionArea, CardContent, Grid, Link, Stack, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import ExtensionOutlined from '@mui/icons-material/ExtensionOutlined'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import { EmptyState } from '../../../components/EmptyState'
import { StatusBadge } from '../../../components/StatusBadge'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { ToolsTab } from '../../assistants/components/ToolsTab'
import { createBusinessCapability, listBusinessCapabilities } from '../api'
import type { CreateCapabilityFormValues } from '../dialogs/CreateCapabilityDialog'
import { CreateCapabilityDialog } from '../dialogs/CreateCapabilityDialog'
import { CapabilityDetailDialog } from './CapabilityDetailDialog'

interface CapabilitiesTabProps {
  assistantId: string
  toolsJson: string | null
}

/**
 * The brief's "MOST IMPORTANT UX CHANGE" (§6): business-oriented Capabilities as the primary
 * view, replacing a flat individual-tool checklist as the first thing an admin sees. The
 * underlying per-tool enable/disable mechanism (ToolsTab) is what actually gates what the
 * assistant can call at chat time — Capabilities are an organizing layer on top of it, not (yet)
 * a runtime control themselves, so it stays reachable under Advanced rather than being removed.
 */
export function CapabilitiesTab({ assistantId, toolsJson }: CapabilitiesTabProps) {
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showAdvanced, setShowAdvanced] = useState(false)

  const query = useQuery({
    queryKey: ['business-capabilities', 'list', assistantId],
    queryFn: () => listBusinessCapabilities(assistantId),
  })

  const createMutation = useMutation({
    mutationFn: (values: CreateCapabilityFormValues) =>
      createBusinessCapability({ assistantId, name: values.name, description: values.description }),
    onSuccess: (created) => {
      notify('Capability created', 'success')
      setCreateOpen(false)
      void queryClient.invalidateQueries({ queryKey: ['business-capabilities', 'list', assistantId] })
      setSelectedId(created.id)
    },
    onError: () => notify('Failed to create capability', 'error'),
  })

  const capabilities = query.data?.items ?? []

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="body2" color="text.secondary">
          Capabilities connect business functions — like Invoice or Leave Management — to this
          assistant's approved tools, knowledge, and automations.
        </Typography>
        <Button size="small" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          Add Capability
        </Button>
      </Stack>

      {capabilities.length === 0 && !query.isLoading ? (
        <EmptyState
          icon={ExtensionOutlined}
          title="No Capabilities"
          description="Your Assistant does not have any capabilities yet. Capabilities connect business functions such as Leave, Invoice, PO or PR to approved data, APIs, knowledge and workflows."
          actionLabel="+ Add Capability"
          onAction={() => setCreateOpen(true)}
        />
      ) : (
        <Grid container spacing={1.5}>
          {capabilities.map((cap) => (
            <Grid key={cap.id} size={{ xs: 12, sm: 6 }}>
              <Card variant="outlined">
                <CardActionArea onClick={() => setSelectedId(cap.id)} sx={{ p: 1.5 }}>
                  <CardContent sx={{ p: 0.5, '&:last-child': { pb: 0.5 } }}>
                    <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
                      <Typography variant="subtitle2" noWrap>
                        {cap.name}
                      </Typography>
                      <StatusBadge status={cap.status} />
                    </Stack>
                    {cap.description && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }} noWrap>
                        {cap.description}
                      </Typography>
                    )}
                    <Stack direction="row" spacing={1.5}>
                      <Typography variant="caption" color="text.secondary">
                        Tools: {cap.toolCount}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        Knowledge: {cap.knowledgeBaseCount}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        Workflows: {cap.workflowCount}
                      </Typography>
                    </Stack>
                  </CardContent>
                </CardActionArea>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Box>
        <Link
          component="button"
          type="button"
          variant="body2"
          underline="hover"
          onClick={() => setShowAdvanced((v) => !v)}
          sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}
        >
          <ExpandMoreIcon fontSize="small" sx={{ transform: showAdvanced ? 'rotate(180deg)' : 'none' }} />
          Advanced — Individual tools
        </Link>
        {showAdvanced && (
          <Box sx={{ mt: 1.5 }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
              What this assistant can actually call at chat time is controlled here, independent of
              how Capabilities above group them for reporting.
            </Typography>
            <ToolsTab assistantId={assistantId} toolsJson={toolsJson} />
          </Box>
        )}
      </Box>

      <CreateCapabilityDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(values) => createMutation.mutate(values)}
        isSubmitting={createMutation.isPending}
      />

      <CapabilityDetailDialog capabilityId={selectedId} assistantId={assistantId} onClose={() => setSelectedId(null)} />
    </Stack>
  )
}
