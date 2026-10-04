import { useRef, useState } from 'react'
import {
  Box,
  Button,
  Card,
  CardActionArea,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  LinearProgress,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Step,
  StepLabel,
  Stepper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import LanguageIcon from '@mui/icons-material/Language'
import QuizIcon from '@mui/icons-material/Quiz'
import StorageIcon from '@mui/icons-material/Storage'
import ExtensionIcon from '@mui/icons-material/Extension'
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile'
import CheckCircleOutlineOutlined from '@mui/icons-material/CheckCircleOutlineOutlined'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlineOutlined'
import { addSource, uploadDocument } from '../api'
import type { SourceType } from '../types'

type Mode = 'picker' | 'Files' | SourceType

const TEXT_SOURCE_MODES: { mode: SourceType; label: string; icon: React.ReactNode; helper: string }[] = [
  { mode: 'Url', label: 'Website', icon: <LanguageIcon />, helper: 'Fetches and indexes a web page' },
  { mode: 'Faq', label: 'FAQ', icon: <QuizIcon />, helper: 'Paste question/answer text to index' },
  { mode: 'Database', label: 'Database', icon: <StorageIcon />, helper: 'Paste extracted rows/text to index' },
  { mode: 'Integration', label: 'Integration', icon: <ExtensionIcon />, helper: 'Paste synced content to index' },
]

const UPLOAD_STEPS = ['Select files', 'Uploading', 'Processing', 'Done']

interface AddKnowledgeDialogProps {
  open: boolean
  onClose: () => void
  knowledgeBaseId: string
  onAdded: () => void
}

export function AddKnowledgeDialog({ open, onClose, knowledgeBaseId, onAdded }: AddKnowledgeDialogProps) {
  const [mode, setMode] = useState<Mode>('picker')
  const [files, setFiles] = useState<File[]>([])
  const [uploadStep, setUploadStep] = useState(0)
  const [uploadResults, setUploadResults] = useState<{ name: string; ok: boolean; error?: string }[]>([])
  const [isBusy, setIsBusy] = useState(false)
  const [url, setUrl] = useState('')
  const [content, setContent] = useState('')
  const [error, setError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  function reset() {
    setMode('picker')
    setFiles([])
    setUploadStep(0)
    setUploadResults([])
    setIsBusy(false)
    setUrl('')
    setContent('')
    setError(null)
  }

  function handleClose() {
    if (isBusy) return
    reset()
    onClose()
  }

  async function handleUploadFiles() {
    setIsBusy(true)
    setUploadStep(1)
    const results: { name: string; ok: boolean; error?: string }[] = []
    for (const file of files) {
      try {
        await uploadDocument(file, knowledgeBaseId)
        results.push({ name: file.name, ok: true })
      } catch {
        results.push({ name: file.name, ok: false, error: 'Upload failed' })
      }
    }
    setUploadStep(2)
    // The upload endpoint enqueues processing server-side; there is no
    // per-file "processing done" signal to poll here, so this step is
    // shown briefly as a transition rather than tracked to completion.
    await new Promise((resolve) => setTimeout(resolve, 400))
    setUploadResults(results)
    setUploadStep(3)
    setIsBusy(false)
    onAdded()
  }

  async function handleAddTextSource(type: SourceType) {
    setIsBusy(true)
    setError(null)
    try {
      await addSource(knowledgeBaseId, {
        type,
        url: type === 'Url' ? url : undefined,
        content: type !== 'Url' ? content : undefined,
      })
      onAdded()
      handleClose()
    } catch {
      setError('Failed to add source.')
    } finally {
      setIsBusy(false)
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Stack direction="row" sx={{ alignItems: 'center', gap: 1 }}>
          {mode !== 'picker' && (
            <IconButton size="small" onClick={reset} disabled={isBusy} aria-label="Back">
              <ArrowBackIcon fontSize="small" />
            </IconButton>
          )}
          Add Knowledge
        </Stack>
      </DialogTitle>
      <DialogContent>
        {mode === 'picker' && (
          <Grid container spacing={1.5} sx={{ mt: 0.5 }}>
            <Grid size={6}>
              <Card variant="outlined">
                <CardActionArea onClick={() => setMode('Files')} sx={{ p: 1.5 }}>
                  <Stack spacing={0.5} sx={{ alignItems: 'flex-start' }}>
                    <UploadFileIcon color="primary" />
                    <Typography variant="subtitle2">Files</Typography>
                    <Typography variant="caption" color="text.secondary">
                      Upload PDF, Word, Excel, text, or markdown files
                    </Typography>
                  </Stack>
                </CardActionArea>
              </Card>
            </Grid>
            {TEXT_SOURCE_MODES.map((s) => (
              <Grid size={6} key={s.mode}>
                <Card variant="outlined">
                  <CardActionArea onClick={() => setMode(s.mode)} sx={{ p: 1.5 }}>
                    <Stack spacing={0.5} sx={{ alignItems: 'flex-start' }}>
                      {s.icon}
                      <Typography variant="subtitle2">{s.label}</Typography>
                      <Typography variant="caption" color="text.secondary">
                        {s.helper}
                      </Typography>
                    </Stack>
                  </CardActionArea>
                </Card>
              </Grid>
            ))}
          </Grid>
        )}

        {mode === 'Files' && (
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Stepper activeStep={uploadStep} alternativeLabel>
              {UPLOAD_STEPS.map((label) => (
                <Step key={label}>
                  <StepLabel>{label}</StepLabel>
                </Step>
              ))}
            </Stepper>

            {uploadStep === 0 && (
              <>
                <Button variant="outlined" component="label" startIcon={<UploadFileIcon />}>
                  Choose files
                  <input
                    ref={fileInputRef}
                    type="file"
                    hidden
                    multiple
                    onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
                  />
                </Button>
                {files.length > 0 && (
                  <List dense>
                    {files.map((f) => (
                      <ListItem key={f.name}>
                        <ListItemIcon>
                          <InsertDriveFileIcon fontSize="small" />
                        </ListItemIcon>
                        <ListItemText primary={f.name} secondary={`${(f.size / 1024).toFixed(1)} KB`} />
                      </ListItem>
                    ))}
                  </List>
                )}
              </>
            )}

            {(uploadStep === 1 || uploadStep === 2) && (
              <Box sx={{ py: 2 }}>
                <LinearProgress />
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
                  {uploadStep === 1 ? 'Uploading files…' : 'Queuing for processing…'}
                </Typography>
              </Box>
            )}

            {uploadStep === 3 && (
              <List dense>
                {uploadResults.map((r) => (
                  <ListItem key={r.name}>
                    <ListItemIcon>
                      {r.ok ? (
                        <CheckCircleOutlineOutlined fontSize="small" color="success" />
                      ) : (
                        <ErrorOutlineIcon fontSize="small" color="error" />
                      )}
                    </ListItemIcon>
                    <ListItemText primary={r.name} secondary={r.ok ? 'Uploaded' : r.error} />
                  </ListItem>
                ))}
              </List>
            )}
          </Stack>
        )}

        {mode === 'Url' && (
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Website URL"
              placeholder="https://example.com/docs"
              fullWidth
              autoFocus
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              error={!!error}
              helperText={error}
            />
          </Stack>
        )}

        {(mode === 'Faq' || mode === 'Database' || mode === 'Integration') && (
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label={mode === 'Faq' ? 'Questions and answers' : mode === 'Database' ? 'Extracted data' : 'Content'}
              placeholder={
                mode === 'Faq'
                  ? 'Q: ...\nA: ...'
                  : 'Paste the text to index — R2WAI does not connect to a live source for this type yet'
              }
              fullWidth
              multiline
              minRows={6}
              autoFocus
              value={content}
              onChange={(e) => setContent(e.target.value)}
              error={!!error}
              helperText={error}
            />
          </Stack>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} disabled={isBusy}>
          {mode === 'Files' && uploadStep === 3 ? 'Close' : 'Cancel'}
        </Button>
        {mode === 'Files' && uploadStep === 0 && (
          <Button variant="contained" disabled={files.length === 0} onClick={() => void handleUploadFiles()}>
            Upload {files.length > 0 ? `(${files.length})` : ''}
          </Button>
        )}
        {mode === 'Url' && (
          <Button variant="contained" disabled={!url || isBusy} onClick={() => void handleAddTextSource('Url')}>
            {isBusy ? 'Adding…' : 'Add'}
          </Button>
        )}
        {(mode === 'Faq' || mode === 'Database' || mode === 'Integration') && (
          <Button variant="contained" disabled={!content || isBusy} onClick={() => void handleAddTextSource(mode)}>
            {isBusy ? 'Adding…' : 'Add'}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  )
}
