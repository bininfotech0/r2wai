import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { connectNotificationHub, type HubNotification } from '../signalr/notificationHub'
import { useAuth } from '../auth/useAuth'
import { listNotifications, markAllNotificationsRead, markNotificationRead } from './api'
import { useSnackbar } from './useSnackbar'

export interface FeedNotification extends HubNotification {
  id: string
  read: boolean
  link: string | null
}

interface NotificationFeedContextValue {
  notifications: FeedNotification[]
  unreadCount: number
  historyLoading: boolean
  historyError: boolean
  retryHistory: () => void
  markAllRead: () => void
  markRead: (id: string) => void
}

const NotificationFeedContext = createContext<NotificationFeedContextValue | null>(null)

const MAX_NOTIFICATIONS = 50

/**
 * One shared SignalR connection + feed, consumed by both NotificationBell
 * (popover) and InboxPage (full list) — avoids each opening its own
 * /hubs/notification connection and seeing divergent state.
 *
 * Backed by the Notifications table server-side: history is fetched once on
 * connect, and live pushes (which also persist server-side, see
 * NotificationService.SendAsync) are prepended as they arrive. Read state is
 * persisted via markRead/markAllRead rather than staying client-only.
 */
export function NotificationFeedProvider({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const { notify } = useSnackbar()
  const [notifications, setNotifications] = useState<FeedNotification[]>([])
  const [historyState, setHistoryState] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle')
  const [historyAttempt, setHistoryAttempt] = useState(0)
  const connectionRef = useRef<ReturnType<typeof connectNotificationHub> | null>(null)
  const seenIds = useRef<Set<string>>(new Set())

  useEffect(() => {
    if (status !== 'authenticated') {
      setHistoryState('idle')
      return
    }

    let cancelled = false
    setHistoryState('loading')
    listNotifications(1, MAX_NOTIFICATIONS)
      .then((result) => {
        if (cancelled) return
        for (const item of result.items) seenIds.current.add(item.id)
        setNotifications(
          result.items.map((item) => ({
            id: item.id,
            title: item.title,
            message: item.message,
            type: item.type,
            timestamp: new Date(item.createdAt).getTime(),
            read: item.isRead,
            link: item.link,
          })),
        )
        setHistoryState('ready')
      })
      .catch(() => {
        if (!cancelled) setHistoryState('error')
      })
    return () => {
      cancelled = true
    }
  }, [status, historyAttempt])

  useEffect(() => {
    if (status !== 'authenticated') return

    const connection = connectNotificationHub({
      onNotification: (n) => {
        const id = n.id ?? `${n.timestamp}-${Math.random().toString(36).slice(2, 8)}`
        if (seenIds.current.has(id)) return
        seenIds.current.add(id)
        setNotifications((prev) => [{ ...n, id, read: false, link: null }, ...prev].slice(0, MAX_NOTIFICATIONS))
      },
    })
    connectionRef.current = connection
    connection.start().catch(() => {
      // Real-time notifications unavailable — non-fatal, page still works.
    })

    return () => {
      void connection.stop()
      connectionRef.current = null
      seenIds.current.clear()
    }
  }, [status])

  const unreadCount = notifications.filter((n) => !n.read).length
  const historyLoading = status === 'authenticated' && (historyState === 'idle' || historyState === 'loading')

  function retryHistory() {
    setHistoryAttempt((attempt) => attempt + 1)
  }

  async function markAllRead() {
    const unreadIds = new Set(notifications.filter((n) => !n.read).map((n) => n.id))
    if (unreadIds.size === 0) return

    setNotifications((prev) => prev.map((n) => (unreadIds.has(n.id) ? { ...n, read: true } : n)))
    try {
      await markAllNotificationsRead()
    } catch {
      setNotifications((prev) => prev.map((n) => (unreadIds.has(n.id) ? { ...n, read: false } : n)))
      notify('Could not mark notifications as read. Try again.', 'error')
    }
  }

  async function markRead(id: string) {
    const notification = notifications.find((n) => n.id === id)
    if (!notification || notification.read) return

    setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, read: true } : n)))
    try {
      await markNotificationRead(id)
    } catch {
      setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, read: false } : n)))
      notify('Could not update this notification. Try again.', 'error')
    }
  }

  return (
    <NotificationFeedContext.Provider
      value={{
        notifications,
        unreadCount,
        historyLoading,
        historyError: historyState === 'error',
        retryHistory,
        markAllRead,
        markRead,
      }}
    >
      {children}
    </NotificationFeedContext.Provider>
  )
}

export function useNotificationFeed(): NotificationFeedContextValue {
  const ctx = useContext(NotificationFeedContext)
  if (!ctx) throw new Error('useNotificationFeed must be used within a NotificationFeedProvider')
  return ctx
}
