import { useState } from 'react'
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import ContentCopyIcon from '@mui/icons-material/ContentCopy'

interface ChatbotEmbedDialogProps {
  open: boolean
  onClose: () => void
  chatbotId: string
}

/**
 * The widget needs no embed token — POST .../chat/stream and GET
 * .../public-info are [AllowAnonymous] with the chatbot id alone as the only
 * "credential". An admin can now restrict which origins the widget answers
 * from (Chatbot.AllowedOrigins, configured on the detail page) — opt-in, so
 * still worth surfacing that it's off by default (any site can embed) and
 * there's still no per-embed auth token or rate limit beyond the tenant's
 * AiUsage cap.
 */
export function ChatbotEmbedDialog({ open, onClose, chatbotId }: ChatbotEmbedDialogProps) {
  const [copied, setCopied] = useState(false)
  const origin = window.location.origin
  const snippet = `<script src="${origin}/widget/widget.js" data-chatbot-id="${chatbotId}" data-base-url="${origin}" async></script>`

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(snippet)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // Clipboard API unavailable (e.g. non-HTTPS/insecure context) — the
      // snippet is still selectable/copyable manually from the text field.
    }
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Embed Chatbot</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <Typography variant="body2" color="text.secondary">
            Paste this snippet before the closing <code>&lt;/body&gt;</code> tag of any page to add this chatbot as a
            floating widget.
          </Typography>
          <TextField
            value={snippet}
            fullWidth
            multiline
            slotProps={{ input: { readOnly: true, sx: { fontFamily: 'monospace', fontSize: '0.8125rem' } } }}
            onFocus={(e) => e.target.select()}
          />
          <Alert severity="warning">
            The chat endpoint this widget calls is unauthenticated — anyone with this chatbot's id can send it
            messages. By default any site can embed it; set "Allowed embed origins" on the chatbot's detail page to
            restrict which domains it will respond from. There's still no per-embed auth token or dedicated rate
            limit. Don't link a knowledge base with sensitive content to a publicly embedded chatbot.
          </Alert>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose}>Close</Button>
        <Button variant="contained" startIcon={<ContentCopyIcon />} onClick={() => void handleCopy()}>
          {copied ? 'Copied!' : 'Copy snippet'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
