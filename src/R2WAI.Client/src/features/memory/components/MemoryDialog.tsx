import { useState, type FormEvent } from 'react'
import { MenuItem, Stack, Switch, TextField, Typography } from '@mui/material'
import { FormDialog } from '../../../components/dialogs/FormDialog'
import { CATEGORIES, MEMORY_CONTENT_MAX, type SaveMemoryInput, type UserMemoryDto } from '../types'

interface MemoryDialogProps {
  /** null adds a new Personal memory. */
  memory: UserMemoryDto | null
  isSubmitting: boolean
  error: string | null
  onClose: () => void
  onSubmit: (input: SaveMemoryInput) => void
}

/** Add or edit one memory. Mounted per open, so each opening starts from the saved values. */
export function MemoryDialog({ memory, isSubmitting, error, onClose, onSubmit }: MemoryDialogProps) {
  const [form, setForm] = useState<SaveMemoryInput>(() => ({
    category: memory?.category ?? 'Preference',
    key: memory?.key ?? '',
    content: memory?.content ?? '',
    isPinned: memory?.isPinned ?? false,
    paused: memory?.status === 'Paused',
  }))
  const [touched, setTouched] = useState(false)

  const contentError = touched && !form.content.trim() ? 'Write what should be remembered.' : null
  const hint = CATEGORIES.find((c) => c.value === form.category)?.hint

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setTouched(true)
    if (!form.content.trim()) return
    onSubmit({ ...form, key: form.key?.trim() ? form.key.trim() : null, content: form.content.trim() })
  }

  return (
    <FormDialog
      open
      onClose={onClose}
      title={memory ? 'Edit memory' : 'Add memory'}
      onSubmit={handleSubmit}
      isSubmitting={isSubmitting}
      submitLabel={memory ? 'Save' : 'Add memory'}
    >
      <TextField
        select
        label="Type"
        value={form.category}
        helperText={hint}
        onChange={(e) => setForm((f) => ({ ...f, category: e.target.value as SaveMemoryInput['category'] }))}
      >
        {CATEGORIES.map((c) => <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>)}
      </TextField>
      <TextField
        label="What should agents remember?"
        placeholder="e.g. Prefers short answers in Hindi."
        value={form.content}
        multiline
        minRows={3}
        required
        autoFocus
        error={!!contentError || !!error}
        helperText={contentError ?? error ?? `${form.content.length}/${MEMORY_CONTENT_MAX} · No ID numbers, phone numbers or email addresses.`}
        slotProps={{ htmlInput: { maxLength: MEMORY_CONTENT_MAX } }}
        onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))}
        onBlur={() => setTouched(true)}
      />
      <TextField
        label="Name (optional)"
        placeholder="preferred_language"
        value={form.key ?? ''}
        helperText="A short name keeps this as one entry you can update, instead of a new one each time."
        slotProps={{ htmlInput: { maxLength: 64 } }}
        onChange={(e) => setForm((f) => ({ ...f, key: e.target.value }))}
      />
      <Stack spacing={0.5}>
        <SwitchRow
          label="Pin"
          description="Pinned memories are always given to agents first."
          checked={form.isPinned}
          onChange={(isPinned) => setForm((f) => ({ ...f, isPinned }))}
        />
        {memory && memory.status !== 'PendingReview' && (
          <SwitchRow
            label="Paused"
            description="Kept here, but no agent sees it until you resume it."
            checked={form.paused}
            onChange={(paused) => setForm((f) => ({ ...f, paused }))}
          />
        )}
      </Stack>
    </FormDialog>
  )
}

function SwitchRow({ label, description, checked, onChange }: {
  label: string
  description: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
      <Switch checked={checked} onChange={(e) => onChange(e.target.checked)} slotProps={{ input: { 'aria-label': label } }} />
      <div>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>{label}</Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.45 }}>{description}</Typography>
      </div>
    </Stack>
  )
}
