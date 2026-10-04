import * as signalR from '@microsoft/signalr'
import { tokenStorage } from '../auth/tokenStorage'

/**
 * Connects to /hubs/status — previously unconnected by any client (Blazor
 * never wired it up either; confirmed by grep, zero references). The
 * backend's IWorkflowStatusService that would emit these events is likewise
 * registered in DI but never actually called from WorkflowBridge or
 * StepActivityFactory during a real run — this connection and its event
 * wiring are real and correct, but no live step events will arrive from an
 * actual execution until that backend gap is closed (a Track A non-goal;
 * flagged here so it isn't mistaken for a frontend bug).
 */
export function createStatusHubConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl('/hubs/status', {
      accessTokenFactory: () => tokenStorage.getToken() ?? '',
    })
    .withAutomaticReconnect()
    .build()
}

export interface StepStatusEvent {
  stepName: string
  stepIndex: number
  status: 'started' | 'completed' | 'failed'
  output?: string
  error?: string
  timestamp: string
}

export interface StatusHubCallbacks {
  onStepEvent: (event: StepStatusEvent) => void
  onWorkflowCompleted?: () => void
  onWorkflowFailed?: (error: string) => void
}

export function wireStatusHubEvents(connection: signalR.HubConnection, callbacks: StatusHubCallbacks): void {
  connection.on('WorkflowStepStarted', (stepName: string, stepIndex: number, timestamp: string) => {
    callbacks.onStepEvent({ stepName, stepIndex, status: 'started', timestamp })
  })
  connection.on('WorkflowStepCompleted', (stepName: string, stepIndex: number, output: string | null, timestamp: string) => {
    callbacks.onStepEvent({ stepName, stepIndex, status: 'completed', output: output ?? undefined, timestamp })
  })
  connection.on('WorkflowStepFailed', (stepName: string, stepIndex: number, error: string, timestamp: string) => {
    callbacks.onStepEvent({ stepName, stepIndex, status: 'failed', error, timestamp })
  })
  connection.on('WorkflowCompleted', () => callbacks.onWorkflowCompleted?.())
  connection.on('WorkflowFailed', (_instanceId: string, error: string) => callbacks.onWorkflowFailed?.(error))
}
