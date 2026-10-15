import { Alert, Link, Stack, TextField } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'
import {
  SCHEDULE_PRESETS,
  browserTimeZone,
  formatSchedule,
  looksLikeCron,
  parseTrigger,
  type TriggerKind,
} from '../trigger'

interface TriggerEditorProps {
  value: string | null
  onChange: (next: string | null) => void
}

const CUSTOM = '__custom__'

/** "How it starts" — only triggers the platform actually fires (see ../trigger.ts). */
export function TriggerEditor({ value, onChange }: TriggerEditorProps) {
  const parsed = parseTrigger(value)
  const selectedKind: Exclude<TriggerKind, 'unsupported'> | '' = parsed.kind === 'unsupported' ? '' : parsed.kind
  const cron = parsed.cron ?? ''
  const timeZone = parsed.timeZone ?? browserTimeZone()
  const preset = SCHEDULE_PRESETS.find((p) => p.cron === cron)?.cron ?? CUSTOM
  const cronInvalid = parsed.kind === 'schedule' && !looksLikeCron(cron)

  function selectKind(kind: string) {
    if (kind === 'webhook') onChange('Webhook')
    else if (kind === 'schedule') onChange(formatSchedule(SCHEDULE_PRESETS[2].cron, browserTimeZone()))
    else onChange(null)
  }

  return (
    <Stack spacing={1.5} sx={{ mt: 1 }}>
      {parsed.kind === 'unsupported' && (
        <Alert severity="warning">
          The saved trigger “{value}” never started a run — event triggers like that were never connected to
          anything and have been removed. Choose how this automation should start; saving without a choice sets it to
          Manual.
        </Alert>
      )}
      <TextField
        select
        fullWidth
        size="small"
        label="Trigger"
        value={selectedKind}
        onChange={(e) => selectKind(e.target.value)}
        slotProps={{ select: { native: true } }}
      >
        {selectedKind === '' && <option value="">Choose a trigger</option>}
        <option value="manual">Manual — run it yourself, or let an agent start it</option>
        <option value="webhook">Webhook — another system calls a URL</option>
        <option value="schedule">Schedule — runs at set times</option>
      </TextField>

      {parsed.kind === 'webhook' && (
        <Alert severity="info">
          Create the webhook URL under{' '}
          <Link component={RouterLink} to="/developer">
            API &amp; SDK → Webhooks
          </Link>{' '}
          and link it to this automation. Webhooks only start published automations.
        </Alert>
      )}

      {parsed.kind === 'schedule' && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          <TextField
            select
            size="small"
            label="Runs"
            value={preset}
            onChange={(e) => {
              if (e.target.value !== CUSTOM) onChange(formatSchedule(e.target.value, timeZone))
            }}
            slotProps={{ select: { native: true } }}
            sx={{ minWidth: { sm: 220 } }}
          >
            {SCHEDULE_PRESETS.map((p) => (
              <option key={p.cron} value={p.cron}>
                {p.label}
              </option>
            ))}
            <option value={CUSTOM}>Custom (cron)</option>
          </TextField>
          <TextField
            size="small"
            label="Cron expression"
            value={cron}
            onChange={(e) => onChange(formatSchedule(e.target.value, timeZone))}
            error={cronInvalid}
            helperText={cronInvalid ? 'Use 5 fields: minute hour day month weekday, e.g. 0 9 * * 1-5' : 'minute hour day month weekday'}
            sx={{ flexGrow: 1 }}
            slotProps={{ htmlInput: { spellCheck: false, 'aria-label': 'Cron expression' } }}
          />
          <TextField
            size="small"
            label="Time zone"
            value={timeZone}
            onChange={(e) => onChange(formatSchedule(cron, e.target.value))}
            helperText="IANA name, e.g. Asia/Kolkata"
            sx={{ minWidth: { sm: 180 } }}
          />
        </Stack>
      )}
      {parsed.kind === 'schedule' && (
        <Alert severity="info" variant="outlined">
          Scheduled runs start only while the automation is published. A run missed while the server was down for more
          than 5 minutes is skipped, not caught up.
        </Alert>
      )}
    </Stack>
  )
}
