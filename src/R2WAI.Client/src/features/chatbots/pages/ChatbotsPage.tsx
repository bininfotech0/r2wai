import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button, Stack, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch'
import type { GridColDef } from '@mui/x-data-grid'
import { useNavigate } from 'react-router-dom'
import { DataTable } from '../../../components/data/DataTable'
import { StatusBadge } from '../../../components/StatusBadge'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createChatbot, listChatbots } from '../api'
import { CreateChatbotDialog } from '../dialogs/CreateChatbotDialog'
import type { ChatbotDto } from '../types'

const QUERY_KEY = 'chatbots'

export function ChatbotsPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)

  const query = useQuery({
    queryKey: [QUERY_KEY, page],
    queryFn: () => listChatbots(page, 20),
  })

  const createMutation = useMutation({
    mutationFn: createChatbot,
    onSuccess: (chatbot) => {
      notify('Chatbot created', 'success')
      setCreateOpen(false)
      void queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
      navigate(`/chatbots/${chatbot.id}`)
    },
    onError: () => notify('Failed to create chatbot', 'error'),
  })

  const columns: GridColDef<ChatbotDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    {
      field: 'status',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <StatusBadge status={params.value} toneMap={{ Paused: 'warning' }} />,
    },
    {
      field: 'voiceEnabled',
      headerName: 'Voice',
      width: 90,
      renderCell: (params) => (params.value ? 'Enabled' : '—'),
    },
    {
      field: 'assistantId',
      headerName: 'Assistant',
      width: 130,
      renderCell: (params) => (params.value ? 'Linked' : '—'),
    },
    {
      field: 'id',
      headerName: 'Website widget',
      width: 170,
      sortable: false,
      // Labelled "Website widget" here rather than reusing the channel name from the
      // deployment overview, so this row action reads as an action rather than as a second
      // navigation entry for the same destination.
      renderCell: (params) => (
        <Button
          size="small"
          variant="outlined"
          onClick={(event) => {
            // DataTable treats a row click as "open this record"; stop this from also firing.
            event.stopPropagation()
            navigate(`/deploy/widget/${params.id as string}`)
          }}
        >
          Configure
        </Button>
      ),
    },
  ]

  return (
    <Box>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ alignItems: { xs: 'stretch', sm: 'center' }, justifyContent: 'space-between', mb: 2 }}>
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Publish
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Embeddable chat surfaces for assistants — publishable to a website via a script tag.
          </Typography>
        </Box>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          {/* Distribution lives under Chatbots rather than in its own nav section, so this is
              the one door to the cross-chatbot overview. */}
          <Button sx={{ width: { xs: '100%', sm: 'auto' } }} variant="outlined" startIcon={<RocketLaunchIcon />} onClick={() => navigate('/deploy')}>
            Deployment overview
          </Button>
          <Button sx={{ width: { xs: '100%', sm: 'auto' } }} variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            New Chatbot
          </Button>
        </Stack>
      </Stack>

      <DataTable
        rows={query.data?.items ?? []}
        columns={columns}
        loading={query.isLoading}
        error={query.error}
        onRetry={() => void query.refetch()}
        rowCount={query.data?.totalCount ?? 0}
        page={page}
        pageSize={20}
        onPageChange={setPage}
        onRowClick={(id) => navigate(`/chatbots/${id}`)}
      />

      <CreateChatbotDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={(values) => createMutation.mutate(values)}
        isSubmitting={createMutation.isPending}
      />
    </Box>
  )
}
