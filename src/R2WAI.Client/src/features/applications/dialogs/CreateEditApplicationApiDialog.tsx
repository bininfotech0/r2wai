import { useEffect, useState } from 'react'
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import { API_AUTH_SCHEMES, type ApiAuthScheme, type ApplicationApiDto, type CreateApplicationApiInput } from '../types'

interface CreateEditApplicationApiDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateApplicationApiInput) => void | Promise<void>
  api?: ApplicationApiDto | null
  isSubmitting?: boolean
}

export function CreateEditApplicationApiDialog({
  open,
  onClose,
  onSubmit,
  api,
  isSubmitting,
}: CreateEditApplicationApiDialogProps) {
  const isEdit = !!api
  const [name, setName] = useState('')
  const [baseUrl, setBaseUrl] = useState('')
  const [authScheme, setAuthScheme] = useState<ApiAuthScheme>('None')
  const [credentialRef, setCredentialRef] = useState('')
  const [credentialSecret, setCredentialSecret] = useState('')
  const [credentialHeaderName, setCredentialHeaderName] = useState('')
  const [openApiSource, setOpenApiSource] = useState('')
  const [isActive, setIsActive] = useState(true)

  useEffect(() => {
    if (!open) return
    setName(api?.name ?? '')
    setBaseUrl(api?.baseUrl ?? '')
    setAuthScheme(api?.authScheme ?? 'None')
    setCredentialRef(api?.credentialRef ?? '')
    setCredentialSecret('')
    setCredentialHeaderName(api?.credentialHeaderName ?? '')
    setOpenApiSource(api?.openApiSource ?? '')
    setIsActive(api?.isActive ?? true)
  }, [open, api])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name,
      baseUrl,
      authScheme,
      credentialRef: credentialRef || undefined,
      credentialSecret: credentialSecret || undefined,
      credentialHeaderName: credentialHeaderName || undefined,
      openApiSource: openApiSource || undefined,
      isActive,
    })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit API' : 'New API'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField
              label="Base URL"
              placeholder="https://api.example.gov/v1"
              fullWidth
              required
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="api-auth-scheme-label">Auth scheme</InputLabel>
              <Select
                labelId="api-auth-scheme-label"
                label="Auth scheme"
                value={authScheme}
                onChange={(e) => setAuthScheme(e.target.value as ApiAuthScheme)}
              >
                {API_AUTH_SCHEMES.map((s) => (
                  <MenuItem key={s} value={s}>
                    {s}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            {authScheme !== 'None' && (
              <>
                <TextField
                  label="Credential reference"
                  helperText="A human-readable label for where this credential came from — not the secret itself"
                  fullWidth
                  value={credentialRef}
                  onChange={(e) => setCredentialRef(e.target.value)}
                />
                <TextField
                  label={authScheme === 'ApiKey' ? 'API key' : 'Token'}
                  helperText={
                    api?.hasCredential
                      ? 'A credential is already stored — leave blank to keep it, or enter a new value to replace it'
                      : 'Stored encrypted; used to authenticate outbound calls this API makes'
                  }
                  type="password"
                  fullWidth
                  value={credentialSecret}
                  onChange={(e) => setCredentialSecret(e.target.value)}
                />
                {authScheme === 'ApiKey' && (
                  <TextField
                    label="Header name"
                    placeholder="X-Api-Key"
                    helperText="The HTTP header the key is sent in"
                    fullWidth
                    required
                    value={credentialHeaderName}
                    onChange={(e) => setCredentialHeaderName(e.target.value)}
                  />
                )}
              </>
            )}
            <TextField
              label="OpenAPI spec URL (optional)"
              fullWidth
              value={openApiSource}
              onChange={(e) => setOpenApiSource(e.target.value)}
            />
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              <Typography variant="body2">Active</Typography>
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={
              isSubmitting ||
              !name ||
              !baseUrl ||
              (authScheme === 'ApiKey' && !!credentialSecret && !credentialHeaderName)
            }
          >
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
