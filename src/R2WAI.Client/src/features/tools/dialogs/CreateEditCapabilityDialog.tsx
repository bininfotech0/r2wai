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
import { RISK_LEVELS, type CapabilityDto, type CapabilityFormInput, type RiskLevel } from '../types'

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE']

interface CreateEditCapabilityDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (values: CapabilityFormInput) => void | Promise<void>
  tool?: CapabilityDto | null
  isSubmitting?: boolean
}

export function CreateEditCapabilityDialog({ open, onClose, onSubmit, tool, isSubmitting }: CreateEditCapabilityDialogProps) {
  const isEdit = !!tool
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [httpMethod, setHttpMethod] = useState('GET')
  const [endpointPath, setEndpointPath] = useState('')
  const [riskLevel, setRiskLevel] = useState<RiskLevel>('Low')
  const [requiredRole, setRequiredRole] = useState('')
  const [confirmationRequired, setConfirmationRequired] = useState(false)
  const [approvalRequired, setApprovalRequired] = useState(false)
  const [auditRequired, setAuditRequired] = useState(true)

  useEffect(() => {
    if (!open) return
    setName(tool?.name ?? '')
    setDescription(tool?.description ?? '')
    setHttpMethod(tool?.httpMethod ?? 'GET')
    setEndpointPath(tool?.endpointPath ?? '')
    setRiskLevel(tool?.riskLevel ?? 'Low')
    setRequiredRole(tool?.requiredRole ?? '')
    setConfirmationRequired(tool?.confirmationRequired ?? false)
    setApprovalRequired(tool?.approvalRequired ?? false)
    setAuditRequired(tool?.auditRequired ?? true)
  }, [open, tool])

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    void onSubmit({
      name,
      description: description || undefined,
      httpMethod,
      endpointPath: endpointPath || undefined,
      riskLevel,
      requiredRole: requiredRole || undefined,
      confirmationRequired,
      approvalRequired,
      auditRequired,
    })
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Edit Tool' : 'New Tool'}</DialogTitle>
      <Box component="form" onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2}>
            <TextField label="Name" fullWidth autoFocus required value={name} onChange={(e) => setName(e.target.value)} />
            <TextField label="Description" fullWidth multiline minRows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
            <Stack direction="row" spacing={2}>
              <FormControl size="small" sx={{ minWidth: 120 }}>
                <InputLabel id="tool-method-label">Method</InputLabel>
                <Select labelId="tool-method-label" label="Method" value={httpMethod} onChange={(e) => setHttpMethod(e.target.value)}>
                  {HTTP_METHODS.map((m) => (
                    <MenuItem key={m} value={m}>
                      {m}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <TextField
                label="Endpoint path"
                placeholder="/leave/{id}"
                fullWidth
                value={endpointPath}
                onChange={(e) => setEndpointPath(e.target.value)}
              />
            </Stack>
            <Typography variant="subtitle2">Governance</Typography>
            <FormControl size="small" fullWidth>
              <InputLabel id="tool-risk-label">Risk level</InputLabel>
              <Select labelId="tool-risk-label" label="Risk level" value={riskLevel} onChange={(e) => setRiskLevel(e.target.value as RiskLevel)}>
                {RISK_LEVELS.map((r) => (
                  <MenuItem key={r} value={r}>
                    {r}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField label="Required role (optional)" fullWidth value={requiredRole} onChange={(e) => setRequiredRole(e.target.value)} />
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={confirmationRequired} onChange={(e) => setConfirmationRequired(e.target.checked)} />
              <Typography variant="body2">Confirmation required before running</Typography>
            </Stack>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={approvalRequired} onChange={(e) => setApprovalRequired(e.target.checked)} />
              <Typography variant="body2">Approval required</Typography>
            </Stack>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch checked={auditRequired} onChange={(e) => setAuditRequired(e.target.checked)} />
              <Typography variant="body2">Audit every call</Typography>
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isSubmitting || !name}>
            {isSubmitting ? 'Saving…' : isEdit ? 'Save changes' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
