import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import {
  Alert,
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
  TextField,
} from '@mui/material'
import { testIntegration } from '../api'
import { ApiRequestError } from '../../../lib/api/fetchJson'
import { TOOL_TYPES, TOOL_TYPE_LABELS, type AuthType, type IntegrationConfiguration, type IntegrationDto, type ToolType } from '../types'

const AUTH_TYPES: AuthType[] = ['None', 'Bearer', 'ApiKey', 'Basic', 'OAuth2']

export interface IntegrationFormValues {
  name: string
  type: ToolType
  description: string
  endpointUrl: string
  configuration: string
}

interface CreateEditIntegrationDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: IntegrationFormValues) => void | Promise<void>
  integration?: IntegrationDto | null
  isSubmitting?: boolean
  // Catalog "Connect" prefill — create mode only (ignored when editing an existing integration,
  // which always seeds from the real saved row instead). Nothing here is a live connection yet;
  // it just saves retyping the catalog's own suggested name/endpoint/auth type.
  initialValues?: { name?: string; type?: ToolType; description?: string; endpointUrl?: string; authType?: AuthType }
}

function parseConfig(json: string | null | undefined): IntegrationConfiguration {
  if (!json) return { AuthType: 'None' }
  try {
    return JSON.parse(json) as IntegrationConfiguration
  } catch {
    return { AuthType: 'None' }
  }
}

export function CreateEditIntegrationDialog({
  open,
  onClose,
  onSubmit,
  integration,
  isSubmitting,
  initialValues,
}: CreateEditIntegrationDialogProps) {
  const isEdit = !!integration

  const [name, setName] = useState('')
  const [type, setType] = useState<ToolType>('Http')
  const [description, setDescription] = useState('')
  const [endpointUrl, setEndpointUrl] = useState('')
  const [authType, setAuthType] = useState<AuthType>('None')
  const [token, setToken] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [apiKeyHeaderName, setApiKeyHeaderName] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')

  const testMutation = useMutation({ mutationFn: () => testIntegration(integration!.id) })

  useEffect(() => {
    if (!open) return
    const config = parseConfig(integration?.configuration)
    setName(integration?.name ?? initialValues?.name ?? '')
    setType((integration?.type as ToolType) ?? initialValues?.type ?? 'Http')
    setDescription(integration?.description ?? initialValues?.description ?? '')
    setEndpointUrl(integration?.endpointUrl ?? initialValues?.endpointUrl ?? '')
    setAuthType(config.AuthType ?? initialValues?.authType ?? 'None')
    setToken(config.Token ?? '')
    setApiKey(config.ApiKey ?? '')
    setApiKeyHeaderName(config.ApiKeyHeaderName ?? '')
    setUsername(config.Username ?? '')
    setPassword(config.Password ?? '')
    testMutation.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, integration, initialValues])

  // Secrets (Token/ApiKey/Password) are never sent back by the server once saved (see
  // IntegrationDto.configuration on the backend — redacted), so the fields below start blank on
  // Edit. Omitting a blank secret here (rather than submitting an empty string) tells the backend
  // "unchanged" — IntegrationCredentialCodec.MergeAndEncrypt preserves whatever was saved before.
  // Non-secret fields (ApiKeyHeaderName, Username) are never redacted, so those are always resent.
  function buildConfiguration(): string {
    const config: IntegrationConfiguration = { AuthType: authType }
    if (authType === 'Bearer' || authType === 'OAuth2') {
      if (token) config.Token = token
    }
    if (authType === 'ApiKey') {
      if (apiKey) config.ApiKey = apiKey
      config.ApiKeyHeaderName = apiKeyHeaderName
    }
    if (authType === 'Basic') {
      config.Username = username
      if (password) config.Password = password
    }
    return JSON.stringify(config)
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({ name, type, description, endpointUrl, configuration: buildConfiguration() })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Integration' : 'New Integration'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <FormControl size="small" fullWidth required>
              <InputLabel id="integration-type-label">Type</InputLabel>
              <Select
                labelId="integration-type-label"
                label="Type"
                value={type}
                onChange={(e) => setType(e.target.value as ToolType)}
              >
                {TOOL_TYPES.map((t) => (
                  <MenuItem key={t} value={t}>
                    {TOOL_TYPE_LABELS[t]}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            {type !== 'Http' && (
              <Alert severity="warning">
                {TOOL_TYPE_LABELS[type]} integrations save as a governance record — visible in audits and
                policies — but assistants cannot call them yet. Only REST/HTTP integrations are executable
                today.
              </Alert>
            )}
            <TextField
              label="Description"
              fullWidth
              multiline
              minRows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
            <TextField
              label="Base URL"
              placeholder="https://api.example.com"
              fullWidth
              value={endpointUrl}
              onChange={(e) => setEndpointUrl(e.target.value)}
            />

            <FormControl size="small" fullWidth>
              <InputLabel id="auth-type-label">Authentication</InputLabel>
              <Select
                labelId="auth-type-label"
                label="Authentication"
                value={authType}
                onChange={(e) => setAuthType(e.target.value as AuthType)}
              >
                {AUTH_TYPES.map((a) => (
                  <MenuItem key={a} value={a}>
                    {a}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {(authType === 'Bearer' || authType === 'OAuth2') && (
              <TextField
                label="Token"
                type="password"
                fullWidth
                value={token}
                onChange={(e) => setToken(e.target.value)}
                helperText={isEdit ? 'Leave blank to keep the current token' : undefined}
              />
            )}
            {authType === 'ApiKey' && (
              <>
                <TextField
                  label="API Key"
                  type="password"
                  fullWidth
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  helperText={isEdit ? 'Leave blank to keep the current key' : undefined}
                />
                <TextField
                  label="Header name"
                  placeholder="X-Api-Key"
                  fullWidth
                  required
                  helperText="The HTTP header this key is sent in"
                  value={apiKeyHeaderName}
                  onChange={(e) => setApiKeyHeaderName(e.target.value)}
                />
              </>
            )}
            {authType === 'Basic' && (
              <>
                <TextField label="Username" fullWidth value={username} onChange={(e) => setUsername(e.target.value)} />
                <TextField
                  label="Password"
                  type="password"
                  fullWidth
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  helperText={isEdit ? 'Leave blank to keep the current password' : undefined}
                />
              </>
            )}

            {isEdit && (
              <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
                <Button
                  size="small"
                  variant="outlined"
                  onClick={() => testMutation.mutate()}
                  disabled={testMutation.isPending}
                >
                  {testMutation.isPending ? 'Testing…' : 'Test connection'}
                </Button>
                {testMutation.data && (
                  <Alert severity={testMutation.data.success ? 'success' : 'warning'} sx={{ py: 0, flexGrow: 1 }}>
                    {testMutation.data.message}
                  </Alert>
                )}
                {testMutation.isError && (
                  <Alert severity="error" sx={{ py: 0, flexGrow: 1 }}>
                    {testMutation.error instanceof ApiRequestError ? testMutation.error.message : 'Test request failed.'}
                  </Alert>
                )}
              </Stack>
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={isSubmitting || !name || (authType === 'ApiKey' && !!apiKey && !apiKeyHeaderName)}
          >
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
