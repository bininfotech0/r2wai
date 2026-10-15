import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Grid,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DownloadOutlined from '@mui/icons-material/DownloadOutlined'
import PsychologyOutlined from '@mui/icons-material/PsychologyOutlined'
import PushPin from '@mui/icons-material/PushPin'
import PushPinOutlined from '@mui/icons-material/PushPinOutlined'
import SearchIcon from '@mui/icons-material/Search'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { LoadingSkeleton } from '../../../components/LoadingSkeleton'
import { PageHeader } from '../../../components/PageHeader'
import { SectionCard } from '../../../components/SectionCard'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { RowActionsMenu } from '../../../components/data/RowActionsMenu'
import { apiErrorMessage, describeApiError } from '../../../lib/api/fetchJson'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { downloadBlob } from '../../monitor/downloadBlob'
import {
  MEMORY_QUERY_KEY,
  approveMemory,
  clearMemory,
  createMemory,
  deleteMemory,
  exportMemory,
  getMemoryOverview,
  updateMemory,
  updateMemorySettings,
} from '../api'
import { MemoryDialog } from '../components/MemoryDialog'
import {
  CATEGORIES,
  SOURCE_LABELS,
  categoryLabel,
  partitionKey,
  type MemoryOverview,
  type MemoryPartition,
  type MemorySettings,
  type SaveMemoryInput,
  type UserMemoryDto,
} from '../types'

type Confirm =
  | { kind: 'delete'; memory: UserMemoryDto }
  | { kind: 'clear'; partition: string; title: string; message: string }

// Borders rather than Stack's divider prop: a list may only contain list items.
const ROW_DIVIDERS = { '& > [role="listitem"] + [role="listitem"]': { borderTop: 1, borderColor: 'divider' } } as const

const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })

function formatWhen(iso: string): string {
  const seconds = (new Date(iso).getTime() - Date.now()) / 1000
  const steps: [Intl.RelativeTimeFormatUnit, number][] = [['day', 86400], ['hour', 3600], ['minute', 60]]
  for (const [unit, size] of steps) {
    if (Math.abs(seconds) >= size) return relative.format(Math.round(seconds / size), unit)
  }
  return 'just now'
}

function toInput(memory: UserMemoryDto, patch: Partial<SaveMemoryInput>): SaveMemoryInput {
  return {
    category: memory.category,
    key: memory.key,
    content: memory.content,
    isPinned: memory.isPinned,
    paused: memory.status === 'Paused',
    ...patch,
  }
}

/**
 * The user's own memory: what agents may remember about them between conversations, who wrote
 * each entry (them, the opt-in learning pass, or an agent in its own partition), and the switches
 * for all of it. Nothing here is visible to anyone else, administrators included.
 */
