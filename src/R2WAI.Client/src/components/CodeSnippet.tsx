import { useCallback, useEffect, useRef, useState } from 'react'
import { Box, IconButton, Stack, Tooltip, Typography } from '@mui/material'
import ContentCopyOutlined from '@mui/icons-material/ContentCopyOutlined'
import CheckIcon from '@mui/icons-material/Check'

interface CodeSnippetProps {
  code: string
  /** Shown in the header strip. Also names the language for screen readers. */
  language?: string
  /** Caps the rendered height and scrolls past it. Good for long embed snippets. */
  maxHeight?: number
  /** Disables the copy button for read-only reference material. */
  copyable?: boolean
}

/**
 * Read-only code block with a copy-to-clipboard button — the one component every
 * "here is the thing to paste" surface needs (widget embed script, curl sample,
 * .NET/React SDK snippet, webhook payload).
 *
 * Clipboard writes go through `navigator.clipboard` with a `document.execCommand`
 * fallback, because the app is routinely served over plain HTTP on a LAN IP during
 * on-prem demos, where the async Clipboard API is unavailable in non-secure contexts
 * and would otherwise throw instead of copying.
 */
export function CodeSnippet({ code, language, maxHeight, copyable = true }: CodeSnippetProps) {
  const [copied, setCopied] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current)
    },
    [],
  )

  const handleCopy = useCallback(async () => {
    const markCopied = () => {
      setCopied(true)
      if (timer.current) clearTimeout(timer.current)
      timer.current = setTimeout(() => setCopied(false), 2000)
    }
    try {
      await navigator.clipboard.writeText(code)
      markCopied()
    } catch {
      const textarea = document.createElement('textarea')
      textarea.value = code
      textarea.setAttribute('readonly', '')
      textarea.style.position = 'fixed'
      textarea.style.opacity = '0'
      document.body.appendChild(textarea)
      textarea.select()
      const ok = document.execCommand('copy')
      document.body.removeChild(textarea)
      if (ok) markCopied()
    }
  }, [code])

  return (
    <Box
      component="figure"
      sx={{ m: 0, border: '1px solid', borderColor: 'divider', borderRadius: 2, overflow: 'hidden' }}
    >
      {(language || copyable) && (
        <Stack
          direction="row"
          sx={{
            alignItems: 'center',
            justifyContent: 'space-between',
            px: 1.5,
            py: 0.75,
            bgcolor: 'action.hover',
            borderBottom: '1px solid',
            borderColor: 'divider',
          }}
        >
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{ fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}
          >
            {language ?? 'Snippet'}
          </Typography>
          {copyable && (
            <Tooltip title={copied ? 'Copied' : 'Copy to clipboard'}>
              <IconButton size="small" onClick={() => void handleCopy()} aria-label={`Copy ${language ?? 'code'} snippet`}>
                {copied ? (
                  <CheckIcon fontSize="inherit" color="success" />
                ) : (
                  <ContentCopyOutlined fontSize="inherit" />
                )}
              </IconButton>
            </Tooltip>
          )}
        </Stack>
      )}
      <Box
        component="pre"
        sx={{
          m: 0,
          p: 1.5,
          overflow: 'auto',
          maxHeight,
          fontSize: '0.8125rem',
          lineHeight: 1.6,
          fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Consolas, monospace',
        }}
      >
        <code>{code}</code>
      </Box>
      {/* Announces the copy result to screen readers; the icon swap alone is visual. */}
      <Typography role="status" aria-live="polite" sx={{ position: 'absolute', width: 1, height: 1, p: 0, m: -1, overflow: 'hidden', clipPath: 'inset(50%)', whiteSpace: 'nowrap' }}>
        {copied ? 'Copied to clipboard' : ''}
      </Typography>
    </Box>
  )
}
