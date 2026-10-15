import { Stack } from '@mui/material'
import ContentCopyOutlinedIcon from '@mui/icons-material/ContentCopyOutlined'
import { TooltipIconButton } from './TooltipIconButton'
import { useSnackbar } from '../lib/notifications/useSnackbar'

interface ChatResponseActionsProps {
  content: string
}

/** Quiet, consistent actions attached to a completed assistant response. */
export function ChatResponseActions({ content }: ChatResponseActionsProps) {
  const { notify } = useSnackbar()

  async function copyResponse() {
    try {
      await navigator.clipboard.writeText(content)
      notify('Response copied', 'success')
    } catch {
      notify('Could not copy the response', 'error')
    }
  }

  if (!content.trim()) return null

  return (
    <Stack direction="row" sx={{ justifyContent: 'flex-end', mt: 0.5, mb: -0.5 }}>
      <TooltipIconButton size="small" tooltip="Copy response" onClick={() => void copyResponse()}>
        <ContentCopyOutlinedIcon fontSize="small" />
      </TooltipIconButton>
    </Stack>
  )
}
