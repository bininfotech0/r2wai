import { FormControl, InputLabel, MenuItem, Select, Stack, Switch, TextField, Typography } from '@mui/material'

export interface JsonSchemaProperty {
  type: 'string' | 'number' | 'integer' | 'boolean'
  title?: string
  description?: string
  enum?: (string | number)[]
  format?: 'textarea'
  default?: unknown
}

export interface JsonSchema {
  type: 'object'
  properties: Record<string, JsonSchemaProperty>
  required?: string[]
}

export type SchemaFormValue = Record<string, string | number | boolean | undefined>

interface SchemaFormProps {
  schema: JsonSchema
  value: SchemaFormValue
  onChange: (value: SchemaFormValue) => void
}

/**
 * Renders a form from a JSON Schema — Tool/Integration/Node Schema -> Dynamic
 * Form Engine -> rendered form, per the migration plan's Track B section.
 * Used by the Dynamic Step Editor (Phase 7), Advanced Automation Builder
 * node properties (Phase 8), and tool/integration Advanced sections. Covers
 * the common property shapes (string/number/boolean, enum-as-select,
 * textarea) — not a full JSON Schema implementation.
 */
export function SchemaForm({ schema, value, onChange }: SchemaFormProps) {
  function setField(key: string, fieldValue: string | number | boolean) {
    onChange({ ...value, [key]: fieldValue })
  }

  return (
    <Stack spacing={2}>
      {Object.entries(schema.properties).map(([key, prop]) => {
        const label = prop.title ?? key
        const required = schema.required?.includes(key) ?? false
        const current = value[key]

        if (prop.enum) {
          const labelId = `${key}-label`
          return (
            <FormControl key={key} size="small" fullWidth required={required}>
              <InputLabel id={labelId}>{label}</InputLabel>
              <Select
                labelId={labelId}
                id={key}
                label={label}
                value={current !== undefined ? String(current) : ''}
                onChange={(e) => setField(key, e.target.value)}
              >
                {prop.enum.map((option) => (
                  <MenuItem key={String(option)} value={String(option)}>
                    {String(option)}
                  </MenuItem>
                ))}
              </Select>
              {prop.description && (
                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, ml: 1.5 }}>
                  {prop.description}
                </Typography>
              )}
            </FormControl>
          )
        }

        if (prop.type === 'boolean') {
          return (
            <Stack key={key} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Switch
                checked={Boolean(current)}
                onChange={(e) => setField(key, e.target.checked)}
              />
              <Typography variant="body2">{label}</Typography>
            </Stack>
          )
        }

        return (
          <TextField
            key={key}
            label={label}
            required={required}
            size="small"
            fullWidth
            multiline={prop.format === 'textarea'}
            minRows={prop.format === 'textarea' ? 3 : undefined}
            type={prop.type === 'number' || prop.type === 'integer' ? 'number' : 'text'}
            helperText={prop.description}
            value={current ?? ''}
            onChange={(e) =>
              setField(
                key,
                prop.type === 'number' || prop.type === 'integer' ? Number(e.target.value) : e.target.value,
              )
            }
          />
        )
      })}
    </Stack>
  )
}
