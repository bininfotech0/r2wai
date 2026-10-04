import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import InfoOutlined from '@mui/icons-material/InfoOutlined'
import LockOutlined from '@mui/icons-material/LockOutlined'
import BadgeOutlined from '@mui/icons-material/BadgeOutlined'
import PolicyOutlined from '@mui/icons-material/PolicyOutlined'
import BuildOutlined from '@mui/icons-material/BuildOutlined'
import ShieldOutlined from '@mui/icons-material/ShieldOutlined'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import KeyOffOutlined from '@mui/icons-material/KeyOffOutlined'
import HistoryOutlined from '@mui/icons-material/HistoryOutlined'
import EditOutlined from '@mui/icons-material/EditOutlined'
import { listModels, listRoles } from '../../admin/api'
import { listKnowledgeBases } from '../../knowledge/api'
import { listCapabilities } from '../../tools/api'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { getAuthStatus, listPolicies, upsertPolicy } from '../api'
import { EditPolicyDialog } from '../dialogs/EditPolicyDialog'
import { POLICY_TYPES, type GlobalPolicyDto, type PolicyType } from '../types'

const NAV_PERSONA_BY_ROLE: Record<string, string> = { SystemAdmin: 'Super Admin', Admin: 'Admin', User: 'User' }

function SectionHeader({ icon: Icon, title, stat }: { icon: typeof LockOutlined; title: string; stat?: string }) {
  return (
    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', width: '100%' }}>
      <Icon fontSize="small" color="action" />
      <Typography sx={{ fontWeight: 600, flexGrow: 1 }}>{title}</Typography>
      {stat && (
        <Typography variant="body2" color="text.secondary" sx={{ mr: 1 }}>
          {stat}
        </Typography>
      )}
    </Stack>
  )
}

function queryStat(isLoading: boolean, isError: boolean, value: string) {
  if (isLoading) return 'Loading…'
  if (isError) return 'Unavailable'
  return value
}

