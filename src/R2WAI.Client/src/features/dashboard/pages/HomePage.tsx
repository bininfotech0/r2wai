import { useState } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  Box,
  Button,
  ButtonBase,
  Paper,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'
import CircleIcon from '@mui/icons-material/Circle'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import BadgeOutlined from '@mui/icons-material/BadgeOutlined'
import AccountBalanceOutlined from '@mui/icons-material/AccountBalanceOutlined'
import SupportAgentOutlined from '@mui/icons-material/SupportAgentOutlined'
import DescriptionOutlined from '@mui/icons-material/DescriptionOutlined'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import InsightsOutlined from '@mui/icons-material/InsightsOutlined'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutlineOutlined'
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined'
import type { SvgIconComponent } from '@mui/icons-material'
import { fetchJson } from '../../../lib/api/fetchJson'
import { useAuth } from '../../../lib/auth/useAuth'
import { getPersona } from '../../../lib/nav/roleNav'
import { StatusBadge } from '../../../components/StatusBadge'
import { EmptyState } from '../../../components/EmptyState'
import { SectionCard } from '../../../components/SectionCard'
import { UniversalCreate, type CreateKind } from '../../../components/UniversalCreate'
import { OnboardingChecklist } from '../components/OnboardingChecklist'
import { CreateAssistantDialog } from '../../assistants/dialogs/CreateAssistantDialog'
import { CreateAutomationDialog } from '../../automations/dialogs/CreateAutomationDialog'
import type { AssistantSummary, PagedResult, RecentWorkItem, WorkflowSummary } from '../types'

/** Clickable list row (recent work, pending approvals) — a real link so Tab/Enter/middle-click work. */
const linkRowSx = {
  alignItems: 'center',
  justifyContent: 'space-between',
  px: 1,
  py: 0.75,
  mx: -1,
  borderRadius: 1.5,
  color: 'inherit',
  textDecoration: 'none',
  transition: 'background-color 140ms ease',
  '&:hover': { bgcolor: 'action.hover' },
  '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.light', outlineOffset: 1 },
} as const

/**
 * Starting points, as in Copilot Studio's template gallery. Each one opens the real AI-draft flow
 * with its description filled in; nothing is created until the user confirms there.
 */
const TEMPLATES: { title: string; blurb: string; icon: SvgIconComponent; description: string }[] = [
  {
    title: 'HR helpdesk',
    blurb: 'Leave, payroll and policy questions',
    icon: BadgeOutlined,
    description: 'An HR helpdesk agent that answers employee questions about leave, payroll and HR policies from our policy documents, and says when a question needs a person.',
  },
  {
    title: 'Citizen services',
    blurb: 'Scheme eligibility and how to apply',
    icon: AccountBalanceOutlined,
    description: 'A citizen services agent that explains government scheme eligibility, required documents and how to apply, in plain language, citing the official circulars.',
  },
  {
    title: 'IT service desk',
    blurb: 'Troubleshooting and access requests',
    icon: SupportAgentOutlined,
    description: 'An IT service desk agent that helps staff troubleshoot common problems, explains how to request access, and summarises the issue when it needs to go to the IT team.',
  },
  {
    title: 'Document Q&A',
    blurb: 'Answers from your uploaded files',
    icon: DescriptionOutlined,
    description: 'A document question-answering agent that answers only from the uploaded documents, cites the source for every answer, and says so when the documents do not cover a question.',
  },
]

function useRecentWork() {
  return useQuery({
    queryKey: ['dashboard', 'recent-work'],
    queryFn: async (): Promise<RecentWorkItem[]> => {
      const [assistants, workflows] = await Promise.all([
        fetchJson<PagedResult<AssistantSummary>>('/assistants?page=1&pageSize=5'),
        fetchJson<PagedResult<WorkflowSummary>>('/workflows?page=1&pageSize=5'),
      ])
      const items: RecentWorkItem[] = [
        ...assistants.items.map((a) => ({
          id: a.id,
          name: a.name,
          type: 'Assistant' as const,
          status: a.publishStatus,
          timestamp: a.createdAt,
        })),
        ...workflows.items.map((w) => ({
          id: w.id,
          name: w.name,
          type: 'Automation' as const,
          status: w.versionStatus,
          timestamp: w.modifiedAt ?? w.createdAt,
        })),
      ]
      return items.sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp)).slice(0, 6)
    },
  })
}

function usePendingApprovalsCount() {
  return useQuery({
    queryKey: ['approvals', 'pending-count'],
    queryFn: () => fetchJson<PagedResult<unknown>>('/approvals/pending?page=1&pageSize=1'),
  })
}

function greeting(): string {
  const hour = new Date().getHours()
  return hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening'
}

/**
 * Create-first Home, as in Copilot Studio: describe what you want, or start from a template, then
 * pick up recent work. The platform's numbers live on Activity → Monitor (PlatformOverview).
 */
