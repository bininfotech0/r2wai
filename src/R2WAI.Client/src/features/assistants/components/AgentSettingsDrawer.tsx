import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  MenuItem,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import CloseIcon from '@mui/icons-material/Close'
import { Link as RouterLink } from 'react-router-dom'
import { queryKeys } from '../../../lib/api/queryKeys'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { updateAssistant } from '../api'
import { buildFullUpdatePayload, type AssistantDto, type BehaviorSettings } from '../types'
import { AssistantApiAccessSection } from './AssistantApiAccessSection'

const RESPONSE_STYLES: NonNullable<BehaviorSettings['responseStyle']>[] = ['Professional', 'Friendly', 'Concise', 'Detailed']
const ANSWER_LENGTHS: NonNullable<BehaviorSettings['answerLength']>[] = ['Brief', 'Balanced', 'Thorough']

const SECTIONS = ['Generative AI', 'Security', 'Advanced'] as const
type Section = (typeof SECTIONS)[number]

function parseSettings(raw: string | null): BehaviorSettings {
  if (!raw) return {}
  try {
    return JSON.parse(raw) as BehaviorSettings
  } catch {
    return {} // malformed/legacy settings: start fresh rather than crash the drawer
  }
}

interface AgentSettingsDrawerProps {
  open: boolean
  onClose: () => void
  assistant: AssistantDto
  onDelete: () => void
}

/**
 * The agent's Settings, opened from the page header as in Copilot Studio: how it answers
 * (Generative AI), what governs it (Security), and API access plus deletion (Advanced). Mounted
 * only while open, so each opening starts from the saved settings.
 */
export function AgentSettingsDrawer({ open, onClose, assistant, onDelete }: AgentSettingsDrawerProps) {
  const [section, setSection] = useState<Section>('Generative AI')

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      slotProps={{ paper: { sx: { width: { xs: '100%', sm: 520 }, display: 'flex', flexDirection: 'column' } } }}
    >
      <Stack direction="row" sx={{ alignItems: 'center', px: 3, pt: 2 }}>
        <Typography variant="h6" component="h2" sx={{ flex: 1, fontWeight: 650 }}>
          Settings
        </Typography>
        <IconButton aria-label="Close settings" onClick={onClose}>
          <CloseIcon />
        </IconButton>
      </Stack>
      <Tabs value={section} onChange={(_, v: Section) => setSection(v)} sx={{ px: 2, borderBottom: 1, borderColor: 'divider' }}>
        {SECTIONS.map((s) => <Tab key={s} value={s} label={s} />)}
      </Tabs>
      <Box sx={{ flex: 1, overflowY: 'auto', p: 3 }}>
        {open && section === 'Generative AI' && <GenerativeAiSettings assistant={assistant} onSaved={onClose} />}
        {section === 'Security' && (
          <Stack spacing={2}>
            <Typography variant="body2" color="text.secondary">
              This agent follows your organisation&apos;s policies: personal-data redaction, the tool risk
              limit, and approval rules. Every tool call passes the Tool Gateway&apos;s checks whatever is set
              here, and is recorded in Activity.
            </Typography>
            <Button component={RouterLink} to="/security" variant="outlined" size="small" sx={{ alignSelf: 'flex-start' }}>
              Open Security & Policies
            </Button>
          </Stack>
        )}
        {section === 'Advanced' && (
          <Stack spacing={3}>
            <AssistantApiAccessSection assistant={assistant} />
            <Divider />
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 650 }}>Delete agent</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1.5 }}>
                Removes the agent and stops every channel that uses it. This cannot be undone.
              </Typography>
              <Button color="error" variant="outlined" onClick={onDelete}>
                Delete agent
              </Button>
            </Box>
          </Stack>
        )}
      </Box>
    </Drawer>
  )
}

