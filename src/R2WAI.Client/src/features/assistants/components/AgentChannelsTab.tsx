import type { ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Box, Button, Grid, Paper, Stack, Typography } from '@mui/material'
import LanguageOutlined from '@mui/icons-material/LanguageOutlined'
import WhatsApp from '@mui/icons-material/WhatsApp'
import WebhookOutlined from '@mui/icons-material/WebhookOutlined'
import GroupsOutlined from '@mui/icons-material/GroupsOutlined'
import TagOutlined from '@mui/icons-material/TagOutlined'
import { Link as RouterLink, useNavigate } from 'react-router-dom'
import { queryKeys } from '../../../lib/api/queryKeys'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createChatbot, listChatbots } from '../../chatbots/api'
import type { AssistantDto } from '../types'

type TileState = 'live' | 'ready' | 'off' | 'soon'

const STATE_LABEL: Record<TileState, string> = {
  live: 'Live',
  ready: 'Available',
  off: 'Not set up',
  soon: 'Coming soon',
}

const STATE_COLOR: Record<TileState, string> = {
  live: 'success.main',
  ready: 'info.main',
  off: 'text.disabled',
  soon: 'text.disabled',
}

function ChannelTile({ icon, title, description, state, detail, action }: {
  icon: ReactNode
  title: string
  description: string
  state: TileState
  detail?: string
  action?: ReactNode
}) {
  return (
    <Paper variant="outlined" sx={{ p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', gap: 1.5, opacity: state === 'soon' ? 0.72 : 1 }}>
      <Box sx={{ display: 'grid', placeItems: 'center', width: 40, height: 40, borderRadius: 2, bgcolor: 'action.hover', color: 'primary.main' }}>
        {icon}
      </Box>
      <Box sx={{ flex: 1 }}>
        <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 650 }}>{title}</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>{description}</Typography>
      </Box>
      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
        <Box component="span" aria-hidden sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: STATE_COLOR[state] }} />
        <Typography variant="caption" sx={{ fontWeight: 600 }}>{STATE_LABEL[state]}</Typography>
        {detail && <Typography variant="caption" color="text.secondary">· {detail}</Typography>}
      </Stack>
      {action && <Box>{action}</Box>}
    </Paper>
  )
}

/**
 * Channels as tiles, as in Copilot Studio. Each channel is a chatbot backed by this agent; the
 * tiles open the existing set-up pages. Teams, Slack and WhatsApp are shown, honestly, as not yet available:
 * their inbound adapters don't exist (blueprint §3.3), so nothing here pretends they work.
 */
export function AgentChannelsTab({ assistant }: { assistant: AssistantDto }) {
  const navigate = useNavigate()
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()

  const chatbotsQuery = useQuery({
    queryKey: queryKeys.assistants.chatbots(assistant.id),
    queryFn: () => listChatbots(1, 20, assistant.id),
  })
  const chatbots = chatbotsQuery.data?.items ?? []
  const primary = chatbots.find((c) => c.status === 'Active') ?? chatbots[0]

  const createMutation = useMutation({
    mutationFn: () => createChatbot({ name: `${assistant.name} Widget`, assistantId: assistant.id, voiceEnabled: false }),
    onSuccess: (chatbot) => {
      notify('Website chatbot created', 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.chatbots(assistant.id) })
      navigate(`/deploy/widget/${chatbot.id}`)
    },
    onError: () => notify('Failed to create chatbot', 'error'),
  })

  const activeCount = chatbots.filter((c) => c.status === 'Active').length
  const needsChatbot = 'Set up the website channel first'

  return (
    <Stack spacing={2}>
      <Typography variant="body2" color="text.secondary">
        Make {assistant.name} available where people already are.
      </Typography>
      {assistant.publishStatus !== 'Published' && (
        <Alert severity="warning" variant="outlined">
          Publish this agent before sharing it: channels can&apos;t answer until a version is published.
        </Alert>
      )}
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 6, lg: 4 }}>
          <ChannelTile
            icon={<LanguageOutlined />}
            title="Website"
            description="Embed a chat widget on your site with one script tag."
            state={activeCount > 0 ? 'live' : primary ? 'ready' : 'off'}
            detail={chatbots.length > 0 ? `${chatbots.length} chatbot${chatbots.length === 1 ? '' : 's'}` : undefined}
            action={primary ? (
              <Button size="small" variant="outlined" component={RouterLink} to={`/deploy/widget/${primary.id}`}>Manage</Button>
            ) : (
              <Button size="small" variant="contained" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
                {createMutation.isPending ? 'Setting up…' : 'Set up'}
              </Button>
            )}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 4 }}>
          <ChannelTile icon={<WhatsApp />} title="WhatsApp" description="Answer on a WhatsApp Business number." state="soon" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 4 }}>
          <ChannelTile
            icon={<WebhookOutlined />}
            title="Webhook / API"
            description="Let any system send a message and get the reply, with a webhook key."
            state={primary ? 'ready' : 'off'}
            detail={primary ? undefined : needsChatbot}
            action={primary && (
              <Button size="small" variant="outlined" component={RouterLink} to={`/chatbots/${primary.id}`}>Manage key</Button>
            )}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 4 }}>
          <ChannelTile icon={<GroupsOutlined />} title="Microsoft Teams" description="Chat with the agent inside Teams." state="soon" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 4 }}>
          <ChannelTile icon={<TagOutlined />} title="Slack" description="Answer in Slack channels and direct messages." state="soon" />
        </Grid>
      </Grid>
    </Stack>
  )
}