export function SecurityPolicyCenterPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [editingType, setEditingType] = useState<PolicyType | null>(null)

  const policiesQuery = useQuery({ queryKey: ['governance-policies'], queryFn: listPolicies })
  const authStatusQuery = useQuery({ queryKey: ['governance-auth-status'], queryFn: getAuthStatus })
  const capabilitiesQuery = useQuery({ queryKey: ['capabilities-for-security'], queryFn: () => listCapabilities(1, 100, '') })
  const modelsQuery = useQuery({ queryKey: ['models-for-security'], queryFn: listModels })
  const knowledgeBasesQuery = useQuery({
    queryKey: ['knowledgebases-for-security'],
    queryFn: () => listKnowledgeBases(1, 100, ''),
  })
  const rolesQuery = useQuery({ queryKey: ['roles-for-security'], queryFn: listRoles })

  const policies = policiesQuery.data ?? []
  const policyByType = new Map(policies.map((p) => [p.type, p]))
  const activePolicyCount = policies.filter((p) => p.isActive).length
  const failedQueries = [
    policiesQuery,
    authStatusQuery,
    capabilitiesQuery,
    modelsQuery,
    knowledgeBasesQuery,
    rolesQuery,
  ].filter((query) => query.isError)

  const capabilities = capabilitiesQuery.data?.items ?? []
  const highRiskCount = capabilities.filter((c) => c.riskLevel === 'High' || c.riskLevel === 'Critical').length
  const approvalRequiredCount = capabilities.filter((c) => c.approvalRequired).length

  const models = modelsQuery.data?.items ?? []
  const activeModelCount = models.filter((m) => m.isActive).length
  const sensitiveModelCount = models.filter((m) => m.dataClassification === 'Confidential' || m.dataClassification === 'Restricted').length

  const knowledgeBases = knowledgeBasesQuery.data?.items ?? []
  const sensitiveKbCount = knowledgeBases.filter((kb) => kb.dataClassification === 'Confidential' || kb.dataClassification === 'Restricted').length
  const sensitiveSourceCount = sensitiveModelCount + sensitiveKbCount

  const roles = rolesQuery.data?.items ?? []
  const systemRoles = roles.filter((r) => r.isSystem)
  const customRoles = roles.filter((r) => !r.isSystem)

  const upsertMutation = useMutation({
    mutationFn: upsertPolicy,
    onSuccess: () => {
      notify('Policy saved', 'success')
      setEditingType(null)
      void queryClient.invalidateQueries({ queryKey: ['governance-policies'] })
    },
    onError: () => notify('Failed to save policy', 'error'),
  })

  const editingPolicy: GlobalPolicyDto | null = editingType ? (policyByType.get(editingType) ?? null) : null

  return (
    <Box>
      <Stack sx={{ mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Security & Policies
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Review authentication, permissions, and data protection.
        </Typography>
      </Stack>

      {failedQueries.length > 0 && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={
            <Button
              color="inherit"
              size="small"
              onClick={() => void Promise.all([
                policiesQuery.refetch(),
                authStatusQuery.refetch(),
                capabilitiesQuery.refetch(),
                modelsQuery.refetch(),
                knowledgeBasesQuery.refetch(),
                rolesQuery.refetch(),
              ])}
            >
              Retry
            </Button>
          }
        >
          Some security data could not be loaded. Affected sections show “Unavailable” until you retry.
        </Alert>
      )}

      <Accordion defaultExpanded disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader
            icon={LockOutlined}
            title="Authentication"
            stat={queryStat(authStatusQuery.isLoading, authStatusQuery.isError, `${authStatusQuery.data?.mfaEnabledUsers ?? 0}/${authStatusQuery.data?.totalUsers ?? 0} with MFA`)}
          />
        </AccordionSummary>
        <AccordionDetails>
          <Stack spacing={1}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Typography variant="body2">
                {authStatusQuery.isLoading
                  ? 'Loading authentication status…'
                  : authStatusQuery.isError
                    ? 'Authentication status is unavailable.'
                    : `${authStatusQuery.data?.mfaEnabledUsers ?? 0} of ${authStatusQuery.data?.totalUsers ?? 0} users have MFA enabled`}
              </Typography>
              <Tooltip title="MFA is per-user opt-in (Profile > Security) unless the Auth policy below sets requireMfa.">
                <InfoOutlined fontSize="small" color="disabled" />
              </Tooltip>
            </Stack>
            <Chip
              label={authStatusQuery.isLoading ? 'SSO status loading' : authStatusQuery.isError ? 'SSO status unavailable' : `SSO ${authStatusQuery.data?.ssoConfigured ? 'configured' : 'not configured'}`}
              size="small"
              color={authStatusQuery.data?.ssoConfigured && !authStatusQuery.isError ? 'success' : 'default'}
              variant="outlined"
              sx={{ width: 'fit-content' }}
            />
            {authStatusQuery.data?.requireMfaPolicyActive && (
              <Chip label="MFA required by policy" size="small" color="success" variant="outlined" sx={{ width: 'fit-content' }} />
            )}
          </Stack>
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={BadgeOutlined} title="RBAC" stat={queryStat(rolesQuery.isLoading, rolesQuery.isError, `${systemRoles.length} system, ${customRoles.length} custom roles`)} />
        </AccordionSummary>
        <AccordionDetails>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            System roles determine navigation access. Custom roles add permissions without changing the app sections
            people can see.
          </Typography>
          {rolesQuery.isLoading ? (
            <Typography variant="body2" color="text.secondary">Loading roles…</Typography>
          ) : rolesQuery.isError ? (
            <Typography variant="body2" color="text.secondary">Role data is unavailable.</Typography>
          ) : (
            <List dense disablePadding>
              {systemRoles.map((r) => (
                <ListItem key={r.id} disableGutters>
                  <ListItemText primary={r.name} secondary={r.description ?? undefined} />
                  <Chip label={NAV_PERSONA_BY_ROLE[r.name] ?? 'Not tied to nav'} size="small" color="primary" variant="outlined" />
                </ListItem>
              ))}
            </List>
          )}
          <Button size="small" onClick={() => navigate('/users')} sx={{ mt: 1 }}>
            Manage roles & assignments
          </Button>
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={PolicyOutlined} title="Policies" stat={queryStat(policiesQuery.isLoading, policiesQuery.isError, `${activePolicyCount}/${POLICY_TYPES.length} active`)} />
        </AccordionSummary>
        <AccordionDetails>
          {policiesQuery.isLoading ? (
            <Typography variant="body2" color="text.secondary">Loading policies…</Typography>
          ) : policiesQuery.isError ? (
            <Typography variant="body2" color="text.secondary">Policy configuration is unavailable.</Typography>
          ) : (
            <List dense disablePadding>
              {POLICY_TYPES.map((type) => {
                const policy = policyByType.get(type)
                return (
                  <ListItem key={type} disablePadding divider>
                    <ListItemButton component="button" onClick={() => setEditingType(type)} aria-label={`Edit ${type} policy`}>
                      <ListItemText primary={type} secondary={policy?.name ?? 'Not configured'} />
                      <Chip
                        label={policy ? (policy.isActive ? 'Active' : 'Inactive') : 'Not configured'}
                        size="small"
                        color={policy?.isActive ? 'success' : 'default'}
                        variant="outlined"
                        sx={{ mr: 1 }}
                      />
                      <EditOutlined fontSize="small" color="action" aria-hidden="true" />
                    </ListItemButton>
                  </ListItem>
                )
              })}
            </List>
          )}
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={BuildOutlined} title="Tool Security" stat={queryStat(capabilitiesQuery.isLoading, capabilitiesQuery.isError, `${highRiskCount} high/critical risk`)} />
        </AccordionSummary>
        <AccordionDetails>
          <Typography variant="body2">
            {capabilitiesQuery.isLoading
              ? 'Loading tool security…'
              : capabilitiesQuery.isError
                ? 'Tool security data is unavailable.'
                : `${highRiskCount} tool${highRiskCount === 1 ? '' : 's'} rated High or Critical risk · ${approvalRequiredCount} require approval before an AI agent can call them.`}
          </Typography>
          <Button size="small" onClick={() => navigate('/tools')} sx={{ mt: 1 }}>
            View Tools & APIs
          </Button>
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={ShieldOutlined} title="Data Security" stat={queryStat(modelsQuery.isLoading || knowledgeBasesQuery.isLoading, modelsQuery.isError || knowledgeBasesQuery.isError, `${sensitiveSourceCount} sensitive-classified`)} />
        </AccordionSummary>
        <AccordionDetails>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
            <Typography variant="body2">
              {modelsQuery.isLoading || knowledgeBasesQuery.isLoading
                ? 'Loading data classifications…'
                : modelsQuery.isError || knowledgeBasesQuery.isError
                  ? 'Data classification totals are unavailable.'
                  : `${sensitiveModelCount} AI model${sensitiveModelCount === 1 ? '' : 's'} and ${sensitiveKbCount} knowledge base${sensitiveKbCount === 1 ? '' : 's'} classified Confidential or Restricted.`}
            </Typography>
            <Tooltip title="DataClassification is enforced on both AI Models (external-provider boundary) and Knowledge Bases (RAG context boundary, gated by the Knowledge policy above).">
              <InfoOutlined fontSize="small" color="disabled" />
            </Tooltip>
          </Stack>
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={SmartToyOutlined} title="AI Governance" stat={queryStat(modelsQuery.isLoading, modelsQuery.isError, `${activeModelCount} active models`)} />
        </AccordionSummary>
        <AccordionDetails>
          <Typography variant="body2">
            {modelsQuery.isLoading
              ? 'Loading model configurations…'
              : modelsQuery.isError
                ? 'Model configuration data is unavailable.'
                : `${activeModelCount} of ${models.length} registered model configuration${models.length === 1 ? '' : 's'} active.`}
          </Typography>
          <Button size="small" onClick={() => navigate('/models')} sx={{ mt: 1 }}>
            View AI Models
          </Button>
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={KeyOffOutlined} title="Secrets" stat="Not configured" />
        </AccordionSummary>
        <AccordionDetails>
          <Alert severity="info">
            Credentials are encrypted in the database. For production, provide secrets through the Kubernetes{' '}
            <code>r2wai-secrets</code> resource instead of using defaults.
          </Alert>
        </AccordionDetails>
      </Accordion>

      <Accordion disableGutters variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <SectionHeader icon={HistoryOutlined} title="Audit" />
        </AccordionSummary>
        <AccordionDetails>
          <Typography variant="body2" sx={{ mb: 1 }}>
            Changes and governed tool calls are recorded. Open Monitor to filter the full audit log.
          </Typography>
          <Button size="small" onClick={() => navigate('/monitor?tab=audit')}>
            Open Audit Logs
          </Button>
        </AccordionDetails>
      </Accordion>

      <EditPolicyDialog
        open={!!editingType}
        onClose={() => setEditingType(null)}
        onSubmit={(values) => {
          if (editingType) upsertMutation.mutate({ type: editingType, ...values })
        }}
        type={editingType}
        policy={editingPolicy}
        isSubmitting={upsertMutation.isPending}
      />
    </Box>
  )
}
