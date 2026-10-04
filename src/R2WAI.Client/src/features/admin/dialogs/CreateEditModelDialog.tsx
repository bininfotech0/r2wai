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
import { DATA_CLASSIFICATIONS, MODEL_PROVIDERS, type DataClassification, type ModelConfigDto, type ModelFormInput } from '../types'

interface CreateEditModelDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: ModelFormInput) => void | Promise<void>
  model?: ModelConfigDto | null
  isSubmitting?: boolean
}

export function CreateEditModelDialog({ open, onClose, onSubmit, model, isSubmitting }: CreateEditModelDialogProps) {
  const isEdit = !!model
  const [name, setName] = useState('')
  const [provider, setProvider] = useState('ollama')
  const [modelId, setModelId] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [endpoint, setEndpoint] = useState('')
  const [maxTokens, setMaxTokens] = useState('')
  const [temperature, setTemperature] = useState('')
  const [topP, setTopP] = useState('')
  const [isDefault, setIsDefault] = useState(false)
  const [dataClassification, setDataClassification] = useState<DataClassification>('Internal')

  useEffect(() => {
    if (!open) return
    setName(model?.name ?? '')
    setProvider(model?.provider ?? 'ollama')
    setModelId(model?.modelId ?? '')
    setApiKey('')
    setEndpoint(model?.endpoint ?? '')
    setMaxTokens(model?.maxTokens?.toString() ?? '')
    setTemperature(model?.temperature?.toString() ?? '')
    setTopP(model?.topP?.toString() ?? '')
    setIsDefault(model?.isDefault ?? false)
    setDataClassification(model?.dataClassification ?? 'Internal')
  }, [open, model])

  const requiresLocalProvider = dataClassification === 'Confidential' || dataClassification === 'Restricted'
  const maxTokensInvalid = !!maxTokens && (!Number.isInteger(Number(maxTokens)) || Number(maxTokens) < 1)
  const temperatureInvalid = !!temperature && (!Number.isFinite(Number(temperature)) || Number(temperature) < 0 || Number(temperature) > 2)
  const topPInvalid = !!topP && (!Number.isFinite(Number(topP)) || Number(topP) < 0 || Number(topP) > 1)
  const localProviderInvalid = requiresLocalProvider && provider !== 'ollama'
  const formInvalid = maxTokensInvalid || temperatureInvalid || topPInvalid || localProviderInvalid

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name,
      provider,
      modelId,
      apiKey: apiKey || undefined,
      endpoint: endpoint || undefined,
      maxTokens: maxTokens ? Number(maxTokens) : undefined,
      temperature: temperature ? Number(temperature) : undefined,
      topP: topP ? Number(topP) : undefined,
      isDefault,
      dataClassification,
    })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Model' : 'New Model'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <FormControl size="small" fullWidth required>
              <InputLabel id="model-provider-label">Provider</InputLabel>
              <Select labelId="model-provider-label" label="Provider" value={provider} onChange={(e) => setProvider(e.target.value)}>
                {MODEL_PROVIDERS.map((p) => (
                  <MenuItem key={p} value={p}>
                    {p}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Model ID"
              placeholder="e.g. qwen3:4b, gpt-4o"
              fullWidth
              required
              value={modelId}
              onChange={(e) => setModelId(e.target.value)}
            />
            <TextField label="Endpoint (optional)" fullWidth value={endpoint} onChange={(e) => setEndpoint(e.target.value)} />
            <TextField
              label="API key (optional)"
              type="password"
              fullWidth
              helperText={isEdit && model?.hasApiKey ? 'A key is already set — leave blank to keep it' : undefined}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
            />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField
                label="Max tokens"
                type="number"
                fullWidth
                error={maxTokensInvalid}
                helperText={maxTokensInvalid ? 'Enter a whole number greater than 0.' : undefined}
                value={maxTokens}
                onChange={(e) => setMaxTokens(e.target.value)}
              />
              <TextField
                label="Temperature"
                type="number"
                fullWidth
                error={temperatureInvalid}
                helperText={temperatureInvalid ? 'Use a value from 0 to 2.' : undefined}
                slotProps={{ htmlInput: { step: 0.1, min: 0, max: 2 } }}
                value={temperature}
                onChange={(e) => setTemperature(e.target.value)}
              />
              <TextField
                label="Top P"
                type="number"
                fullWidth
                error={topPInvalid}
                helperText={topPInvalid ? 'Use a value from 0 to 1.' : undefined}
                slotProps={{ htmlInput: { step: 0.05, min: 0, max: 1 } }}
                value={topP}
                onChange={(e) => setTopP(e.target.value)}
              />
            </Stack>
            <FormControl size="small" fullWidth>
              <InputLabel id="model-classification-label">Data classification</InputLabel>
              <Select
                labelId="model-classification-label"
                label="Data classification"
                value={dataClassification}
                onChange={(e) => setDataClassification(e.target.value as DataClassification)}
              >
                {DATA_CLASSIFICATIONS.map((c) => (
                  <MenuItem key={c} value={c}>
                    {c}
                  </MenuItem>
                ))}
              </Select>
              {requiresLocalProvider && (
                <Typography variant="caption" color="warning.main" sx={{ mt: 0.5, ml: 1.5 }}>
                  Confidential/Restricted data requires Ollama. Select Ollama or choose a lower classification to
                  continue.
                </Typography>
              )}
            </FormControl>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} />
              <Typography variant="body2">Default model</Typography>
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name.trim() || !modelId.trim() || formInvalid}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
