import { useState } from 'react'
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
  TextField,
} from '@mui/material'
import { APPLICATION_ENVIRONMENTS, type ApplicationEnvironment, type CreateApplicationInput } from '../types'

interface CreateApplicationDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CreateApplicationInput) => void | Promise<void>
  isSubmitting?: boolean
}

export function CreateApplicationDialog({ open, onClose, onSubmit, isSubmitting }: CreateApplicationDialogProps) {
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [description, setDescription] = useState('')
  const [baseUrl, setBaseUrl] = useState('')
  const [environment, setEnvironment] = useState<ApplicationEnvironment>('Development')

  function handleClose() {
    setName('')
    setCode('')
    setDescription('')
    setBaseUrl('')
    setEnvironment('Development')
    onClose()
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name,
      code,
      description: description || undefined,
      baseUrl: baseUrl || undefined,
      environment,
    })
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Add Connected System</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField label="Code" fullWidth required value={code} onChange={(e) => setCode(e.target.value)} />
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
              placeholder="https://app.example.gov"
              fullWidth
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="application-env-label">Environment</InputLabel>
              <Select
                labelId="application-env-label"
                label="Environment"
                value={environment}
                onChange={(e) => setEnvironment(e.target.value as ApplicationEnvironment)}
              >
                {APPLICATION_ENVIRONMENTS.map((env) => (
                  <MenuItem key={env} value={env}>
                    {env}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name || !code}>
            {isSubmitting ? 'Creating…' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
