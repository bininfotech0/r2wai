import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import { ApiRequestError } from '../../../lib/api/fetchJson'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { analyzeOpenApiSpec, commitOpenApiImport } from '../api'

interface ImportOpenApiDialogProps {
  open: boolean
  onClose: () => void
}

const operationKey = (method: string, path: string) => `${method}-${path}`

/**
 * Analyzes an OpenAPI 3.x spec (URL or uploaded file) and imports selected
 * operations as callable Http integrations (Track B Phase 3a).
 */
export function ImportOpenApiDialog({ open, onClose }: ImportOpenApiDialogProps) {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [sourceTab, setSourceTab] = useState<'url' | 'file'>('url')
  const [url, setUrl] = useState('')
  const [fileContent, setFileContent] = useState<string | null>(null)
  const [fileName, setFileName] = useState('')
  const [selected, setSelected] = useState<Set<string>>(new Set())

  const analyzeMutation = useMutation({
    mutationFn: () => analyzeOpenApiSpec(sourceTab === 'url' ? { url } : { fileContent: fileContent ?? undefined }),
  })

  const commitMutation = useMutation({
    mutationFn: () => {
      const operations = (analyzeMutation.data?.operations ?? []).filter((op) =>
        selected.has(operationKey(op.method, op.path)),
      )
      return commitOpenApiImport(analyzeMutation.data?.baseUrl ?? '', operations)
    },
    onSuccess: (result) => {
      notify(`Imported ${result.ids.length} tool${result.ids.length === 1 ? '' : 's'}`, 'success')
      void queryClient.invalidateQueries({ queryKey: ['integrations'] })
      handleClose()
    },
    onError: () => notify('Failed to import selected operations', 'error'),
  })

  const notAvailable = analyzeMutation.isError && analyzeMutation.error instanceof ApiRequestError

  function handleClose() {
    setUrl('')
    setFileContent(null)
    setFileName('')
    setSelected(new Set())
    analyzeMutation.reset()
    commitMutation.reset()
    onClose()
  }

  function toggle(key: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Import from OpenAPI Spec</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          {!analyzeMutation.data && (
            <>
              <Tabs value={sourceTab} onChange={(_, v) => setSourceTab(v)}>
                <Tab label="URL" value="url" />
                <Tab label="Upload file" value="file" />
              </Tabs>
              {sourceTab === 'url' ? (
                <TextField
                  label="OpenAPI spec URL"
                  placeholder="https://api.example.com/swagger.json"
                  fullWidth
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                />
              ) : (
                <Box>
                  <Button variant="outlined" component="label">
                    {fileName || 'Choose spec file (.json/.yaml)'}
                    <input
                      type="file"
                      hidden
                      accept=".json,.yaml,.yml"
                      onChange={(e) => {
                        const file = e.target.files?.[0]
                        if (!file) return
                        setFileName(file.name)
                        void file.text().then(setFileContent)
                      }}
                    />
                  </Button>
                </Box>
              )}
            </>
          )}

          {notAvailable && (
            <Alert severity="info">
              OpenAPI import isn't available on this backend yet — this UI is built ahead of the endpoint so it's
              ready the moment it lands.
            </Alert>
          )}
          {analyzeMutation.isError && !notAvailable && <Alert severity="error">Failed to analyze spec.</Alert>}

          {analyzeMutation.data && (
            <>
              <Typography variant="subtitle2">
                {analyzeMutation.data.operations.length} operation
                {analyzeMutation.data.operations.length === 1 ? '' : 's'} found
              </Typography>
              <List dense>
                {analyzeMutation.data.operations.map((op) => {
                  const key = operationKey(op.method, op.path)
                  return (
                    <ListItem key={key} onClick={() => toggle(key)} sx={{ cursor: 'pointer' }}>
                      <ListItemIcon>
                        <Checkbox edge="start" checked={selected.has(key)} tabIndex={-1} disableRipple />
                      </ListItemIcon>
                      <ListItemText primary={op.suggestedName} secondary={`${op.method} ${op.path}`} />
                    </ListItem>
                  )
                })}
              </List>
            </>
          )}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose}>Cancel</Button>
        {!analyzeMutation.data ? (
          <Button
            variant="contained"
            disabled={(sourceTab === 'url' ? !url : !fileContent) || analyzeMutation.isPending}
            onClick={() => analyzeMutation.mutate()}
          >
            {analyzeMutation.isPending ? 'Analyzing…' : 'Analyze'}
          </Button>
        ) : (
          <Button
            variant="contained"
            disabled={selected.size === 0 || commitMutation.isPending}
            onClick={() => commitMutation.mutate()}
          >
            {commitMutation.isPending
              ? 'Importing…'
              : `Import ${selected.size || ''} Tool${selected.size === 1 ? '' : 's'}`}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  )
}
