import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Card, CardContent, Chip, Grid, Stack, Typography } from '@mui/material'
import DownloadIcon from '@mui/icons-material/Download'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { downloadReport, generateReport, listReports } from '../api'
import { downloadBlob } from '../downloadBlob'

const GENERATABLE: { type: 'cost' | 'assistant'; label: string }[] = [
  { type: 'cost', label: 'Cost Report' },
  { type: 'assistant', label: 'Assistant Report' },
]
const DOWNLOADABLE_TYPES = ['cost', 'assistant', 'usage']

export function ReportsTab() {
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()
  const [downloading, setDownloading] = useState<string | null>(null)

  const query = useQuery({ queryKey: ['monitor', 'reports'], queryFn: listReports })

  const generateMutation = useMutation({
    mutationFn: generateReport,
    onSuccess: () => {
      notify('Report generated', 'success')
      void queryClient.invalidateQueries({ queryKey: ['monitor', 'reports'] })
    },
    onError: () => notify('Failed to generate report', 'error'),
  })

  async function handleDownload(type: string, format: 'csv' | 'json') {
    setDownloading(`${type}-${format}`)
    try {
      const blob = await downloadReport(type, format)
      downloadBlob(blob, `${type}-report.${format}`)
    } catch {
      notify('Failed to download report', 'error')
    } finally {
      setDownloading(null)
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="caption" color="text.secondary">
        Reports are computed live from current counts, not persisted records — regenerating produces a fresh snapshot
        each time.
      </Typography>

      <Stack direction="row" spacing={1}>
        {GENERATABLE.map((g) => (
          <Button
            key={g.type}
            size="small"
            variant="outlined"
            onClick={() => generateMutation.mutate(g.type)}
            disabled={generateMutation.isPending}
          >
            Generate {g.label}
          </Button>
        ))}
      </Stack>

      <Grid container spacing={2}>
        {(query.data?.items ?? []).map((r) => (
          <Grid key={r.id} size={{ xs: 12, sm: 6, md: 4 }}>
            <Card variant="outlined">
              <CardContent>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                  <Typography variant="subtitle2" sx={{ flexGrow: 1 }} noWrap>
                    {r.name}
                  </Typography>
                  <Chip label={r.status} size="small" color="success" variant="outlined" />
                </Stack>
                <Typography variant="body2" color="text.secondary">
                  {r.type} · {r.period}
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                  {new Date(r.generatedAt).toLocaleString()}
                </Typography>
                {DOWNLOADABLE_TYPES.includes(r.type.toLowerCase()) && (
                  <Stack direction="row" spacing={1}>
                    <Button
                      size="small"
                      startIcon={<DownloadIcon />}
                      disabled={downloading === `${r.type.toLowerCase()}-csv`}
                      onClick={() => void handleDownload(r.type.toLowerCase(), 'csv')}
                    >
                      CSV
                    </Button>
                    <Button
                      size="small"
                      startIcon={<DownloadIcon />}
                      disabled={downloading === `${r.type.toLowerCase()}-json`}
                      onClick={() => void handleDownload(r.type.toLowerCase(), 'json')}
                    >
                      JSON
                    </Button>
                  </Stack>
                )}
              </CardContent>
            </Card>
          </Grid>
        ))}
        {(query.data?.items ?? []).length === 0 && (
          <Grid size={12}>
            <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 4 }}>
              No report data available yet.
            </Typography>
          </Grid>
        )}
      </Grid>
    </Stack>
  )
}
