import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { Alert, Snackbar } from '@mui/material'
import { SnackbarContext, type SnackbarSeverity } from './SnackbarContext'

interface QueuedMessage {
  key: number
  message: string
  severity: SnackbarSeverity
}

let nextKey = 0

export function SnackbarProvider({ children }: { children: ReactNode }) {
  const [queue, setQueue] = useState<QueuedMessage[]>([])
  const current = queue[0]

  const notify = useCallback((message: string, severity: SnackbarSeverity = 'info') => {
    setQueue((prev) => [...prev, { key: nextKey++, message, severity }])
  }, [])

  const handleClose = useCallback(() => {
    setQueue((prev) => prev.slice(1))
  }, [])

  const value = useMemo(() => ({ notify }), [notify])

  return (
    <SnackbarContext.Provider value={value}>
      {children}
      <Snackbar
        key={current?.key}
        open={!!current}
        autoHideDuration={4000}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        {current ? (
          <Alert onClose={handleClose} severity={current.severity} variant="filled" sx={{ width: '100%' }}>
            {current.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </SnackbarContext.Provider>
  )
}
