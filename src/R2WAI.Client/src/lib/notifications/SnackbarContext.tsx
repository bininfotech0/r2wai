import { createContext } from 'react'

export type SnackbarSeverity = 'success' | 'error' | 'warning' | 'info'

export interface SnackbarContextValue {
  notify: (message: string, severity?: SnackbarSeverity) => void
}

export const SnackbarContext = createContext<SnackbarContextValue | null>(null)
