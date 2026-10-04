import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import { discoverApplication } from '../api'
import type { ApplicationDiscoveryResultDto } from '../types'

interface DiscoverApplicationDialogProps {
  open: boolean
  applicationId: string
  onClose: () => void
  onDiscovered: (result: ApplicationDiscoveryResultDto) => void
}

/**
 * Track B Phase 7 (Application Discovery Engine) — the scoped starting point: point this at an
 * existing application's OpenAPI spec, and it creates/enriches that application's API plus one
 * governed capability per discovered operation, ready for admin review before Publish.
 */
export function DiscoverApplicationDialog({ open, applicationId, onClose, onDiscovered }: DiscoverApplicationDialogProps) {
  const [sourceTab, setSourceTab] = useState<'url' | 'file'>('url')
  const [url, setUrl] = useState('')
  const [fileContent, setFileContent] = useState<string | null>(null)
  const [fileName, setFileName] = useState('')

  const discoverMutation = useMutation({
    mutationFn: () =>
      discoverApplication(applicationId, sourceTab === 'url' ? { openApiUrl: url } : { openApiFileContent: fileContent ?? undefined }),
    onSuccess: (result) => onDiscovered(result),
  })

  function handleClose() {
    setUrl('')
    setFileContent(null)
    setFileName('')
    discoverMutation.reset()
    onClose()
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Discover from OpenAPI Spec</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <Typography variant="body2" color="text.secondary">
            Point this at the connected system's OpenAPI spec to automatically register its API and create a
            reviewable capability for each endpoint.
          </Typography>
          <Tabs value={sourceTab} onChange={(_, v) => setSourceTab(v)}>
            <Tab label="URL" value="url" />
            <Tab label="Upload file" value="file" />
          </Tabs>
          {sourceTab === 'url' ? (
            <TextField
              label="OpenAPI spec URL"
              placeholder="https://api.example.gov/swagger.json"
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

          {discoverMutation.isError && <Alert severity="error">Failed to discover from this spec.</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose}>Cancel</Button>
        <Button
          variant="contained"
          disabled={(sourceTab === 'url' ? !url : !fileContent) || discoverMutation.isPending}
          onClick={() => discoverMutation.mutate()}
        >
          {discoverMutation.isPending ? 'Discovering…' : 'Discover'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