export function MemoryPage() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const query = useQuery({ queryKey: MEMORY_QUERY_KEY, queryFn: getMemoryOverview })
  const [tab, setTab] = useState('personal')
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<{ memory: UserMemoryDto | null } | null>(null)
  const [dialogError, setDialogError] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<Confirm | null>(null)

  const invalidate = () => queryClient.invalidateQueries({ queryKey: MEMORY_QUERY_KEY })

  const settingsMutation = useMutation({
    mutationFn: updateMemorySettings,
    onMutate: (settings) => {
      const previous = queryClient.getQueryData<MemoryOverview>(MEMORY_QUERY_KEY)
      queryClient.setQueryData<MemoryOverview>(MEMORY_QUERY_KEY, (old) => (old ? { ...old, settings } : old))
      return { previous }
    },
    onError: (_error, _settings, context) => {
      if (context?.previous) queryClient.setQueryData(MEMORY_QUERY_KEY, context.previous)
      notify('Failed to save memory settings', 'error')
    },
  })

  const saveMutation = useMutation({
    mutationFn: ({ id, input }: { id: string | null; input: SaveMemoryInput }) =>
      id ? updateMemory(id, input) : createMemory(input),
    onSuccess: (_saved, { id }) => {
      notify(id ? 'Memory updated' : 'Memory added', 'success')
      setEditing(null)
      if (!id) setTab('personal')
      void invalidate()
    },
    onError: (error) => setDialogError(apiErrorMessage(error, 'Failed to save memory')),
  })

  const quickUpdate = useMutation({
    mutationFn: ({ memory, patch }: { memory: UserMemoryDto; patch: Partial<SaveMemoryInput> }) =>
      updateMemory(memory.id, toInput(memory, patch)),
    onSuccess: () => void invalidate(),
    onError: (error) => notify(apiErrorMessage(error, 'Failed to update memory'), 'error'),
  })

  const approveMutation = useMutation({
    mutationFn: approveMemory,
    onSuccess: () => {
      notify('Memory approved', 'success')
      void invalidate()
    },
    onError: () => notify('Failed to approve memory', 'error'),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteMemory,
    onSuccess: () => {
      notify('Memory deleted', 'success')
      setConfirm(null)
      void invalidate()
    },
    onError: () => notify('Failed to delete memory', 'error'),
  })

  const clearMutation = useMutation({
    mutationFn: clearMemory,
    onSuccess: ({ removed }) => {
      notify(removed === 1 ? '1 memory deleted' : `${removed} memories deleted`, 'success')
      setConfirm(null)
      setTab('personal')
      void invalidate()
    },
    onError: () => notify('Failed to delete memories', 'error'),
  })

  const exportMutation = useMutation({
    mutationFn: exportMemory,
    onSuccess: (blob) => downloadBlob(blob, `r2wai-memory-${new Date().toISOString().slice(0, 10)}.json`),
    onError: () => notify('Failed to export memory', 'error'),
  })

  const data = query.data
  const pending = useMemo(() => data?.items.filter((m) => m.status === 'PendingReview') ?? [], [data])
  const partitions = data?.partitions ?? []
  const activePartition = partitions.find((p) => partitionKey(p.assistantId) === tab) ?? partitions[0]
  const visible = useMemo(() => {
    if (!data || !activePartition) return []
    const needle = search.trim().toLowerCase()
    return data.items.filter((m) =>
      m.assistantId === activePartition.assistantId
      && m.status !== 'PendingReview'
      && (!needle || m.content.toLowerCase().includes(needle) || (m.key ?? '').includes(needle)))
  }, [data, activePartition, search])

  function openEditor(memory: UserMemoryDto | null) {
    setDialogError(null)
    setEditing({ memory })
  }

  function rowHandlers(memory: UserMemoryDto) {
    return {
      onEdit: () => openEditor(memory),
      onTogglePin: () => quickUpdate.mutate({ memory, patch: { isPinned: !memory.isPinned } }),
      onTogglePause: () => quickUpdate.mutate({ memory, patch: { paused: memory.status !== 'Paused' } }),
      onDelete: () => setConfirm({ kind: 'delete', memory }),
      onApprove: () => approveMutation.mutate(memory.id),
    }
  }

  return (
    <Box>
      <PageHeader
        title="Memory"
        description="What agents remember about you between conversations. Only you can see and change it."
        actions={
          <>
            <Button
              startIcon={<DownloadOutlined />}
              disabled={!data || exportMutation.isPending}
              onClick={() => exportMutation.mutate()}
             loading={exportMutation.isPending}>
              Export
            </Button>
            <Button variant="contained" startIcon={<AddIcon />} disabled={!data} onClick={() => openEditor(null)}>
              Add memory
            </Button>
          </>
        }
      />

      {query.isLoading && <LoadingSkeleton variant="table" rows={6} />}
      {query.isError && <ErrorState {...describeApiError(query.error, 'Unable to load your memory.')} onRetry={() => void query.refetch()} />}

      {data && (
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, md: 8 }}>
            <Stack spacing={2}>
              {!data.settings.memoryEnabled && (
                <Alert severity="info">
                  Memory is off. Agents don&apos;t read or save anything about you until you turn it back on. What is saved here is kept.
                </Alert>
              )}

              {pending.length > 0 && (
                <SectionCard
                  title={`Waiting for your review (${pending.length})`}
                  description="Learned from your chats. No agent sees these until you approve them."
                  action={
                    <Button
                      size="small"
                      color="error"
                      onClick={() => setConfirm({
                        kind: 'clear',
                        partition: 'pending',
                        title: 'Discard all suggestions?',
                        message: `This discards ${pending.length} learned ${pending.length === 1 ? 'memory' : 'memories'} waiting for review.`,
                      })}
                    >
                      Discard all
                    </Button>
                  }
                >
                  <Stack role="list" aria-label="Waiting for review" sx={ROW_DIVIDERS}>
                    {pending.map((m) => <MemoryRow key={m.id} memory={m} {...rowHandlers(m)} />)}
                  </Stack>
                </SectionCard>
              )}

              <Paper variant="outlined">
                <Tabs
                  value={activePartition ? partitionKey(activePartition.assistantId) : 'personal'}
                  onChange={(_, value: string) => { setTab(value); setSearch('') }}
                  variant="scrollable"
                  scrollButtons="auto"
                  aria-label="Memory sections"
                  sx={{ px: 1, borderBottom: 1, borderColor: 'divider' }}
                >
                  {partitions.map((p) => (
                    <Tab
                      key={partitionKey(p.assistantId)}
                      value={partitionKey(p.assistantId)}
                      label={`${partitionLabel(p)} (${p.count - p.pendingCount})`}
                    />
                  ))}
                </Tabs>

                {activePartition && (
                  <Box sx={{ p: { xs: 2, sm: 2.5 } }}>
                    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ alignItems: { sm: 'center' }, mb: 1 }}>
                      <Typography variant="body2" color="text.secondary" sx={{ flex: 1 }}>
                        {activePartition.assistantId
                          ? `Notes ${partitionLabel(activePartition)} keeps for itself. Only ${partitionLabel(activePartition)} reads them.`
                          : 'Every agent with memory switched on can use these.'}
                      </Typography>
                      {activePartition.count - activePartition.pendingCount > 5 && (
                        <TextField
                          size="small"
                          placeholder="Search memory"
                          value={search}
                          onChange={(e) => setSearch(e.target.value)}
                          slotProps={{
                            input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> },
                            htmlInput: { 'aria-label': 'Search memory' },
                          }}
                          sx={{ width: { xs: '100%', sm: 220 } }}
                        />
                      )}
                    </Stack>

                    {visible.length === 0 ? (
                      search.trim() ? (
                        <Typography variant="body2" color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
                          Nothing matches &ldquo;{search.trim()}&rdquo;.
                        </Typography>
                      ) : activePartition.assistantId ? (
                        <Typography variant="body2" color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
                          This agent has no notes about you.
                        </Typography>
                      ) : (
                        <EmptyState
                          icon={PsychologyOutlined}
                          title="Nothing remembered yet"
                          description="Add things you'd like every agent to know, like your role or how you prefer answers. You can also let agents learn them from your chats."
                          actionLabel="Add memory"
                          onAction={() => openEditor(null)}
                        />
                      )
                    ) : (
                      <MemoryGroups items={visible} rowHandlers={rowHandlers} />
                    )}

                    {activePartition.count - activePartition.pendingCount > 0 && (
                      <Box sx={{ mt: 2 }}>
                        <Button
                          size="small"
                          color="error"
                          onClick={() => setConfirm({
                            kind: 'clear',
                            partition: partitionKey(activePartition.assistantId),
                            title: activePartition.assistantId ? `Clear ${partitionLabel(activePartition)}'s notes?` : 'Clear personal memory?',
                            message: activePartition.assistantId
                              ? `This deletes every note ${partitionLabel(activePartition)} keeps about you. It cannot be undone.`
                              : 'This deletes every personal memory, including ones waiting for review. Agent notes are kept. It cannot be undone.',
                          })}
                        >
                          {activePartition.assistantId ? 'Clear these notes' : 'Clear personal memory'}
                        </Button>
                      </Box>
                    )}
                  </Box>
                )}
              </Paper>
            </Stack>
          </Grid>

          <Grid size={{ xs: 12, md: 4 }}>
            <Stack spacing={2}>
              <SettingsCard
                settings={data.settings}
                onChange={(settings) => settingsMutation.mutate(settings)}
              />
              <Paper variant="outlined" sx={{ p: { xs: 2, sm: 2.5 } }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 650 }}>Storage</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                  {data.items.length} of {data.limit} memories used.
                </Typography>
                <Divider sx={{ my: 2 }} />
                <Typography variant="subtitle2" sx={{ fontWeight: 650 }}>Delete all memory</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1.5 }}>
                  Removes everything here, including every agent&apos;s notes. Your settings are kept.
                </Typography>
                <Button
                  variant="outlined"
                  color="error"
                  size="small"
                  disabled={data.items.length === 0}
                  onClick={() => setConfirm({
                    kind: 'clear',
                    partition: 'all',
                    title: 'Delete all memory?',
                    message: `This deletes all ${data.items.length} memories, including every agent's notes about you. It cannot be undone.`,
                  })}
                >
                  Delete all memory
                </Button>
              </Paper>
            </Stack>
          </Grid>
        </Grid>
      )}

      {editing && (
        <MemoryDialog
          memory={editing.memory}
          isSubmitting={saveMutation.isPending}
          error={dialogError}
          onClose={() => setEditing(null)}
          onSubmit={(input) => {
            setDialogError(null)
            saveMutation.mutate({ id: editing.memory?.id ?? null, input })
          }}
        />
      )}

      <ConfirmDeleteDialog
        open={confirm !== null}
        onClose={() => setConfirm(null)}
        title={confirm?.kind === 'delete' ? 'Delete memory?' : confirm?.title}
        message={confirm?.kind === 'delete'
          ? `"${confirm.memory.content}" will be forgotten by every agent. This cannot be undone.`
          : confirm?.message ?? ''}
        isDeleting={deleteMutation.isPending || clearMutation.isPending}
        onConfirm={() => {
          if (confirm?.kind === 'delete') deleteMutation.mutate(confirm.memory.id)
          else if (confirm?.kind === 'clear') clearMutation.mutate(confirm.partition)
        }}
      />
    </Box>
  )
}

