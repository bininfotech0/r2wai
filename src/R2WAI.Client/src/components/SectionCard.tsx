import type { ReactNode } from 'react'
import { Box, Paper, Stack, Typography } from '@mui/material'

interface SectionCardProps {
  title: string
  /** One-line explanation under the title. */
  description?: string
  /** A single action, top-right (Edit, + Add …). */
  action?: ReactNode
  children: ReactNode
}

/**
 * One block of a detail page's Overview, in the Copilot Studio pattern: a titled card with a
 * single action on the right. Agent Overview stacks these (Details, Instructions, Knowledge,
 * Tools) instead of one long form.
 */
export function SectionCard({ title, description, action, children }: SectionCardProps) {
  return (
    <Paper variant="outlined" component="section" aria-label={title} sx={{ p: { xs: 2, sm: 2.5 } }}>
      <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start', mb: 1.5 }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 650 }}>
            {title}
          </Typography>
          {description && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
              {description}
            </Typography>
          )}
        </Box>
        {action && <Box sx={{ flexShrink: 0 }}>{action}</Box>}
      </Stack>
      {children}
    </Paper>
  )
}
