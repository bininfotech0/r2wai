import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
} from '@mui/material'
import { compareDocuments } from '../api'
import type { DocumentDto } from '../types'

interface CompareDocumentsDialogProps {
  open: boolean
  onClose: () => void
  documents: DocumentDto[]
}

/**
 * The backend has no per-document/knowledge-base version history — this
 * calls POST /documents/compare, which AI-compares two distinct documents'
 * content. Framed as "Compare Documents" rather than a literal version diff.
 */
export function CompareDocumentsDialog({ open, onClose, documents }: CompareDocumentsDialogProps) {
  const [sourceId, setSourceId] = useState('')
  const [targetId, setTargetId] = useState('')

  const mutation = useMutation({
    mutationFn: () => compareDocuments(sourceId, targetId),
  })

  function handleClose() {
    setSourceId('')
    setTargetId('')
    mutation.reset()
    onClose()
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Compare Documents</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <FormControl size="small" fullWidth>
            <InputLabel id="source-doc-label">Document A</InputLabel>
            <Select
              labelId="source-doc-label"
              label="Document A"
              value={sourceId}
              onChange={(e) => setSourceId(e.target.value)}
            >
              {documents.map((d) => (
                <MenuItem key={d.id} value={d.id} disabled={d.id === targetId}>
                  {d.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl size="small" fullWidth>
            <InputLabel id="target-doc-label">Document B</InputLabel>
            <Select
              labelId="target-doc-label"
              label="Document B"
              value={targetId}
              onChange={(e) => setTargetId(e.target.value)}
            >
              {documents.map((d) => (
                <MenuItem key={d.id} value={d.id} disabled={d.id === sourceId}>
                  {d.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {mutation.isError && <Alert severity="error">Comparison failed.</Alert>}

          {mutation.data && (
            <Stack spacing={1.5}>
              <Divider />
              {mutation.data.similarityScore !== null && (
                <Chip
                  label={`Similarity: ${Math.round(mutation.data.similarityScore * 100)}%`}
                  color="primary"
                  variant="outlined"
                  sx={{ alignSelf: 'flex-start' }}
                />
              )}
              <Typography variant="body2">{mutation.data.comparison}</Typography>
              {mutation.data.differences && mutation.data.differences.length > 0 && (
                <>
                  <Typography variant="subtitle2">Differences</Typography>
                  {mutation.data.differences.map((d, i) => (
                    <Typography key={i} variant="body2" color="text.secondary">
                      • {d}
                    </Typography>
                  ))}
                </>
              )}
              {mutation.data.similarities && mutation.data.similarities.length > 0 && (
                <>
                  <Typography variant="subtitle2">Similarities</Typography>
                  {mutation.data.similarities.map((s, i) => (
                    <Typography key={i} variant="body2" color="text.secondary">
                      • {s}
                    </Typography>
                  ))}
                </>
              )}
            </Stack>
          )}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose}>Close</Button>
        <Button
          variant="contained"
          disabled={!sourceId || !targetId || mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {mutation.isPending ? 'Comparing…' : 'Compare'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
