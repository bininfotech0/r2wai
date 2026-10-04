import * as signalR from '@microsoft/signalr'
import { tokenStorage } from '../auth/tokenStorage'

export interface HubNotification {
  id: string | null
  title: string
  message: string
  type: string | null
  timestamp: number
}

export interface NotificationHubCallbacks {
  onNotification: (notification: HubNotification) => void
  onReconnecting?: () => void
  onReconnected?: () => void
  onClosed?: () => void
}

/**
 * Connects to /hubs/notification — the simplest of the three hubs (see the
 * migration plan's hard/risky-pieces sequencing: notifications first, to
 * prove hub-through-gateway connectivity before Phase 5's harder streaming
 * chat hub). JWT is passed via query string, matching ChatHub/StatusHub
 * (browsers can't set a WebSocket Authorization header).
 */
export function connectNotificationHub(callbacks: NotificationHubCallbacks): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/notification', {
      accessTokenFactory: () => tokenStorage.getToken() ?? '',
    })
    .withAutomaticReconnect()
    .build()

  connection.on(
    'ReceiveNotification',
    (id: string | null, title: string, message: string, type: string | null, createdAt: string) => {
      callbacks.onNotification({ id, title, message, type, timestamp: new Date(createdAt).getTime() || Date.now() })
    },
  )

  connection.onreconnecting(() => callbacks.onReconnecting?.())
  connection.onreconnected(() => callbacks.onReconnected?.())
  connection.onclose(() => callbacks.onClosed?.())

  return connection
}
