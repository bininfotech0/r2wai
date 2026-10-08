import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  Grid,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import LanguageIcon from '@mui/icons-material/Language'
import ForumIcon from '@mui/icons-material/Forum'
import AddIcon from '@mui/icons-material/Add'
import { PageHeader } from '../../../components/PageHeader'
import { ErrorState } from '../../../components/ErrorState'
import { EmptyState } from '../../../components/EmptyState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { StatusBadge, type StatusTone } from '../../../components/StatusBadge'
import { describeApiError } from '../../../lib/api/fetchJson'
import { listChatbots, listChatbotChannels } from '../../chatbots/api'
import { parseAllowedOriginsText, type ChatbotChannelDto, type ChatbotDto } from '../../chatbots/types'
import { ALL_CHANNEL_TYPES, CHANNEL_CAPABILITIES, describeChannelState, UNAVAILABLE_CHANNELS } from '../types'

/**
 * Distribution overview: every chatbot in the tenant crossed with every channel it could
 * be published to, showing the *real* state of each.
 *
 * This page deliberately never renders a green "Connected" for a messaging channel. The
 * backend stores a channel payload encrypted but ships no provider adapter, so the only
 * truthful states are "not configured" and "configured, unverified" — see
 * CHANNEL_CAPABILITIES and docs/api/MISSING-BACKEND-ENDPOINTS.md §2.4.
 */
export function DeployChannelsPage() {
  const navigate = useNavigate()

  const listQuery = useQuery({ queryKey: ['chatbots', 'deploy'], queryFn: () => listChatbots(1, 200) })
  const chatbots = listQuery.data?.items ?? []

  return (
    <Box>
      <PageHeader
        title="Channels"
        description="Where your agents are published, and what is actually live."
        actions={
          <Button variant="outlined" startIcon={<AddIcon />} onClick={() => navigate('/chatbots')}>
            New chatbot
          </Button>
        }
      />

      <Alert severity="info" sx={{ mb: 2 }}>
        Today only the <strong>website widget</strong> has a working runtime. Messaging channels (
        {[...ALL_CHANNEL_TYPES, ...UNAVAILABLE_CHANNELS.map((entry) => entry.channel)].join(', ')}) cannot send or
        receive messages yet, so they are hidden here unless a chatbot already has settings stored for one.
      </Alert>

      {listQuery.isLoading ? (
        <LoadingSkeleton count={3} />
      ) : listQuery.error ? (
        <ErrorState
          {...describeApiError(listQuery.error, 'Could not load your chatbots.')}
          onRetry={() => void listQuery.refetch()}
        />
      ) : chatbots.length === 0 ? (
        <EmptyState
          icon={LanguageIcon}
          title="Nothing to publish yet"
          description="Create a chatbot, then publish it to a website widget or a messaging channel from here."
          actionLabel="Go to Chatbots"
          onAction={() => navigate('/chatbots')}
        />
      ) : (
        <Stack spacing={2}>
          {chatbots.map((chatbot) => (
            <ChatbotChannelsCard key={chatbot.id} chatbot={chatbot} />
          ))}
        </Stack>
      )}
    </Box>
  )
}

