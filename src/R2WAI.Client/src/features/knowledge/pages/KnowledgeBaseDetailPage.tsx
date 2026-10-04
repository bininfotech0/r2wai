import { queryKeys } from '../../../lib/api/queryKeys'
import { useState } from 'react'
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Paper,
  Stack,
  Tab,
  Tabs,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import RefreshIcon from '@mui/icons-material/Refresh'
import DifferenceIcon from '@mui/icons-material/Difference'
import VisibilityIcon from '@mui/icons-material/Visibility'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { EmptyState } from '../../../components/EmptyState'
import { ErrorState } from '../../../components/ErrorState'
import { StatusBadge } from '../../../components/StatusBadge'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { listAssistants } from '../../assistants/api'
import {
  deleteDocument,
  deleteKnowledgeBase,
  deleteSource,
  getDocumentContent,
  getKnowledgeBase,
  listDocuments,
  reindexKnowledgeBase,
  updateKnowledgeBase,
} from '../api'
import { AddKnowledgeDialog } from '../dialogs/AddKnowledgeDialog'
import { CompareDocumentsDialog } from '../dialogs/CompareDocumentsDialog'
import { CreateEditKnowledgeBaseDialog, type KnowledgeBaseFormValues } from '../dialogs/CreateEditKnowledgeBaseDialog'
import type { DocumentDto } from '../types'

const STATUS_COLOR: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
  Active: 'success',
  Creating: 'warning',
  Failed: 'error',
  Ready: 'success',
  Processing: 'warning',
  Uploading: 'warning',
}

const TABS = ['Overview', 'Sources', 'Documents', 'Retrieval', 'Security', 'Usage'] as const
type Tab = (typeof TABS)[number]

