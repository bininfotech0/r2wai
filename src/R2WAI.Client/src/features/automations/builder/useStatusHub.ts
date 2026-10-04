import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { createStatusHubConnection, wireStatusHubEvents, type StepStatusEvent } from '../../../lib/signalr/statusHub'

export type StatusHubConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

/**
 * Per-run subscription to /hubs/status for a given workflow instance. See
 * lib/signalr/statusHub.ts's doc comment: real, correct wiring, but no
 * events will arrive until the backend actually calls IWorkflowStatusService
 * during execution (a known, pre-existing gap outside Track A's scope).
 */
export function useStatusHub(instanceId: string | null) {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const [connectionState, setConnectionState] = useState<StatusHubConnectionState>('connecting')
  const [stepEvents, setStepEvents] = useState<StepStatusEvent[]>([])
  const [workflowStatus, setWorkflowStatus] = useState<'running' | 'completed' | 'failed' | null>(null)

  useEffect(() => {
    const connection = createStatusHubConnection()
    connectionRef.current = connection

    wireStatusHubEvents(connection, {
      onStepEvent: (event) => setStepEvents((prev) => [...prev, event]),
      onWorkflowCompleted: () => setWorkflowStatus('completed'),
      onWorkflowFailed: () => setWorkflowStatus('failed'),
    })

    connection.onreconnecting(() => setConnectionState('reconnecting'))
    connection.onreconnected(() => {
      setConnectionState('connected')
      if (instanceId) void connection.invoke('SubscribeToWorkflow', instanceId).catch(() => {})
    })
    connection.onclose(() => setConnectionState('disconnected'))

    connection
      .start()
      .then(() => {
        setConnectionState('connected')
        if (instanceId) return connection.invoke('SubscribeToWorkflow', instanceId)
      })
      .catch(() => setConnectionState('disconnected'))

    return () => {
      void connection.stop()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [instanceId])

  return { connectionState, stepEvents, workflowStatus }
}