function GenerativeAiSettings({ assistant, onSaved }: { assistant: AssistantDto; onSaved: () => void }) {
  const { notify } = useSnackbar()
  const queryClient = useQueryClient()
  const [settings, setSettings] = useState<BehaviorSettings>(() => parseSettings(assistant.settings))
  const [showAdvanced, setShowAdvanced] = useState(false)

  const mutation = useMutation({
    mutationFn: () =>
      updateAssistant(assistant.id, buildFullUpdatePayload(assistant, { settings: JSON.stringify(settings) })),
    onSuccess: () => {
      notify('Settings saved', 'success')
      void queryClient.invalidateQueries({ queryKey: queryKeys.assistants.detail(assistant.id) })
      onSaved()
    },
    onError: () => notify('Failed to save settings', 'error'),
  })

  return (
    <Stack spacing={2.5}>
      <TextField
        select
        label="Response style"
        value={settings.responseStyle ?? 'Professional'}
        onChange={(e) => setSettings((s) => ({ ...s, responseStyle: e.target.value as BehaviorSettings['responseStyle'] }))}
      >
        {RESPONSE_STYLES.map((v) => <MenuItem key={v} value={v}>{v}</MenuItem>)}
      </TextField>
      <TextField
        select
        label="Answer length"
        value={settings.answerLength ?? 'Balanced'}
        onChange={(e) => setSettings((s) => ({ ...s, answerLength: e.target.value as BehaviorSettings['answerLength'] }))}
      >
        {ANSWER_LENGTHS.map((v) => <MenuItem key={v} value={v}>{v}</MenuItem>)}
      </TextField>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Switch
          checked={settings.citationsEnabled ?? true}
          onChange={(e) => setSettings((s) => ({ ...s, citationsEnabled: e.target.checked }))}
          slotProps={{ input: { 'aria-label': 'Cite sources in answers' } }}
        />
        <Typography variant="body2">Cite sources in answers</Typography>
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Switch
          checked={settings.askClarification ?? true}
          onChange={(e) => setSettings((s) => ({ ...s, askClarification: e.target.checked }))}
          slotProps={{ input: { 'aria-label': 'Ask clarifying questions' } }}
        />
        <Typography variant="body2">Ask clarifying questions</Typography>
      </Stack>

      {assistant.knowledgeBaseId && (
        <Box>
          <TextField
            select
            fullWidth
            label="Knowledge retrieval"
            value={settings.retrievalMode ?? 'Standard'}
            onChange={(e) => setSettings((s) => ({ ...s, retrievalMode: e.target.value as BehaviorSettings['retrievalMode'] }))}
          >
            <MenuItem value="Standard">Standard — one search, fastest</MenuItem>
            <MenuItem value="Agentic">Agentic — retries with a rewritten query if the first search is weak</MenuItem>
          </TextField>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
            {settings.retrievalMode === 'Agentic'
              ? "Adds up to one extra search (a rewritten query) when the first pass doesn't find a confident match, and tells the agent to say so rather than guess if it still can't find one."
              : 'A single knowledge search per message. Switch to Agentic for questions that often need a differently-worded search.'}
          </Typography>
        </Box>
      )}

      <Button size="small" onClick={() => setShowAdvanced((v) => !v)} sx={{ alignSelf: 'flex-start' }}>
        {showAdvanced ? 'Hide model parameters' : 'Model parameters ▸'}
      </Button>
      {showAdvanced && (
        <Stack spacing={2}>
          <TextField
            label="Temperature"
            type="number"
            slotProps={{ htmlInput: { step: 0.1, min: 0, max: 2 } }}
            value={settings.temperature ?? 0.7}
            onChange={(e) => setSettings((s) => ({ ...s, temperature: Number(e.target.value) }))}
          />
          <TextField
            label="Maximum output tokens"
            type="number"
            value={settings.maxOutputTokens ?? 2048}
            onChange={(e) => setSettings((s) => ({ ...s, maxOutputTokens: Number(e.target.value) }))}
          />
        </Stack>
      )}

      <Box>
        <Button variant="contained" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
          {mutation.isPending ? 'Saving…' : 'Save settings'}
        </Button>
      </Box>
    </Stack>
  )
}