function ChatbotChannelsCard({ chatbot }: { chatbot: ChatbotDto }) {
  const navigate = useNavigate()

  // Real channel list, so "configured" reflects what is actually stored server-side
  // rather than a hardcoded false.
  const channelsQuery = useQuery({
    queryKey: ['chatbot', chatbot.id, 'channels'],
    queryFn: () => listChatbotChannels(chatbot.id),
  })
  const configuredChannels = new Set(
    (channelsQuery.data ?? []).map((c: ChatbotChannelDto) => c.channel),
  )

  const domainCount = parseAllowedOriginsText(chatbot.allowedOrigins).split('\n').filter(Boolean).length
  const widgetLive = chatbot.status === 'Active'

  /**
   * A failed channel read must not be rendered as "Not configured". An empty
   * `configuredChannels` set is indistinguishable from a real "nothing stored yet" when the
   * request failed, and the wrong one of those two is what the user would act on. So the
   * messaging tiles collapse into an explicit error while the widget tile — which reads
   * only from the already-loaded chatbot record — stays accurate and usable.
   */
  if (channelsQuery.isError) {
    return (
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }} noWrap title={chatbot.name}>
            {chatbot.name}
          </Typography>
          <Chip
            size="small"
            variant="outlined"
            label={chatbot.status}
            color={chatbot.status === 'Active' ? 'success' : 'default'}
          />
        </Stack>
        <Alert
          severity="error"
          action={
            <Button size="small" color="inherit" onClick={() => void channelsQuery.refetch()}>
              Retry
            </Button>
          }
          sx={{ mb: 1.5 }}
        >
          Could not load the messaging channels for this chatbot, so their state is unknown.
        </Alert>
        <Grid container spacing={1.5}>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <ChannelTile
              icon={<LanguageIcon fontSize="small" color="primary" />}
              title="Website Widget"
              description="Floating chat widget embedded in your own site."
              status={widgetLive ? 'Live' : 'Agent not active'}
              statusTone={widgetLive ? 'success' : 'default'}
              detail={
                domainCount > 0
                  ? `Restricted to ${domainCount} origin${domainCount === 1 ? '' : 's'}.`
                  : 'Any site can embed it.'
              }
              action="Configure widget"
              onAction={() => navigate(`/deploy/widget/${chatbot.id}`)}
            />
          </Grid>
        </Grid>
      </Paper>
    )
  }

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }} noWrap title={chatbot.name}>
          {chatbot.name}
        </Typography>
        <Chip
          size="small"
          variant="outlined"
          label={chatbot.status}
          color={chatbot.status === 'Active' ? 'success' : 'default'}
        />
      </Stack>

      <Grid container spacing={1.5}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <ChannelTile
            icon={<LanguageIcon fontSize="small" color="primary" />}
            title="Website Widget"
            description="Floating chat widget embedded in your own site."
            status={widgetLive ? 'Live' : 'Agent not active'}
            statusTone={widgetLive ? 'success' : 'default'}
            detail={
              domainCount > 0
                ? `Restricted to ${domainCount} origin${domainCount === 1 ? '' : 's'}.`
                : 'Any site can embed it.'
            }
            action="Configure widget"
            onAction={() => navigate(`/deploy/widget/${chatbot.id}`)}
          />
        </Grid>

        {/* Messaging channels have no provider adapter, so a tile is only worth showing when it
            holds stored settings the admin may want to review or remove. */}
        {ALL_CHANNEL_TYPES.filter((channel) => configuredChannels.has(channel)).map((channel) => {
          const state = describeChannelState(channel, chatbot, true)
          return (
            <Grid key={channel} size={{ xs: 12, sm: 6, md: 3 }}>
              <ChannelTile
                icon={<ForumIcon fontSize="small" color="disabled" />}
                title={channel}
                description={CHANNEL_CAPABILITIES[channel].note}
                status={state.label}
                statusTone={state.tone}
                detail={`Stored ${new Date(
                  (channelsQuery.data ?? []).find((c) => c.channel === channel)?.connectedAt ?? chatbot.createdAt,
                ).toLocaleDateString()}`}
                action={channel === 'WhatsApp' ? 'Set up WhatsApp' : 'Manage on chatbot'}
                onAction={() =>
                  channel === 'WhatsApp' ? navigate(`/deploy/whatsapp/${chatbot.id}`) : navigate(`/chatbots/${chatbot.id}`)
                }
              />
            </Grid>
          )
        })}
      </Grid>
    </Paper>
  )
}

function ChannelTile({
  icon,
  title,
  description,
  status,
  statusTone = 'default',
  detail,
  action,
  onAction,
  note,
}: {
  icon: React.ReactNode
  title: string
  description: string
  status: string
  statusTone?: StatusTone
  detail?: string
  action?: string
  onAction?: () => void
  note?: string
}) {
  return (
    <Paper variant="outlined" sx={{ p: 1.5, height: '100%', display: 'flex', flexDirection: 'column', gap: 1 }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        {icon}
        <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
          {title}
        </Typography>
      </Stack>
      <Box>
        <StatusBadge status={status} tone={statusTone} />
      </Box>
      <Typography variant="caption" color="text.secondary" sx={{ flexGrow: 1 }}>
        {description}
      </Typography>
      {detail && (
        <Typography variant="caption" color="text.secondary">
          {detail}
        </Typography>
      )}
      {note && (
        <Typography variant="caption" color="text.disabled" sx={{ fontFamily: 'monospace', fontSize: '0.7rem' }}>
          {note}
        </Typography>
      )}
      {action && onAction && (
        <Button size="small" variant="outlined" onClick={onAction} sx={{ alignSelf: 'flex-start' }}>
          {action}
        </Button>
      )}
    </Paper>
  )
}
