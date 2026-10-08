import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, CircularProgress, Paper, Stack, Switch, Tab, Tabs, TextField, Typography } from '@mui/material'
import BusinessOutlined from '@mui/icons-material/BusinessOutlined'
import ToggleOnOutlined from '@mui/icons-material/ToggleOnOutlined'
import TuneOutlined from '@mui/icons-material/TuneOutlined'
import SaveOutlined from '@mui/icons-material/SaveOutlined'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { ErrorState } from '../../../components/ErrorState'
import { getSettings, parseFeatureFlags, parseLimits, updateFeatureFlags, updateLimits, updateOrganizationDetails } from '../api'
import type { TenantFeatureFlags, TenantLimits } from '../types'

// API keys and webhooks live on the API & SDK page and AI models have their own Settings entry,
// so this page keeps only what is edited nowhere else.
const CATEGORIES = [
  { label: 'General', icon: <BusinessOutlined fontSize="small" /> },
  { label: 'Features', icon: <ToggleOnOutlined fontSize="small" /> },
  { label: 'Limits', icon: <TuneOutlined fontSize="small" /> },
] as const
type Category = (typeof CATEGORIES)[number]['label']

const FEATURE_LABELS: { key: keyof TenantFeatureFlags; label: string }[] = [
  { key: 'assistants', label: 'AI Assistants' },
  { key: 'workflows', label: 'Automations (legacy)' },
  { key: 'approvals', label: 'Confirmations' },
  { key: 'knowledge', label: 'Knowledge' },
  { key: 'integrations', label: 'Integrations' },
  { key: 'analytics', label: 'Analytics' },
]

export function SettingsPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [category, setCategory] = useState<Category>('General')

  const query = useQuery({ queryKey: ['tenant-settings'], queryFn: getSettings })
  const settings = query.data

  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [domain, setDomain] = useState('')
  const [flags, setFlags] = useState<TenantFeatureFlags | null>(null)
  const [limits, setLimits] = useState<TenantLimits | null>(null)

  const savedFlags = settings ? parseFeatureFlags(settings.features) : null
  const savedLimits = settings ? parseLimits(settings.tenantSettings) : null
  const hasOrganizationChanges = !!settings && (
    name !== settings.tenantName || slug !== settings.tenantSlug || domain !== (settings.tenantDomain ?? '')
  )
  const hasFeatureChanges = !!flags && !!savedFlags && FEATURE_LABELS.some(({ key }) => flags[key] !== savedFlags[key])
  const hasLimitChanges = !!limits && !!savedLimits && (Object.keys(limits) as (keyof TenantLimits)[])
    .some((key) => limits[key] !== savedLimits[key])

  useEffect(() => {
    if (!settings) return
    setName(settings.tenantName)
    setSlug(settings.tenantSlug)
    setDomain(settings.tenantDomain ?? '')
    setFlags(parseFeatureFlags(settings.features))
    setLimits(parseLimits(settings.tenantSettings))
  }, [settings])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['tenant-settings'] })

  const orgMutation = useMutation({
    mutationFn: () => updateOrganizationDetails(name, slug, domain || undefined),
    onSuccess: () => {
      notify('Organization details saved', 'success')
      void invalidate()
    },
    onError: () => notify('Failed to save organization details', 'error'),
  })
  const flagsMutation = useMutation({
    mutationFn: () => updateFeatureFlags(flags!),
    onSuccess: () => {
      notify('Feature flags saved', 'success')
      void invalidate()
    },
    onError: () => notify('Failed to save feature flags', 'error'),
  })
  const limitsMutation = useMutation({
    mutationFn: () => updateLimits(settings!.tenantSettings, limits!),
    onSuccess: () => {
      notify('Limits saved', 'success')
      void invalidate()
    },
    onError: () => notify('Failed to save limits', 'error'),
  })

  if (query.isError) {
    return <ErrorState title="Unable to load settings" description="Your organization settings could not be retrieved." onRetry={() => void query.refetch()} />
  }

  if (query.isLoading || !settings || !flags || !limits) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Settings
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Organization details, enabled features, and usage limits.
        </Typography>
      </Box>

      <Tabs value={category} onChange={(_, v) => setCategory(v)} variant="scrollable" scrollButtons="auto" sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
        {CATEGORIES.map(({ label, icon }) => (
          <Tab key={label} label={label} icon={icon} iconPosition="start" value={label} />
        ))}
      </Tabs>

      {category === 'General' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 500 }}>
          <Stack spacing={2}>
            <TextField label="Organization name" fullWidth value={name} onChange={(e) => setName(e.target.value)} />
            <TextField label="Slug" fullWidth value={slug} onChange={(e) => setSlug(e.target.value)} />
            <TextField label="Domain" fullWidth value={domain} onChange={(e) => setDomain(e.target.value)} />
            <Box>
              <Button variant="contained" startIcon={<SaveOutlined />} disabled={orgMutation.isPending || !hasOrganizationChanges} onClick={() => orgMutation.mutate()}>
                {orgMutation.isPending ? 'Saving…' : 'Save changes'}
              </Button>
            </Box>
          </Stack>
        </Paper>
      )}

      {category === 'Features' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 500 }}>
          <Typography variant="subtitle2" sx={{ mb: 1.5 }}>
            Enabled Features
          </Typography>
          <Stack spacing={1}>
            {FEATURE_LABELS.map((f) => (
              <Stack key={f.key} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <Switch
                  checked={flags[f.key]}
                  onChange={(e) => setFlags({ ...flags, [f.key]: e.target.checked })}
                />
                <Typography variant="body2">{f.label}</Typography>
              </Stack>
            ))}
            <Box>
              <Button variant="contained" startIcon={<SaveOutlined />} disabled={flagsMutation.isPending || !hasFeatureChanges} onClick={() => flagsMutation.mutate()}>
                {flagsMutation.isPending ? 'Saving…' : 'Save changes'}
              </Button>
            </Box>
          </Stack>
        </Paper>
      )}

      {category === 'Limits' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 500 }}>
          <Stack spacing={2}>
            <TextField
              label="Max documents"
              type="number"
              fullWidth
              value={limits.maxDocuments}
              onChange={(e) => setLimits({ ...limits, maxDocuments: Number(e.target.value) })}
            />
            <TextField
              label="Max storage (MB)"
              type="number"
              fullWidth
              value={limits.maxStorageMb}
              onChange={(e) => setLimits({ ...limits, maxStorageMb: Number(e.target.value) })}
            />
            <TextField
              label="Max users"
              type="number"
              fullWidth
              value={limits.maxUsers}
              onChange={(e) => setLimits({ ...limits, maxUsers: Number(e.target.value) })}
            />
            <TextField
              label="Max assistants"
              type="number"
              fullWidth
              value={limits.maxAssistants}
              onChange={(e) => setLimits({ ...limits, maxAssistants: Number(e.target.value) })}
            />
            <TextField
              label="Max automations"
              type="number"
              fullWidth
              value={limits.maxWorkflows}
              onChange={(e) => setLimits({ ...limits, maxWorkflows: Number(e.target.value) })}
            />
            <Box>
              <Button variant="contained" startIcon={<SaveOutlined />} disabled={limitsMutation.isPending || !hasLimitChanges} onClick={() => limitsMutation.mutate()}>
                {limitsMutation.isPending ? 'Saving…' : 'Save changes'}
              </Button>
            </Box>
          </Stack>
        </Paper>
      )}
    </Box>
  )
}
