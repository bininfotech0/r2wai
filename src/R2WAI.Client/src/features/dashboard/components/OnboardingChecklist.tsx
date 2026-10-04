import { useQueries } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Box, Button, LinearProgress, Paper, Stack, Typography } from '@mui/material'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked'
import RocketLaunchOutlined from '@mui/icons-material/RocketLaunchOutlined'
import { fetchJson } from '../../../lib/api/fetchJson'
import { resolveOnboarding } from './onboarding'

/**
 * First-run checklist.
 *
 * Every item is computed from live tenant data — no persisted "onboarding state" flag,
 * because there is no endpoint to persist one (see docs/api/MISSING-BACKEND-ENDPOINTS.md
 * §3.4). The trade-off is deliberate: a checklist that reports real state is worth more
 * than one that remembers a click, and a user who deletes their only assistant correctly
 * sees the item untick again.
 *
 * The checklist hides itself once everything is done, so a mature tenant never carries a
 * permanently-finished widget on their home screen.
 *
 * The derivation itself lives in ./onboarding so that rule is unit-tested rather than only
 * visible in JSX.
 */
export function OnboardingChecklist() {
  const navigate = useNavigate()

  const [metricsQuery, integrationsQuery, chatbotsQuery, aiStatsQuery] = useQueries({
    queries: [
      { queryKey: ['operations', 'metrics'], queryFn: () => fetchJson<{ totalAssistants: number; totalDocuments: number; totalKnowledgeBases: number }>('/operations/metrics') },
      // Only existence matters here, so take the server's totalCount off a one-row page
      // rather than pulling a full page of integration records to read .length.
      { queryKey: ['integrations', 'onboarding-exists'], queryFn: () => fetchJson<{ items: unknown[]; totalCount?: number }>('/integrations?page=1&pageSize=1') },
      // "Has any chatbot been published" genuinely needs a scan, so this one pages the list —
      // at the server's own maximum of 100 rather than asking for 200 and being clamped.
      { queryKey: ['chatbots', 'onboarding-count'], queryFn: () => fetchJson<{ items: { status: string }[] }>('/chatbots?page=1&pageSize=100') },
      { queryKey: ['operations', 'ai-stats'], queryFn: () => fetchJson<{ samplesUsed: number }>('/operations/ai-stats') },
    ],
  })

  const anyLoading = [metricsQuery, integrationsQuery, chatbotsQuery, aiStatsQuery].some((q) => q.isLoading)
  const anyError = [metricsQuery, integrationsQuery, chatbotsQuery, aiStatsQuery].some((q) => q.isError)

  // Stay silent rather than guess: a partial or failed read would otherwise render
  // completed-looking progress the tenant has not actually made.
  if (anyLoading || anyError) return null

  const metrics = metricsQuery.data
  const integrationsPage = integrationsQuery.data
  const chatbots = chatbotsQuery.data?.items ?? []
  // Prefer the server's own count; fall back to the returned rows if this response shape
  // predates totalCount.
  const integrationCount = integrationsPage?.totalCount ?? integrationsPage?.items.length ?? 0

  const resolved = resolveOnboarding({
    totalAssistants: metrics?.totalAssistants ?? 0,
    totalKnowledgeBases: metrics?.totalKnowledgeBases ?? 0,
    totalDocuments: metrics?.totalDocuments ?? 0,
    integrationCount,
    hasPublishedChatbot: chatbots.some((c) => c.status === 'Active'),
    conversationSamples: aiStatsQuery.data?.samplesUsed ?? 0,
  })

  if (!resolved) return null
  const { items, done, total } = resolved

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <RocketLaunchOutlined fontSize="small" color="primary" />
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            Get started
          </Typography>
        </Stack>
        <Typography variant="caption" color="text.secondary">
          {done} of {total} complete
        </Typography>
      </Stack>
      <LinearProgress
        variant="determinate"
        value={(done / total) * 100}
        sx={{ mb: 2, height: 6, borderRadius: 3 }}
        aria-label={`Onboarding ${done} of ${total} complete`}
      />
      <Stack spacing={1}>
        {items.map((item) => (
          <Stack
            key={item.key}
            direction="row"
            spacing={1.5}
            sx={{ alignItems: 'flex-start', p: 1, borderRadius: 2, '&:hover': { bgcolor: 'action.hover' } }}
          >
            <Box sx={{ pt: 0.25, flexShrink: 0 }} aria-hidden>
              {item.isComplete ? (
                <CheckCircleIcon fontSize="small" sx={{ color: 'success.main' }} />
              ) : (
                <RadioButtonUncheckedIcon fontSize="small" sx={{ color: 'text.disabled' }} />
              )}
            </Box>
            <Box sx={{ minWidth: 0, flexGrow: 1 }}>
              <Typography
                variant="body2"
                sx={{ fontWeight: 600, color: item.isComplete ? 'text.secondary' : 'text.primary' }}
              >
                {item.label}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                {item.description}
              </Typography>
            </Box>
            {!item.isComplete && (
              <Button size="small" variant="outlined" onClick={() => navigate(item.path)} sx={{ flexShrink: 0 }}>
                {item.actionLabel}
              </Button>
            )}
          </Stack>
        ))}
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>
        Each step is read live from your workspace, so it ticks off whenever you do the work — on any device.
      </Typography>
    </Paper>
  )
}
