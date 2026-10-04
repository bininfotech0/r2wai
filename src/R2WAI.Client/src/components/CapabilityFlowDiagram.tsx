import { Box, Paper, Stack, Typography } from '@mui/material'

// Deliberately runtime-agnostic labels — matches roleNav.ts's own convention of never naming an
// implementation engine in user-facing copy. "Semantic Kernel"/"Elsa" are transition-period
// runtime choices (ROADMAP.md §3: Agent Framework is the target orchestrator, a durable
// execution ledger replaces Elsa) — naming them here would show every persona an internal detail
// that's explicitly planned to change, on the one screen everyone sees first.
const STAGES = [
  'AI Assistant',
  'Agent Orchestrator',
  'Knowledge / RAG  +  Agent / Rules',
  'Automation / Execution',
  'Tool / API Gateway',
  'Enterprise Systems',
]

const TARGETS = ['API', 'Database', 'ERP', 'Files', 'Services']

/**
 * Compact vertical version of the platform architecture diagram, shown on
 * Home and reused as the Tool Gateway explainer in Assistant Studio (Phase
 * 6) — see the migration plan's UI/UX Design System section.
 */
export function CapabilityFlowDiagram() {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1.5 }}>
        R2WAI Capability Flow
      </Typography>
      <Stack spacing={0.5} sx={{ alignItems: 'center' }}>
        {STAGES.map((stage, i) => (
          <Box key={stage} sx={{ width: '100%', textAlign: 'center' }}>
            <Box
              sx={(theme) => ({
                py: 0.75,
                px: 1.5,
                borderRadius: 1.5,
                bgcolor:
                  i === 0
                    ? theme.palette.primary.main
                    : theme.palette.mode === 'dark'
                      ? 'action.selected'
                      : 'grey.100',
                color: i === 0 ? theme.palette.primary.contrastText : 'text.primary',
                fontSize: '0.8125rem',
                fontWeight: 500,
              })}
            >
              {stage}
            </Box>
            {i < STAGES.length - 1 && (
              <Typography component="div" sx={{ color: 'text.disabled', lineHeight: 1, fontSize: '0.75rem' }}>
                ↓
              </Typography>
            )}
          </Box>
        ))}
        <Stack direction="row" spacing={1} sx={{ pt: 0.5, flexWrap: 'wrap', justifyContent: 'center' }}>
          {TARGETS.map((target) => (
            <Typography
              key={target}
              variant="caption"
              sx={{
                px: 1,
                py: 0.25,
                borderRadius: 1,
                border: '1px solid',
                borderColor: 'divider',
                color: 'text.secondary',
              }}
            >
              {target}
            </Typography>
          ))}
        </Stack>
      </Stack>
    </Paper>
  )
}
