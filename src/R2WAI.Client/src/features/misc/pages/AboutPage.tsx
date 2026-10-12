import { useEffect, useState, type ReactNode } from 'react'
import { Alert, Box, Button, Chip, Grid, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableRow, Typography } from '@mui/material'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import MonitorHeartOutlinedIcon from '@mui/icons-material/MonitorHeartOutlined'
import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined'
import RefreshIcon from '@mui/icons-material/Refresh'
import { PageHeader } from '../../../components/PageHeader'
import { useDocumentTitle } from '../../../lib/useDocumentTitle'

interface HealthCheckEntry {
  name: string
  status: string
  description?: string | null
}

interface HealthReport {
  status: string
  duration: number
  checks: HealthCheckEntry[]
}

// Names matched to roleNav.ts's current primary nav sections (ADR-0002).
const FEATURES = [
  { name: 'Workspaces', description: 'Group agents, knowledge, automations, chatbots and connections by team or use case; connect external systems via REST/OpenAPI, integrations, and MCP servers' },
  { name: 'Agents', description: 'Create domain-specific AI agents with configurable models, and test them in the Playground' },
  { name: 'Knowledge', description: 'Upload documents, build knowledge bases, and enable semantic search with citations' },
  { name: 'Automations', description: 'Design and run multi-step workflows across agents and connected systems' },
  { name: 'Publish', description: 'Deploy chatbots to the website widget, WhatsApp, and other channels' },
  { name: 'Activity', description: 'Executions, confirmations, and monitoring across agents and connected systems' },
  { name: 'Settings', description: 'Users, roles, AI models, security policies, and API & SDK access' },
]

const TECH_STACK = [
  ['Frontend', 'React 19 + TypeScript + MUI 9 (Vite)'],
  ['Backend Runtime', '.NET 10 (ASP.NET Core)'],
  ['Database', 'PostgreSQL + EF Core 10'],
  ['Vector Store', 'pgvector'],
  ['AI Orchestration', 'Microsoft Agent Framework + Semantic Kernel'],
  ['Tool Protocol', 'Model Context Protocol (MCP)'],
  ['Workflow Engine', 'Elsa 3'],
  ['CQRS', 'MediatR'],
  ['Realtime', 'SignalR'],
  ['Cache', 'Redis'],
  ['Observability', 'OpenTelemetry + Serilog'],
]

function healthColor(status: string): 'success' | 'warning' | 'error' | 'default' {
  if (status === 'Healthy') return 'success'
  if (status === 'Degraded') return 'warning'
  if (status === 'Unhealthy') return 'error'
  return 'default'
}

export function AboutPage() {
  useDocumentTitle('About · R2WAI Studio')
  const [health, setHealth] = useState<HealthReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [healthError, setHealthError] = useState<string | null>(null)

  async function loadHealth() {
    setLoading(true)
    setHealthError(null)
    try {
      const response = await fetch('/api-health')
      if (response.ok) {
        setHealth((await response.json()) as HealthReport)
      } else {
        setHealth(null)
        setHealthError("System health couldn't be loaded. Refresh to try again.")
      }
    } catch {
      setHealth(null)
      setHealthError("System health couldn't be reached. Check your connection and try again.")
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadHealth()
  }, [])

  return (
    <Box>
      <PageHeader title="About R2WAI Studio" description="Platform information, health, and configuration." />

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper variant="outlined" sx={{ p: 2.5, height: '100%' }}>
            <SectionHeader icon={<InfoOutlinedIcon fontSize="small" color="primary" />} title="Feature Modules" />
            <Stack spacing={1}>
              {FEATURES.map((f) => (
                <Stack key={f.name} direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
                  <Box sx={{ flexGrow: 1 }}>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                      {f.name}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {f.description}
                    </Typography>
                  </Box>
                  <Chip size="small" label="Enabled" color="success" variant="outlined" />
                </Stack>
              ))}
            </Stack>
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Paper variant="outlined" sx={{ p: 2.5, height: '100%' }}>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
              <SectionHeader icon={<MonitorHeartOutlinedIcon fontSize="small" color="success" />} title="System Health" noMargin />
              <Button size="small" variant="outlined" startIcon={<RefreshIcon />} disabled={loading} onClick={() => void loadHealth()}>
                {loading ? 'Checking…' : 'Refresh'}
              </Button>
            </Stack>

            {health && (
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
                <Chip size="small" label={`Overall: ${health.status}`} color={healthColor(health.status)} />
                {health.duration > 0 && (
                  <Typography variant="caption" color="text.secondary">
                    ({health.duration.toFixed(0)} ms)
                  </Typography>
                )}
              </Stack>
            )}

            {loading && !health && !healthError ? (
              <Stack spacing={1} role="status" aria-label="Checking system health">
                <Skeleton variant="rounded" height={38} />
                <Skeleton variant="rounded" height={38} />
              </Stack>
            ) : healthError ? (
              <Alert severity="error">{healthError}</Alert>
            ) : (
              <Stack spacing={1}>
              {(health?.checks.length ?? 0) === 0 ? (
                <Typography variant="caption" color="text.secondary">
                  No health checks are currently available.
                </Typography>
              ) : (
                health!.checks.map((c) => (
                  <Stack
                    key={c.name}
                    direction="row"
                    sx={{ alignItems: 'center', justifyContent: 'space-between', p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1.5 }}
                  >
                    <Typography variant="body2">{c.name}</Typography>
                    <Chip size="small" label={c.status} color={healthColor(c.status)} variant="outlined" />
                  </Stack>
                ))
              )}
              </Stack>
            )}
          </Paper>
        </Grid>

        <Grid size={12}>
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <SectionHeader icon={<CodeOutlinedIcon fontSize="small" color="primary" />} title="Technology Stack" />
            <Table size="small">
              <TableBody>
                {TECH_STACK.map(([category, tech]) => (
                  <TableRow key={category}>
                    <TableCell sx={{ fontWeight: 600, border: 0, width: { xs: '38%', sm: 200 }, whiteSpace: { xs: 'normal', sm: 'nowrap' } }}>{category}</TableCell>
                    <TableCell sx={{ border: 0, overflowWrap: 'anywhere' }}>{tech}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  )
}

function SectionHeader({ icon, title, noMargin }: { icon: ReactNode; title: string; noMargin?: boolean }) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: noMargin ? 0 : 1.5 }}>
      {icon}
      <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
        {title}
      </Typography>
    </Stack>
  )
}
