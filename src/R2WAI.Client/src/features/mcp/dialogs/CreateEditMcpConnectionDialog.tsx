import { useEffect, useState } from 'react'
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'
import type { McpConnectionDto, McpConnectionInput } from '../types'

interface CreateEditMcpConnectionDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: McpConnectionInput) => void | Promise<void>
  connection?: McpConnectionDto | null
  isSubmitting?: boolean
}

function isHttpEndpoint(value: string) {
  try {
    const url = new URL(value)
    return (url.protocol === 'https:' || url.protocol === 'http:') && !!url.hostname
  } catch {
    return false
  }
}

export function CreateEditMcpConnectionDialog({
  open,
  onClose,
  onSubmit,
  connection,
  isSubmitting,
}: CreateEditMcpConnectionDialogProps) {
  const isEdit = !!connection
  const [name, setName] = useState('')
  const [endpointUrl, setEndpointUrl] = useState('')
  const [authHeaderName, setAuthHeaderName] = useState('')
  const [credential, setCredential] = useState('')
  const endpointInvalid = endpointUrl.trim().length > 0 && !isHttpEndpoint(endpointUrl.trim())
  const credentialNeedsHeader = credential.length > 0 && !authHeaderName.trim()

  useEffect(() => {
    if (!open) return
    setName(connection?.name ?? '')
    setEndpointUrl(connection?.endpointUrl ?? '')
    setAuthHeaderName(connection?.authHeaderName ?? '')
    setCredential('')
  }, [open, connection])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name: name.trim(),
      endpointUrl: endpointUrl.trim(),
      authHeaderName: authHeaderName.trim() || undefined,
      credential: credential || undefined,
    })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit MCP Server' : 'Connect MCP Server'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField
              label="Endpoint URL"
              placeholder="https://mcp.example.com/sse"
              error={endpointInvalid}
              helperText={endpointInvalid ? 'Enter a valid http:// or https:// endpoint. Local/stdio servers are not supported.' : 'HTTP/SSE transport only — no local/stdio servers.'}
              fullWidth
              required
              value={endpointUrl}
              onChange={(e) => setEndpointUrl(e.target.value)}
            />
            <TextField
              label="Auth header name (optional)"
              placeholder="Authorization"
              error={credentialNeedsHeader}
              helperText={credentialNeedsHeader ? 'Enter the header name used to send this credential.' : 'Leave blank if the server requires no authentication.'}
              fullWidth
              value={authHeaderName}
              onChange={(e) => setAuthHeaderName(e.target.value)}
            />
            <TextField
              label="Credential (optional)"
              helperText={
                connection?.hasCredential
                  ? 'A credential is already stored — leave blank to keep it, or enter a new value to replace it'
                  : 'Stored encrypted; sent under the header name above on every call to this server'
              }
              type="password"
              fullWidth
              value={credential}
              onChange={(e) => setCredential(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name.trim() || !isHttpEndpoint(endpointUrl.trim()) || credentialNeedsHeader}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Connect'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
