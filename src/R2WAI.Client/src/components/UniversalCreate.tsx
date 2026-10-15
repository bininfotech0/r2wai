import { useId } from 'react'
import { Box, Button, Dialog, DialogTitle, Grid, Paper, Typography } from '@mui/material'
import { TooltipIconButton as IconButton } from './TooltipIconButton'
import CloseIcon from '@mui/icons-material/Close'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import BuildOutlined from '@mui/icons-material/BuildOutlined'

export type CreateKind = 'assistant' | 'automation' | 'knowledge' | 'integration'

interface UniversalCreateProps {
  open: boolean
  onClose: () => void
  onSelect: (kind: CreateKind) => void
  onTemplate?: () => void
}

const OPTIONS: { kind: CreateKind; label: string; description: string; icon: typeof SmartToyOutlined }[] = [
  { kind: 'assistant', label: 'AI Assistant', description: 'Create an assistant that answers questions and performs tasks', icon: SmartToyOutlined },
  { kind: 'automation', label: 'Automation', description: 'Build a workflow for repeatable business processes', icon: AccountTreeOutlined },
  { kind: 'knowledge', label: 'Knowledge base', description: 'Organize documents and sources for your agents', icon: MenuBookOutlined },
  { kind: 'integration', label: 'Connected system', description: 'Connect an external service for your agents and tools', icon: BuildOutlined },
]

/**
 * The "+ Create" entry point shared by Home and each Build area's own
 * "+New X" button — see the migration plan's UI/UX Design System section.
 */
export function UniversalCreate({ open, onClose, onSelect, onTemplate }: UniversalCreateProps) {
  const titleId = useId()

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth aria-labelledby={titleId}>
      <DialogTitle id={titleId} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        What would you like to create?
        <IconButton size="small" onClick={onClose} aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <Box sx={{ p: 3, pt: 0 }}>
        <Grid container spacing={1.5}>
          {OPTIONS.map((option) => {
            const Icon = option.icon
            return (
              <Grid key={option.kind} size={{ xs: 12, sm: 6 }}>
                <Paper
                  component="button"
                  type="button"
                  variant="outlined"
                  onClick={() => onSelect(option.kind)}
                  sx={{
                    p: 2,
                    cursor: 'pointer',
                    height: '100%',
                    width: '100%',
                    display: 'block',
                    textAlign: 'left',
                    font: 'inherit',
                    color: 'inherit',
                    bgcolor: 'background.paper',
                    transition: (theme) => theme.transitions.create(['background-color', 'border-color']),
                    '&:hover': {
                      borderColor: 'primary.main',
                      bgcolor: 'action.hover',
                      '& .create-option-icon': { color: 'primary.main', bgcolor: 'action.selected' },
                    },
                    '&:focus-visible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: 2 },
                  }}
                >
                  <Box
                    className="create-option-icon"
                    sx={{
                      display: 'grid',
                      placeItems: 'center',
                      width: 36,
                      height: 36,
                      mb: 1,
                      borderRadius: 1.5,
                      bgcolor: 'action.hover',
                      color: 'text.secondary',
                      transition: (theme) => theme.transitions.create(['background-color', 'color']),
                    }}
                  >
                    <Icon fontSize="small" />
                  </Box>
                  <Typography variant="subtitle2">{option.label}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                    {option.description}
                  </Typography>
                </Paper>
              </Grid>
            )
          })}
        </Grid>
        {onTemplate && (
          <Button fullWidth sx={{ mt: 2 }} onClick={onTemplate}>
            Create from template
          </Button>
        )}
      </Box>
    </Dialog>
  )
}
