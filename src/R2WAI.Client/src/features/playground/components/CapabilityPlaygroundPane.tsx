import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Alert, Button, Stack, Typography } from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import { ApiRequestError } from '../../../lib/api/fetchJson'
import { getCapability, testCapability } from '../../tools/api'
import type { TestConnectionResult } from '../../tools/types'

interface CapabilityPlaygroundPaneProps {
  capabilityId: string
  onInspectorData?: (data: {
    result: TestConnectionResult | null
    isPending: boolean
    testedAt: string | null
    riskLevel: string | null
    requiredRole: string | null
  }) => void
}

/**
 * Center panel for Capability (Tool/API) mode (redesign Phase 6). Previously this tab was an
 * honest stub explaining there was no backend test endpoint — that gap closed when Phase 5 added
 * a persisted LastTestStatus and IntegrationsController.Test governance check. Reuses that same
 * real endpoint (DynamicToolExecutor → HttpTool, the exact path an AI agent's own call takes).
 */
export function CapabilityPlaygroundPane({ capabilityId, onInspectorData }: CapabilityPlaygroundPaneProps) {
  const [result, setResult] = useState<TestConnectionResult | null>(null)

  useEffect(() => setResult(null), [capabilityId])

  const capabilityQuery = useQuery({
    queryKey: ['playground-capability', capabilityId],
    queryFn: () => getCapability(capabilityId),
    enabled: !!capabilityId,
  })

  const testMutation = useMutation({
    mutationFn: () => testCapability(capabilityId),
    onSuccess: (r) => setResult(r),
    onError: (err) => setResult({ success: false, message: err instanceof ApiRequestError ? err.message : 'Test request failed.' }),
  })

  useEffect(() => {
    onInspectorData?.({
      result,
      isPending: testMutation.isPending,
      testedAt: capabilityQuery.data?.lastTestedAt ?? null,
      riskLevel: capabilityQuery.data?.riskLevel ?? null,
      requiredRole: capabilityQuery.data?.requiredRole ?? null,
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [result, testMutation.isPending, capabilityQuery.data])

  return (
    <Stack sx={{ height: '100%' }}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ p: 1.5, alignItems: 'center', borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
          Tool / API Test
        </Typography>
        <Button
          size="small"
          variant="contained"
          startIcon={<PlayArrowIcon />}
          disabled={!capabilityId || testMutation.isPending}
          onClick={() => testMutation.mutate()}
        >
          {testMutation.isPending ? 'Testing…' : 'Run Test'}
        </Button>
      </Stack>

      <Stack sx={{ flexGrow: 1, p: 2, overflowY: 'auto' }} spacing={2}>
        <Typography variant="caption" color="text.secondary">
          Sends a real request to the configured endpoint through the same governance gate (role, risk, approval)
          an AI agent's own call goes through — not a bypass, not a mock.
        </Typography>
        {capabilityQuery.data && (
          <Typography variant="body2">
            <strong>{capabilityQuery.data.name}</strong>
            {capabilityQuery.data.description ? ` — ${capabilityQuery.data.description}` : ''}
          </Typography>
        )}
        {!testMutation.isPending && result && <Alert severity={result.success ? 'success' : 'warning'}>{result.message}</Alert>}
        {!result && !testMutation.isPending && (
          <Typography variant="body2" color="text.secondary">
            Click "Run Test" to call this tool directly and see the real result.
          </Typography>
        )}
      </Stack>
    </Stack>
  )
}
