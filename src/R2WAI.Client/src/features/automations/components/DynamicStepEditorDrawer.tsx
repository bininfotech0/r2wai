import { useEffect, useState } from 'react'
import {
  Box,
  Button,
  Checkbox,
  Chip,
  Drawer,
  FormControl,
  InputLabel,
  ListItemText,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { SchemaForm, type SchemaFormValue } from '../../../components/SchemaForm'
import { STEP_TYPES, type StepType, type WorkflowStep, type WorkflowStepConfig } from '../types'
import { STEP_CONFIG_SCHEMAS } from '../stepSchemas'

interface DynamicStepEditorDrawerProps {
  open: boolean
  onClose: () => void
  step: WorkflowStep | null
  otherStepNames: string[]
  onSave: (step: WorkflowStep) => void
  onDelete?: () => void
}

/**
 * Per-step configuration — Action dropdown selects the real executable type
 * (see StepActivityFactory.ClassifyStepType), Inputs are schema-driven via
 * the shared SchemaForm, and "Runs before" is the cross-step data-binding
 * piece: an explicit next-step list (WorkflowBridge.NextStepsOnlyDto) that
 * fans out execution instead of the default linear next-in-order behavior.
 */
export function DynamicStepEditorDrawer({
  open,
  onClose,
  step,
  otherStepNames,
  onSave,
  onDelete,
}: DynamicStepEditorDrawerProps) {
  const [name, setName] = useState('')
  const [type, setType] = useState<StepType>('Action')
  const [config, setConfig] = useState<SchemaFormValue>({})
  const [nextSteps, setNextSteps] = useState<string[]>([])
  const [retryCount, setRetryCount] = useState(0)
  const [showAdvanced, setShowAdvanced] = useState(false)

  useEffect(() => {
    if (!step) {
      setName('')
      setType('Action')
      setConfig({})
      setNextSteps([])
      setRetryCount(0)
      return
    }
    setName(step.name)
    setType(step.type ?? 'Action')
    setConfig((step.config as SchemaFormValue) ?? {})
    setNextSteps(step.config?.nextSteps ?? [])
    setRetryCount(0)
  }, [step, open])

  function handleSave() {
    const cleanConfig: WorkflowStepConfig = { ...(config as WorkflowStepConfig) }
    if (nextSteps.length > 0) cleanConfig.nextSteps = nextSteps
    onSave({
      order: step?.order ?? 0,
      name: name.trim() || 'Untitled step',
      type,
      action: type === 'Action' ? name.trim() : undefined,
      config: Object.keys(cleanConfig).length > 0 ? cleanConfig : undefined,
    })
  }

  return (
    <Drawer anchor="right" open={open} onClose={onClose}>
      <Box sx={{ width: 400, p: 2.5 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 2 }}>
          {step ? 'Edit Step' : 'New Step'}
        </Typography>
        <Stack spacing={2}>
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth autoFocus />

          <FormControl fullWidth size="small">
            <InputLabel id="step-type-label">Action</InputLabel>
            <Select
              labelId="step-type-label"
              label="Action"
              value={type}
              onChange={(e) => {
                setType(e.target.value as StepType)
                setConfig({})
              }}
            >
              {STEP_TYPES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {Object.keys(STEP_CONFIG_SCHEMAS[type].properties).length > 0 ? (
            <SchemaForm schema={STEP_CONFIG_SCHEMAS[type]} value={config} onChange={setConfig} />
          ) : (
            <Typography variant="caption" color="text.secondary">
              This step type has no additional configuration.
            </Typography>
          )}

          <Button size="small" onClick={() => setShowAdvanced((v) => !v)} sx={{ alignSelf: 'flex-start' }}>
            {showAdvanced ? 'Hide advanced' : 'Advanced ▸'}
          </Button>
          {showAdvanced && (
            <Stack spacing={2}>
              <TextField
                label="Retry count"
                type="number"
                value={retryCount}
                onChange={(e) => setRetryCount(Number(e.target.value))}
                helperText="Not yet wired to execution — reserved for the Advanced Builder (Phase 8)"
                size="small"
              />
              {otherStepNames.length > 0 && (
                <FormControl fullWidth size="small">
                  <InputLabel id="next-steps-label">Runs before</InputLabel>
                  <Select
                    labelId="next-steps-label"
                    label="Runs before"
                    multiple
                    value={nextSteps}
                    onChange={(e) => setNextSteps(typeof e.target.value === 'string' ? [] : e.target.value)}
                    renderValue={(selected) => (
                      <Stack direction="row" spacing={0.5} sx={{ flexWrap: 'wrap' }}>
                        {selected.map((s) => (
                          <Chip key={s} label={s} size="small" />
                        ))}
                      </Stack>
                    )}
                  >
                    {otherStepNames.map((n) => (
                      <MenuItem key={n} value={n}>
                        <Checkbox checked={nextSteps.includes(n)} size="small" />
                        <ListItemText primary={n} />
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            </Stack>
          )}

          <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between' }}>
            {onDelete ? (
              <Button color="error" onClick={onDelete}>
                Remove
              </Button>
            ) : (
              <Box />
            )}
            <Stack direction="row" spacing={1}>
              <Button onClick={onClose}>Cancel</Button>
              <Button variant="contained" disabled={!name.trim()} onClick={handleSave}>
                Save
              </Button>
            </Stack>
          </Stack>
        </Stack>
      </Box>
    </Drawer>
  )
}
