import { createContext } from 'react'
import type { UserInfo } from './types'

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: UserInfo | null
  claims: Record<string, string | string[]> | null
  login: (email: string, password: string, mfaCode?: string) => Promise<void>
  loginWithEntra: (mfaCode?: string) => Promise<void>
  logout: () => Promise<void>
  refreshUser: () => Promise<void>
  isEntraAvailable: boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)
