import { useState, type MouseEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Box, Button, Menu, MenuItem, Stack, Tab, Tabs, Typography } from '@mui/material'
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown'
import ScienceOutlined from '@mui/icons-material/ScienceOutlined'
import { OverviewTab } from '../components/OverviewTab'
import { ErrorsTab } from '../components/ErrorsTab'
import { AiOperationsTab } from '../components/AiOperationsTab'
import { AuditLogsTab } from '../components/AuditLogsTab'
import { ReportsTab } from '../components/ReportsTab'
import { UsageAnalyticsTab } from '../components/UsageAnalyticsTab'
import { TestCasesTab } from '../components/TestCasesTab'
import { TestHistoryTab } from '../components/TestHistoryTab'

// Ops monitoring — what an admin checks day to day for platform health.
const PRIMARY_TABS = [
  { value: 'overview', label: 'Overview' },
  { value: 'errors', label: 'Errors' },
  { value: 'ai', label: 'AI Operations' },
  { value: 'audit', label: 'Audit Logs' },
  { value: 'usage', label: 'Usage Analytics' },
] as const

// QA regression tooling — a different job (verifying assistant behavior against known cases),
// not ops monitoring. Moved out of the flat tab row into its own menu (Phase 9) rather than
// deleted or hidden — still one click away, just no longer implying it's the same kind of thing
// as Errors/Audit.
const SECONDARY_TABS = [
  { value: 'reports', label: 'Reports' },
  { value: 'testcases', label: 'Test Cases' },
  { value: 'testhistory', label: 'Test History' },
] as const

type TabValue = (typeof PRIMARY_TABS)[number]['value'] | (typeof SECONDARY_TABS)[number]['value']

export function MonitorPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedTab = searchParams.get('tab')
  const tab: TabValue = [...PRIMARY_TABS, ...SECONDARY_TABS].some((item) => item.value === requestedTab)
    ? requestedTab as TabValue
    : 'overview'
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)

  const isSecondaryActive = SECONDARY_TABS.some((t) => t.value === tab)
  const activeSecondaryLabel = SECONDARY_TABS.find((t) => t.value === tab)?.label

  function openMenu(e: MouseEvent<HTMLElement>) {
    setMenuAnchor(e.currentTarget)
  }

  function selectTab(value: TabValue) {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      if (value === 'overview') next.delete('tab')
      else next.set('tab', value)
      return next
    }, { replace: true })
  }

  function selectSecondary(value: TabValue) {
    selectTab(value)
    setMenuAnchor(null)
  }

  return (
    <Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Monitor
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Platform health, errors, AI usage, and the audit trail.
        </Typography>
      </Box>

      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Tabs
          value={isSecondaryActive ? false : tab}
          onChange={(_, v) => selectTab(v as TabValue)}
          variant="scrollable"
          scrollButtons="auto"
          sx={{ minWidth: 0, flexGrow: 1 }}
        >
          {PRIMARY_TABS.map((t) => (
            <Tab key={t.value} label={t.label} value={t.value} />
          ))}
        </Tabs>
        <Button
          size="small"
          aria-label="QA and Testing menu"
          color={isSecondaryActive ? 'primary' : 'inherit'}
          startIcon={<ScienceOutlined fontSize="small" />}
          endIcon={<ArrowDropDownIcon />}
          onClick={openMenu}
          sx={{ mr: 1, flexShrink: 0 }}
        >
          {isSecondaryActive ? activeSecondaryLabel : 'QA & Testing'}
        </Button>
        <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)}>
          {SECONDARY_TABS.map((t) => (
            <MenuItem key={t.value} selected={tab === t.value} onClick={() => selectSecondary(t.value)}>
              {t.label}
            </MenuItem>
          ))}
        </Menu>
      </Stack>

      {tab === 'overview' && <OverviewTab />}
      {tab === 'errors' && <ErrorsTab />}
      {tab === 'ai' && <AiOperationsTab />}
      {tab === 'audit' && (
        <AuditLogsTab
          initialEntityType={searchParams.get('entityType') ?? undefined}
          initialEntityId={searchParams.get('entityId') ?? undefined}
        />
      )}
      {tab === 'usage' && <UsageAnalyticsTab />}
      {tab === 'reports' && <ReportsTab />}
      {tab === 'testcases' && <TestCasesTab />}
      {tab === 'testhistory' && <TestHistoryTab />}
    </Box>
  )
}
