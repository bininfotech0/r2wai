import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Box, Button } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch'
import ForumOutlined from '@mui/icons-material/ForumOutlined'
import type { GridColDef } from '@mui/x-data-grid'
import { useNavigate } from 'react-router-dom'
import { DataTable } from '../../../components/data/DataTable'
import { NameCell } from '../../../components/data/NameCell'
import { RowActionsMenu } from '../../../components/data/RowActionsMenu'
import { queryKeys } from '../../../lib/api/queryKeys'
import { listAssistants } from '../../assistants/api'
import { StatusBadge } from '../../../components/StatusBadge'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { createChatbot, listChatbots } from '../api'
import { CreateChatbotDialog } from '../dialogs/CreateChatbotDialog'
import type { ChatbotDto } from '../types'
import { PageHeader } from '../../../components/PageHeader'

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

  // Chatbots carry only the agent's id; names come from the agent list (same bounded query the
  // Knowledge page uses for its "Used by" column).
  const agentsQuery = useQuery({
    queryKey: queryKeys.assistants.forUsageCount,
    queryFn: () => listAssistants(1, 200, ''),
  })
  const agentName = new Map((agentsQuery.data?.items ?? []).map((a) => [a.id, a.name]))

  const columns: GridColDef<ChatbotDto>[] = [
    {
      field: 'name',
      headerName: 'Name',
      flex: 1,
      minWidth: 240,
      renderCell: ({ row }) => (
        <NameCell
          icon={ForumOutlined}
          name={row.name}
          secondary={row.assistantId ? `Answers as ${agentName.get(row.assistantId) ?? 'a linked agent'}` : 'No agent linked'}
        />
      ),
    },
    {
      field: 'status',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <StatusBadge status={params.value} toneMap={{ Paused: 'warning' }} />,
    },
    {
      field: 'assistantId',
      headerName: 'Agent',
      width: 200,
      valueGetter: (_, row) => (row.assistantId ? agentName.get(row.assistantId) ?? 'Linked' : '—'),
    },
    {
      field: 'voiceEnabled',
      headerName: 'Voice',
      width: 90,
      renderCell: (params) => (params.value ? 'Enabled' : '—'),
    },
    {
      field: 'actions',
      headerName: '',
      width: 56,
      sortable: false,
      disableColumnMenu: true,
      renderCell: ({ row }) => (
        <RowActionsMenu
          label={row.name}
          actions={[
            { label: 'Open', onSelect: () => navigate(`/chatbots/${row.id}`) },
            { label: 'Website widget', onSelect: () => navigate(`/deploy/widget/${row.id}`) },
          ]}
        />
      ),
    },
  ]

  return (
    <Box>
      <PageHeader
        title="Publish"
        description="Chatbots put a published agent on your website, or on any system through a webhook."
        actions={
          <>
            {/* Distribution lives under Chatbots rather than in its own nav section, so this is
                the one door to the cross-chatbot overview. */}
            <Button variant="outlined" startIcon={<RocketLaunchIcon />} onClick={() => navigate('/deploy')}>
              Deployment overview
            </Button>
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
              New Chatbot
            </Button>
          </>
        }
      />

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
