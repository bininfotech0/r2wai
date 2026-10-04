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
  { kind: 'assistant', label: 'AI Assistant', description: 'Answer, reason and perform tasks', icon: SmartToyOutlined },
  { kind: 'automation', label: 'Automation', description: 'Automate business work and processes', icon: AccountTreeOutlined },
  { kind: 'knowledge', label: 'Knowledge', description: 'Add enterprise data', icon: MenuBookOutlined },
  { kind: 'integration', label: 'Integration', description: 'Connect an existing system', icon: BuildOutlined },
]

/**
 * The "+ Create" entry point shared by Home and each Build area's own
 * "+New X" button — see the migration plan's UI/UX Design System section.
 */
export function UniversalCreate({ open, onClose, onSelect, onTemplate }: UniversalCreateProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
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
              <Grid key={option.kind} size={6}>
                <Paper
                  variant="outlined"
                  onClick={() => onSelect(option.kind)}
                  sx={{
                    p: 2,
                    cursor: 'pointer',
                    height: '100%',
                    '&:hover': { borderColor: 'primary.main', bgcolor: 'action.hover' },
                  }}
                >
                  <Icon color="primary" sx={{ mb: 1 }} />
                  <Typography variant="subtitle2">{option.label}</Typography>
                  <Typography variant="caption" color="text.secondary">
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
