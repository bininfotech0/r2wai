import { useState } from 'react'
import { Box, Button, Chip, Stack, TextField, Typography } from '@mui/material'
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome'

interface AiDraftCreatePromptProps {
  heading: string
  placeholder: string
  examples?: string[]
  initialValue?: string
  onGenerate: (description: string) => void | Promise<void>
  onManual?: () => void
  isGenerating?: boolean
}

/**
 * Single free-text-box + Generate flow shared by Assistant (Phase 6) and
 * Automation (Phase 7) AI-first creation — one component instead of two
 * bespoke flows, per the UI/UX design system.
 */
export function AiDraftCreatePrompt({
  heading,
  placeholder,
  examples,
  initialValue,
  onGenerate,
  onManual,
  isGenerating,
}: AiDraftCreatePromptProps) {
  const [description, setDescription] = useState(initialValue ?? '')

  return (
    <Stack spacing={2} sx={{ maxWidth: 560, mx: 'auto', textAlign: 'center', py: 2 }}>
      <Typography variant="h6" sx={{ fontWeight: 600 }}>
        {heading}
      </Typography>

      <TextField
        multiline
        minRows={3}
        placeholder={placeholder}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        fullWidth
      />

      {examples && examples.length > 0 && (
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', justifyContent: 'center' }}>
          {examples.map((example) => (
            <Chip key={example} label={example} size="small" onClick={() => setDescription(example)} />
          ))}
        </Stack>
      )}

      <Box>
        <Button
          variant="contained"
          startIcon={<AutoAwesomeIcon />}
          disabled={!description.trim() || isGenerating}
          onClick={() => void onGenerate(description)}
        >
          {isGenerating ? 'Generating…' : 'Generate'}
        </Button>
      </Box>

      {onManual && (
        <Box>
          <Typography variant="body2" color="text.secondary" component="span">
            or{' '}
          </Typography>
          <Button variant="text" size="small" onClick={onManual}>
            configure manually
          </Button>
        </Box>
      )}
    </Stack>
  )
}