export function HomePage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const persona = getPersona(user)
  const isAdmin = persona === 'Admin' || persona === 'SuperAdmin'
  const recentWork = useRecentWork()
  const pendingApprovals = usePendingApprovalsCount()
  const [prompt, setPrompt] = useState('')
  const [pickerOpen, setPickerOpen] = useState(false)
  // A template or the typed prompt; whichever opened the dialog seeds its description.
  const [assistantSeed, setAssistantSeed] = useState<string | null>(null)
  const [createAutomationOpen, setCreateAutomationOpen] = useState(false)

  function handleCreateKind(kind: CreateKind) {
    setPickerOpen(false)
    if (kind === 'assistant') setAssistantSeed(prompt)
    else if (kind === 'automation') setCreateAutomationOpen(true)
    else if (kind === 'knowledge') navigate('/knowledge')
    else navigate('/connections')
  }

  const firstName = user?.displayName?.split(' ')[0]

  return (
    <Stack spacing={4}>
      {/* Hero: one box, one question. */}
      {/* alignSelf, not mx:auto: Stack resets its children's margins. */}
      <Box sx={{ maxWidth: 880, width: '100%', alignSelf: 'center', pt: { xs: 1, md: 3 }, textAlign: 'center' }}>
        <Typography variant="body1" color="text.secondary">
          {greeting()}{firstName ? `, ${firstName}` : ''}
        </Typography>
        <Typography
          variant="h4"
          component="h1"
          sx={{ fontWeight: 700, letterSpacing: '-0.02em', mt: 0.5, mb: 2.5, fontSize: { xs: '1.6rem', md: '2.1rem' } }}
        >
          What would you like to build?
        </Typography>

        <Paper
          component="form"
          variant="outlined"
          onSubmit={(e) => {
            e.preventDefault()
            setPickerOpen(true)
          }}
          sx={{
            p: 1.5,
            textAlign: 'left',
            borderRadius: 3,
            transition: (t) => t.transitions.create(['border-color', 'box-shadow'], { duration: 140, easing: 'ease' }),
            '&:focus-within': {
              borderColor: 'primary.main',
              boxShadow: (t) => `0 0 0 3px color-mix(in srgb, ${t.palette.primary.main} 14%, transparent)`,
            },
          }}
        >
          <TextField
            fullWidth
            multiline
            minRows={2}
            maxRows={6}
            variant="standard"
            placeholder="Describe what you want R2WAI to create…"
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                setPickerOpen(true)
              }
            }}
            slotProps={{ htmlInput: { 'aria-label': 'Describe what you want R2WAI to create' }, input: { disableUnderline: true, sx: { px: 1, fontSize: 16 } } }}
          />
          <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mt: 1, gap: 1, flexWrap: 'wrap' }}>
            <Typography variant="caption" color="text.secondary" sx={{ px: 1 }}>
              e.g. “Answer pension questions from our 2024 circulars, in English and Hindi.”
            </Typography>
            <Button type="submit" variant="contained" startIcon={<AutoAwesomeIcon />}>
              Create
            </Button>
          </Stack>
        </Paper>

        <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: 'wrap', justifyContent: 'center', rowGap: 1 }}>
          <Button variant="outlined" size="small" onClick={() => setAssistantSeed(prompt)}>+ Agent</Button>
          <Button variant="outlined" size="small" onClick={() => setCreateAutomationOpen(true)}>+ Automation</Button>
          <Button variant="outlined" size="small" onClick={() => navigate('/knowledge')}>+ Knowledge</Button>
          <Button variant="outlined" size="small" onClick={() => navigate('/connections')}>+ Connection</Button>
        </Stack>
      </Box>

      <UniversalCreate open={pickerOpen} onClose={() => setPickerOpen(false)} onSelect={handleCreateKind} />
      {assistantSeed !== null && (
        <CreateAssistantDialog
          open
          initialDescription={assistantSeed}
          onClose={() => setAssistantSeed(null)}
          onCreated={(id) => {
            setAssistantSeed(null)
            setPrompt('')
            navigate(`/assistants/${id}`)
          }}
        />
      )}
      {createAutomationOpen && (
        <CreateAutomationDialog
          open={createAutomationOpen}
          initialDescription={prompt}
          onClose={() => setCreateAutomationOpen(false)}
          onCreated={(id) => {
            setCreateAutomationOpen(false)
            setPrompt('')
            navigate(`/automations/${id}`)
          }}
        />
      )}

      <OnboardingChecklist />

      {/* Templates */}
      <Box component="section" aria-labelledby="templates-heading">
        <Typography id="templates-heading" variant="h6" component="h2" sx={{ fontWeight: 650, mb: 1.5 }}>
          Start from a template
        </Typography>
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(4, minmax(0, 1fr))' } }}>
          {TEMPLATES.map(({ title, blurb, icon: Icon, description }) => (
            <ButtonBase
              key={title}
              onClick={() => setAssistantSeed(description)}
              aria-label={`Start from the ${title} template`}
              sx={{
                display: 'block',
                textAlign: 'left',
                p: 2,
                borderRadius: 2,
                border: '1px solid',
                borderColor: 'divider',
                bgcolor: 'background.paper',
                transition: (t) => t.transitions.create(['border-color', 'transform', 'box-shadow'], { duration: 160, easing: 'ease' }),
                '&:hover': { borderColor: 'primary.main', transform: 'translateY(-2px)', boxShadow: 2 },
              }}
            >
              <Box sx={{ display: 'grid', placeItems: 'center', width: 36, height: 36, borderRadius: 1.5, bgcolor: 'action.hover', color: 'primary.main', mb: 1.5 }}>
                <Icon fontSize="small" />
              </Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 650 }}>{title}</Typography>
              <Typography variant="body2" color="text.secondary">{blurb}</Typography>
            </ButtonBase>
          ))}
        </Box>
      </Box>

      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'minmax(0, 3fr) minmax(0, 2fr)' } }}>
        <SectionCard
          title="Recent"
          action={<Button size="small" component={RouterLink} to="/assistants">All agents</Button>}
        >
          {recentWork.isLoading && (
            <Stack spacing={1.5}>
              {[0, 1, 2].map((i) => (
                <Stack key={i} direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                  <Skeleton variant="rounded" width={28} height={28} sx={{ borderRadius: 1.5 }} />
                  <Skeleton variant="text" width="60%" />
                </Stack>
              ))}
            </Stack>
          )}
          {recentWork.data && recentWork.data.length === 0 && (
            <EmptyState
              icon={InboxOutlinedIcon}
              title="Nothing yet"
              description="Create your first agent or automation to see it here."
            />
          )}
          <Stack spacing={0.5}>
            {recentWork.data?.map((item) => {
              const Icon = item.type === 'Assistant' ? SmartToyOutlined : AccountTreeOutlined
              const target = item.type === 'Assistant' ? `/assistants/${item.id}` : `/automations/${item.id}`
              return (
                <Stack key={`${item.type}-${item.id}`} component={RouterLink} to={target} direction="row" spacing={1.5} sx={linkRowSx}>
                  <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', minWidth: 0 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 28, height: 28, borderRadius: 1.5, flexShrink: 0, bgcolor: 'action.selected', color: 'text.secondary' }}>
                      <Icon sx={{ fontSize: 16 }} />
                    </Box>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="body2" noWrap>{item.name}</Typography>
                      <Typography variant="caption" color="text.secondary">{item.type === 'Assistant' ? 'Agent' : 'Automation'}</Typography>
                    </Box>
                  </Stack>
                  <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', flexShrink: 0 }}>
                    <StatusBadge status={item.status} />
                    <ChevronRightIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
                  </Stack>
                </Stack>
              )
            })}
          </Stack>
        </SectionCard>

        <Stack spacing={2}>
          <SectionCard title="Needs attention">
            {pendingApprovals.isLoading ? (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                <Skeleton variant="circular" width={28} height={28} />
                <Skeleton variant="text" width="50%" />
              </Stack>
            ) : pendingApprovals.data?.totalCount ? (
              <Stack component={RouterLink} to="/approvals" direction="row" spacing={1.5} sx={linkRowSx}>
                <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 28, height: 28, borderRadius: '50%', bgcolor: (t) => (t.palette.mode === 'dark' ? 'warning.dark' : '#FFEBE2'), color: 'warning.main' }}>
                    <CircleIcon sx={{ fontSize: 10 }} />
                  </Box>
                  <Typography variant="body2">
                    {pendingApprovals.data.totalCount} confirmation{pendingApprovals.data.totalCount === 1 ? '' : 's'} waiting on you
                  </Typography>
                </Stack>
                <ChevronRightIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
              </Stack>
            ) : (
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', color: 'text.secondary' }}>
                <CheckCircleOutlineIcon sx={{ fontSize: 20, color: 'success.main' }} />
                <Typography variant="body2" color="text.secondary">All caught up — no approvals waiting.</Typography>
              </Stack>
            )}
          </SectionCard>

          {isAdmin && (
            <Stack component={RouterLink} to="/monitor" direction="row" spacing={1.5} sx={{ ...linkRowSx, mx: 0, p: 2, border: '1px solid', borderColor: 'divider', borderRadius: 2, bgcolor: 'background.paper' }}>
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                <InsightsOutlined color="primary" />
                <Box>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>Usage and performance</Typography>
                  <Typography variant="caption" color="text.secondary">Conversations, success rate, tokens and agent ranking</Typography>
                </Box>
              </Stack>
              <ChevronRightIcon sx={{ fontSize: 18, color: 'text.disabled' }} />
            </Stack>
          )}
        </Stack>
      </Box>
    </Stack>
  )
}
