import { Box, Stack, Typography } from '@mui/material'

const srOnly = {
  position: 'absolute',
  width: '1px',
  height: '1px',
  p: 0,
  m: '-1px',
  overflow: 'hidden',
  clip: 'rect(0 0 0 0)',
  whiteSpace: 'nowrap',
  border: 0,
} as const

interface ChatTypingIndicatorProps {
  label?: string
}

/** Small, accessible response-in-progress cue shared by chat surfaces. */
export function ChatTypingIndicator({ label = 'Assistant is responding' }: ChatTypingIndicatorProps) {
  return (
    <Stack
      direction="row"
      spacing={1}
      role="status"
      aria-live="polite"
      sx={{ alignItems: 'center', minHeight: 24 }}
    >
      <Box component="span" sx={srOnly}>{label}</Box>
      <Stack direction="row" spacing={0.5} aria-hidden="true" sx={{ alignItems: 'center' }}>
        {[0, 1, 2].map((index) => (
          <Box
            key={index}
            component="span"
            sx={{
              width: 5,
              height: 5,
              borderRadius: '50%',
              bgcolor: 'text.secondary',
              opacity: 0.35,
              animation: `r2wai-chat-typing 900ms ease-in-out ${index * 140}ms infinite`,
              '@keyframes r2wai-chat-typing': {
                '0%, 60%, 100%': { opacity: 0.35, transform: 'translateY(0) scale(.85)' },
                '30%': { opacity: 0.9, transform: 'translateY(-3px) scale(1)' },
              },
            }}
          />
        ))}
      </Stack>
      <Typography variant="caption" color="text.secondary">Thinking</Typography>
    </Stack>
  )
}