function partitionLabel(partition: MemoryPartition): string {
  if (!partition.assistantId) return 'Personal'
  return partition.assistantName ?? 'Removed agent'
}

type RowHandlers = {
  onEdit: () => void
  onTogglePin: () => void
  onTogglePause: () => void
  onDelete: () => void
  onApprove: () => void
}

function MemoryGroups({ items, rowHandlers }: { items: UserMemoryDto[]; rowHandlers: (m: UserMemoryDto) => RowHandlers }) {
  return (
    <Stack spacing={2}>
      {CATEGORIES.map(({ value }) => {
        const group = items.filter((m) => m.category === value)
        if (group.length === 0) return null
        return (
          <Box key={value} component="section" aria-label={categoryLabel(value)}>
            <Typography variant="overline" color="text.secondary" sx={{ display: 'block', lineHeight: 2 }}>
              {categoryLabel(value)}
            </Typography>
            <Stack role="list" aria-label={categoryLabel(value)} sx={ROW_DIVIDERS}>
              {group.map((m) => <MemoryRow key={m.id} memory={m} {...rowHandlers(m)} />)}
            </Stack>
          </Box>
        )
      })}
    </Stack>
  )
}

function MemoryRow({ memory, onEdit, onTogglePin, onTogglePause, onDelete, onApprove }: { memory: UserMemoryDto } & RowHandlers) {
  const pendingReview = memory.status === 'PendingReview'
  const paused = memory.status === 'Paused'
  const when = memory.lastUsedAt
    ? `used ${formatWhen(memory.lastUsedAt)}`
    : `saved ${formatWhen(memory.modifiedAt ?? memory.createdAt)}`

  return (
    <Stack role="listitem" aria-label={memory.content} direction="row" spacing={1} sx={{ py: 1.25, alignItems: 'flex-start' }}>
      <Box sx={{ flex: 1, minWidth: 0, opacity: paused ? 0.6 : 1 }}>
        <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }}>{memory.content}</Typography>
        <Stack direction="row" spacing={1} useFlexGap sx={{ mt: 0.5, alignItems: 'center', flexWrap: 'wrap' }}>
          {memory.key && <Chip size="small" variant="outlined" label={memory.key} />}
          {pendingReview && <Chip size="small" variant="outlined" label={categoryLabel(memory.category)} />}
          {paused && <Chip size="small" label="Paused" />}
          <Typography variant="caption" color="text.secondary">
            {SOURCE_LABELS[memory.source]} · {when}
          </Typography>
        </Stack>
      </Box>
      {pendingReview ? (
        <>
          <Button size="small" variant="outlined" onClick={onApprove}>Approve</Button>
          <RowActionsMenu
            label={memory.content}
            actions={[
              { label: 'Edit', onSelect: onEdit },
              { label: 'Discard', onSelect: onDelete, destructive: true },
            ]}
          />
        </>
      ) : (
        <>
          <Tooltip title={memory.isPinned ? 'Unpin' : 'Pin'}>
            <IconButton size="small" aria-label={memory.isPinned ? 'Unpin memory' : 'Pin memory'} aria-pressed={memory.isPinned} onClick={onTogglePin}>
              {memory.isPinned ? <PushPin fontSize="small" color="primary" /> : <PushPinOutlined fontSize="small" />}
            </IconButton>
          </Tooltip>
          <RowActionsMenu
            label={memory.content}
            actions={[
              { label: 'Edit', onSelect: onEdit },
              { label: paused ? 'Resume' : 'Pause', onSelect: onTogglePause },
              { label: 'Delete', onSelect: onDelete, destructive: true },
            ]}
          />
        </>
      )}
    </Stack>
  )
}