export function KnowledgeBaseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [tab, setTab] = useState<Tab>('Overview')
  const [page, setPage] = useState(1)
  const [editOpen, setEditOpen] = useState(false)
  const [addOpen, setAddOpen] = useState(false)
  const [compareOpen, setCompareOpen] = useState(false)
  const [deleteKbOpen, setDeleteKbOpen] = useState(false)
  const [deletingSourceId, setDeletingSourceId] = useState<string | null>(null)
  const [deletingDoc, setDeletingDoc] = useState<DocumentDto | null>(null)
  const [viewingDoc, setViewingDoc] = useState<DocumentDto | null>(null)

  const kbQuery = useQuery({
    queryKey: ['knowledgebases', id],
    queryFn: () => getKnowledgeBase(id!),
    enabled: !!id,
  })

  const docsQuery = useQuery({
    queryKey: ['documents', id, page],
    queryFn: () => listDocuments(page, 20, id!),
    enabled: !!id && tab === 'Documents',
  })

  const contentQuery = useQuery({
    queryKey: ['document-content', viewingDoc?.id],
    queryFn: () => getDocumentContent(viewingDoc!.id),
    enabled: !!viewingDoc,
  })

  // No backend endpoint reports which assistants use a knowledge base — same reasoning as
  // KnowledgeLibraryPage's "Used By" column: derived client-side from the assistants list.
  const assistantsQuery = useQuery({
    queryKey: queryKeys.assistants.forKnowledgeBaseUsage(id),
    queryFn: () => listAssistants(1, 200, ''),
    enabled: !!id && tab === 'Usage',
  })
  const usingAssistants = (assistantsQuery.data?.items ?? []).filter((a) => a.knowledgeBaseId === id)

  const invalidateKb = () => queryClient.invalidateQueries({ queryKey: ['knowledgebases', id] })
  const invalidateDocs = () => queryClient.invalidateQueries({ queryKey: ['documents', id] })

  const updateMutation = useMutation({
    mutationFn: (values: KnowledgeBaseFormValues) => updateKnowledgeBase(id!, values),
    onSuccess: () => {
      notify('Knowledge base updated', 'success')
      setEditOpen(false)
      void invalidateKb()
    },
    onError: () => notify('Failed to update knowledge base', 'error'),
  })

  const deleteKbMutation = useMutation({
    mutationFn: () => deleteKnowledgeBase(id!),
    onSuccess: () => {
      notify('Knowledge base deleted', 'success')
      navigate('/knowledge')
    },
    onError: () => notify('Failed to delete knowledge base', 'error'),
  })

  const deleteSourceMutation = useMutation({
    mutationFn: (sourceId: string) => deleteSource(sourceId),
    onSuccess: () => {
      notify('Source removed', 'success')
      setDeletingSourceId(null)
      void invalidateKb()
    },
    onError: () => notify('Failed to remove source', 'error'),
  })

  const deleteDocMutation = useMutation({
    mutationFn: (docId: string) => deleteDocument(docId),
    onSuccess: () => {
      notify('Document deleted', 'success')
      setDeletingDoc(null)
      void invalidateDocs()
      void invalidateKb()
    },
    onError: () => notify('Failed to delete document', 'error'),
  })

  const reindexMutation = useMutation({
    mutationFn: () => reindexKnowledgeBase(id!),
    onSuccess: (result) => notify(`Reindexed ${result.reindexed} of ${result.totalDocuments} documents`, 'success'),
    onError: () => notify('Failed to reindex', 'error'),
  })

  const documentColumns: GridColDef<DocumentDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'fileType', headerName: 'Type', width: 90 },
    {
      field: 'fileSize',
      headerName: 'Size',
      width: 100,
      valueFormatter: (value: number) => `${(value / 1024).toFixed(1)} KB`,
    },
    {
      field: 'status',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => (
        <Chip label={params.value} size="small" color={STATUS_COLOR[params.value] ?? 'default'} variant="outlined" />
      ),
    },
    {
      field: 'actions',
      headerName: '',
      width: 90,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <IconButton
            size="small"
            aria-label="View content"
            onClick={(e) => {
              e.stopPropagation()
              setViewingDoc(params.row)
            }}
          >
            <VisibilityIcon fontSize="small" />
          </IconButton>
          <IconButton
            size="small"
            aria-label="Delete document"
            onClick={(e) => {
              e.stopPropagation()
              setDeletingDoc(params.row)
            }}
          >
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Stack>
      ),
    },
  ]

  if (kbQuery.isError) {
    return <ErrorState title="Unable to load knowledge base" description="The knowledge base details could not be retrieved." onRetry={() => void kbQuery.refetch()} />
  }

  if (kbQuery.isLoading || !kbQuery.data) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    )
  }

  const kb = kbQuery.data

  return (
    <Box>
      <Stack direction="row" sx={{ alignItems: 'center', mb: 0.5, gap: 1 }}>
        <Button component={RouterLink} to="/knowledge" startIcon={<ArrowBackIcon />} size="small">
          Knowledge
        </Button>
      </Stack>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Typography variant="h6" sx={{ fontWeight: 600 }}>
              {kb.name}
            </Typography>
            <Chip label={kb.status} size="small" color={STATUS_COLOR[kb.status] ?? 'default'} variant="outlined" />
          </Stack>
          <Typography variant="body2" color="text.secondary">
            {kb.description || 'No description'}
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button size="small" onClick={() => setEditOpen(true)}>
            Edit
          </Button>
          <Button
            size="small"
            startIcon={<RefreshIcon />}
            onClick={() => reindexMutation.mutate()}
            disabled={reindexMutation.isPending}
          >
            {reindexMutation.isPending ? 'Reindexing…' : 'Reindex'}
          </Button>
          <Button size="small" color="error" onClick={() => setDeleteKbOpen(true)}>
            Delete
          </Button>
        </Stack>
      </Stack>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
        {TABS.map((t) => (
          <Tab key={t} label={t} value={t} />
        ))}
      </Tabs>

      {tab === 'Overview' && (
        <Stack spacing={2}>
          <Stack direction="row" spacing={2} sx={{ flexWrap: 'wrap' }}>
            <Paper variant="outlined" sx={{ p: 2, minWidth: 140 }}>
              <Typography variant="caption" color="text.secondary">
                Sources
              </Typography>
              <Typography variant="h5" sx={{ fontWeight: 700 }}>
                {kb.sources.length}
              </Typography>
            </Paper>
            <Paper variant="outlined" sx={{ p: 2, minWidth: 140 }}>
              <Typography variant="caption" color="text.secondary">
                Documents
              </Typography>
              <Typography variant="h5" sx={{ fontWeight: 700 }}>
                {kb.documentCount}
              </Typography>
            </Paper>
            <Paper variant="outlined" sx={{ p: 2, minWidth: 140 }}>
              <Typography variant="caption" color="text.secondary">
                Classification
              </Typography>
              <Box sx={{ mt: 0.5 }}>
                <StatusBadge status={kb.dataClassification} toneMap={{ Public: 'success', Internal: 'info', Confidential: 'warning', Restricted: 'error' }} />
              </Box>
            </Paper>
          </Stack>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              About
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {kb.description || 'No description provided for this knowledge base.'}
            </Typography>
          </Paper>
        </Stack>
      )}

      {tab === 'Sources' && (
        <Box>
          <Box sx={{ mb: 1.5 }}>
            <Button size="small" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>
              Add Knowledge
            </Button>
          </Box>
          {kb.sources.length === 0 ? (
            <EmptyState
              title="No sources yet"
              description="Add a file, website, or text source to start building this knowledge base."
            />
          ) : (
            <List>
              {kb.sources.map((s) => (
                <ListItem
                  key={s.id}
                  divider
                  secondaryAction={
                    <IconButton size="small" aria-label="Remove source" onClick={() => setDeletingSourceId(s.id)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  }
                >
                  <ListItemText
                    primary={
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <Typography variant="body2" sx={{ fontWeight: 500 }}>
                          {s.type}
                          {s.url ? `: ${s.url}` : ''}
                        </Typography>
                        {s.status && <StatusBadge status={s.status} />}
                      </Stack>
                    }
                    secondary={
                      s.status === 'Failed' && s.error
                        ? `Failed: ${s.error}`
                        : `${s.chunkCount} chunk${s.chunkCount === 1 ? '' : 's'} indexed${s.indexedAt ? ` · last synced ${new Date(s.indexedAt).toLocaleString()}` : ''}`
                    }
                    slotProps={{ secondary: { color: s.status === 'Failed' ? 'error' : 'text.secondary' } }}
                  />
                </ListItem>
              ))}
            </List>
          )}
        </Box>
      )}

      {tab === 'Documents' && (
        <Box>
          <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'flex-end', mb: 1 }}>
            <Button
              size="small"
              startIcon={<DifferenceIcon />}
              disabled={(docsQuery.data?.items.length ?? 0) < 2}
              onClick={() => setCompareOpen(true)}
            >
              Compare Documents
            </Button>
          </Stack>
          <DataTable
            rows={docsQuery.data?.items ?? []}
            columns={documentColumns}
            loading={docsQuery.isLoading}
            error={docsQuery.error}
            onRetry={() => void docsQuery.refetch()}
            rowCount={docsQuery.data?.totalCount ?? 0}
            page={page}
            pageSize={20}
            onPageChange={setPage}
          />
        </Box>
      )}

      {tab === 'Retrieval' && (
        <Paper variant="outlined" sx={{ p: 2, maxWidth: 500 }}>
          <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
            Advanced — Retrieval configuration
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Set once at creation and applied to every source in this knowledge base. Not editable here.
          </Typography>
          <Stack spacing={1.5}>
            <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">
                Embedding model
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {kb.embeddingModel ?? '—'}
              </Typography>
            </Stack>
            <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">
                Chunk size
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {kb.chunkSize ?? '—'}
              </Typography>
            </Stack>
            <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
              <Typography variant="body2" color="text.secondary">
                Chunk overlap
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {kb.chunkOverlap ?? '—'}
              </Typography>
            </Stack>
          </Stack>
        </Paper>
      )}

      {tab === 'Security' && (
        <Stack spacing={2} sx={{ maxWidth: 560 }}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Data classification
            </Typography>
            <Box sx={{ mb: 1.5 }}>
              <StatusBadge status={kb.dataClassification} toneMap={{ Public: 'success', Internal: 'info', Confidential: 'warning', Restricted: 'error' }} />
            </Box>
            <Typography variant="body2" color="text.secondary">
              Governed by this tenant's Knowledge policy: if a ceiling is configured, an assistant's
              retrieval search against this knowledge base is blocked once its classification exceeds
              that ceiling — enforced server-side on every search, not just hidden from the UI. Change
              the classification via Edit.
            </Typography>
          </Paper>
          <Button component={RouterLink} to="/security" variant="outlined" size="small" sx={{ alignSelf: 'flex-start' }}>
            Open Security & Policies
          </Button>
        </Stack>
      )}

      {tab === 'Usage' && (
        <Box>
          {assistantsQuery.isLoading ? (
            <CircularProgress size={24} />
          ) : usingAssistants.length === 0 ? (
            <EmptyState title="Not used by any assistant yet" description="Link this knowledge base to an assistant from the assistant's Knowledge tab." />
          ) : (
            <List>
              {usingAssistants.map((a) => (
                <ListItem key={a.id} divider disablePadding>
                  <ListItemButton component={RouterLink} to={`/assistants/${a.id}`}>
                    <ListItemText primary={a.name} secondary={a.publishStatus} />
                  </ListItemButton>
                </ListItem>
              ))}
            </List>
          )}
        </Box>
      )}

      <CreateEditKnowledgeBaseDialog
        open={editOpen}
        onClose={() => setEditOpen(false)}
        onSubmit={(v) => updateMutation.mutate(v)}
        knowledgeBase={kb}
        isSubmitting={updateMutation.isPending}
      />

      <AddKnowledgeDialog
        open={addOpen}
        onClose={() => setAddOpen(false)}
        knowledgeBaseId={id!}
        onAdded={() => {
          void invalidateKb()
          void invalidateDocs()
        }}
      />

      <CompareDocumentsDialog open={compareOpen} onClose={() => setCompareOpen(false)} documents={docsQuery.data?.items ?? []} />

      <ConfirmDeleteDialog
        open={deleteKbOpen}
        onClose={() => setDeleteKbOpen(false)}
        onConfirm={() => deleteKbMutation.mutate()}
        title="Delete Knowledge Base"
        message={`Delete "${kb.name}"? This removes all its sources and documents. This cannot be undone.`}
        isDeleting={deleteKbMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deletingSourceId}
        onClose={() => setDeletingSourceId(null)}
        onConfirm={() => deletingSourceId && deleteSourceMutation.mutate(deletingSourceId)}
        title="Remove Source"
        message="Remove this source? Already-indexed content from it will remain until the next reindex."
        isDeleting={deleteSourceMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deletingDoc}
        onClose={() => setDeletingDoc(null)}
        onConfirm={() => deletingDoc && deleteDocMutation.mutate(deletingDoc.id)}
        title="Delete Document"
        message={`Delete "${deletingDoc?.name}"? This cannot be undone.`}
        isDeleting={deleteDocMutation.isPending}
      />

      <Dialog open={!!viewingDoc} onClose={() => setViewingDoc(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{viewingDoc?.name}</DialogTitle>
        <DialogContent>
          {contentQuery.isLoading ? (
            <CircularProgress size={24} />
          ) : (
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
              {contentQuery.data?.content || 'No text content available.'}
            </Typography>
          )}
        </DialogContent>
      </Dialog>
    </Box>
  )
}
