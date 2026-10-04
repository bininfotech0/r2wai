import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Stack,
  Typography,
} from '@mui/material'
import LinkOutlined from '@mui/icons-material/LinkOutlined'
import { StatusBadge } from './StatusBadge'
import { TOOL_TYPE_LABELS, type IntegrationDto, type ToolType } from '../features/integrations/types'

interface IntegrationCardProps {
  integration: IntegrationDto
  onConfigure: () => void
  onTest?: () => void
  /** Disables Configure/Test and shows a caption, e.g. while a mutation is in flight. */
  busy?: boolean
  /**
   * When true the card renders the stored configuration state instead of a Configure
   * button — used on read-only surfaces (assistant tool assignment) where editing the
   * integration belongs to another page.
   */
  readOnly?: boolean
  /** Optional override, e.g. an assistant name in "used by" context. */
  footerNote?: string
}

/**
 * Connector tile for the Integrations library and the agent wizard's tool step.
 *
 * The status shown is always `lastTestStatus` — the real, persisted result of the last
 * live `POST /integrations/{id}/test` call — never `isActive` alone. A connector that
 * is switched on but has never been tested reads "Not tested", because that is the
 * honest state: enabled is a config flag, not proof the credentials work.
 */
export function IntegrationCard({ integration, onConfigure, onTest, busy, readOnly, footerNote }: IntegrationCardProps) {
  const tested = integration.lastTestStatus != null
  const connected = integration.lastTestStatus === 'Connected'

  return (
    <Card variant="outlined" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <CardContent sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column' }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start', mb: 1 }}>
          <Box
            sx={{
              width: 32,
              height: 32,
              flexShrink: 0,
              borderRadius: 2,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              bgcolor: 'action.hover',
              color: 'text.secondary',
            }}
          >
            <LinkOutlined fontSize="small" />
          </Box>
          <Box sx={{ minWidth: 0, flexGrow: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }} noWrap title={integration.name}>
              {integration.name}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {TOOL_TYPE_LABELS[integration.type as ToolType] ?? integration.type}
            </Typography>
          </Box>
        </Stack>

        <Typography
          variant="body2"
          color="text.secondary"
          sx={{
            mb: 1.5,
            flexGrow: 1,
            display: '-webkit-box',
            WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical',
            overflow: 'hidden',
          }}
        >
          {integration.description || 'No description provided.'}
        </Typography>

        <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 0.5, mb: 1 }}>
          {tested ? (
            <StatusBadge status={connected ? 'Connected' : 'Error'} />
          ) : (
            <StatusBadge status="Not tested" tone="default" />
          )}
          <StatusBadge status={integration.isActive ? 'Enabled' : 'Disabled'} />
          {integration.lastTestedAt && (
            <Typography variant="caption" color="text.secondary">
              Tested {new Date(integration.lastTestedAt).toLocaleDateString()}
            </Typography>
          )}
        </Stack>

        {footerNote && (
          <Typography variant="caption" color="text.secondary" sx={{ mb: 1 }}>
            {footerNote}
          </Typography>
        )}

        {integration.endpointUrl && (
          <Typography
            variant="caption"
            color="text.secondary"
            noWrap
            title={integration.endpointUrl}
            sx={{ display: 'block', mb: 1, fontFamily: 'ui-monospace, Menlo, Consolas, monospace' }}
          >
            {integration.endpointUrl}
          </Typography>
        )}
      </CardContent>

      {!readOnly && (
        <Stack direction="row" spacing={1} sx={{ px: 2, pb: 2 }}>
          <Button size="small" variant="outlined" onClick={onConfigure} disabled={busy}>
            Configure
          </Button>
          {onTest && (
            <Button size="small" color="inherit" onClick={onTest} disabled={busy}>
              Test
            </Button>
          )}
          {busy && <Chip size="small" label="Working…" sx={{ alignSelf: 'center' }} />}
        </Stack>
      )}
    </Card>
  )
}