function SettingsCard({ settings, onChange }: { settings: MemorySettings; onChange: (settings: MemorySettings) => void }) {
  const set = (patch: Partial<MemorySettings>) => onChange({ ...settings, ...patch })
  return (
    <SectionCard title="Settings" description="Changes apply from your next message.">
      <Stack spacing={1.5}>
        <SettingSwitch
          label="Use memory in chats"
          description="Agents can use what is saved here to personalise their answers."
          checked={settings.memoryEnabled}
          onChange={(memoryEnabled) => set({ memoryEnabled })}
        />
        <SettingSwitch
          label="Learn from my chats"
          description="After a chat, pick out lasting preferences and facts you mention about yourself."
          checked={settings.autoLearnEnabled}
          disabled={!settings.memoryEnabled}
          onChange={(autoLearnEnabled) => set({ autoLearnEnabled })}
        />
        <Box sx={{ pl: 3 }}>
          <SettingSwitch
            label="Review before saving"
            description="Learned memories wait for your approval before any agent sees them."
            checked={settings.reviewLearned}
            disabled={!settings.memoryEnabled || !settings.autoLearnEnabled}
            onChange={(reviewLearned) => set({ reviewLearned })}
          />
        </Box>
        <SettingSwitch
          label="Let agents keep notes"
          description="Agents set up to keep notes can save them in their own section. You can delete any of them."
          checked={settings.agentNotesEnabled}
          disabled={!settings.memoryEnabled}
          onChange={(agentNotesEnabled) => set({ agentNotesEnabled })}
        />
      </Stack>
    </SectionCard>
  )
}

function SettingSwitch({ label, description, checked, disabled, onChange }: {
  label: string
  description: string
  checked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start', opacity: disabled ? 0.6 : 1 }}>
      <Switch
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
        slotProps={{ input: { 'aria-label': label } }}
        sx={{ mt: -0.5 }}
      />
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>{label}</Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.45 }}>{description}</Typography>
      </Box>
    </Stack>
  )
}
