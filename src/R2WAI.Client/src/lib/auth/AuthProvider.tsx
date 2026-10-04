import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { AuthContext, type AuthStatus } from './AuthContext'
import { tokenStorage } from './tokenStorage'
import { decodeJwt } from './jwt'
import {
  entraIdLoginRequest,
  loginRequest,
  logoutRequest,
  meRequest,
  onSessionExpired,
} from './authClient'
import { initMsal, isEntraConfigured, loginMicrosoft } from './msal'
import { AuthError, type UserInfo } from './types'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<UserInfo | null>(null)
  const [claims, setClaims] = useState<Record<string, string | string[]> | null>(null)
  const [isEntraAvailable, setIsEntraAvailable] = useState(false)
  // Mirrors JwtAuthenticationStateProvider's _lastValidatedToken: once a token has
  // been confirmed (by /me, or because it was just issued by login/refresh/entra-id),
  // don't re-hit /me for it again this session.
  const lastValidatedToken = useRef<string | null>(null)

  function enterAuthenticated(token: string, userInfo: UserInfo) {
    const decoded = decodeJwt(token)
    lastValidatedToken.current = token
    setClaims(decoded?.claims ?? null)
    setUser(userInfo)
    setStatus('authenticated')
  }

  function enterAnonymous() {
    lastValidatedToken.current = null
    setClaims(null)
    setUser(null)
    setStatus('anonymous')
  }

  useEffect(() => {
    let cancelled = false

    async function bootstrap() {
      const token = tokenStorage.getToken()
      if (!token) {
        if (!cancelled) enterAnonymous()
        return
      }

      const decoded = decodeJwt(token)
      if (!decoded) {
        tokenStorage.clearAll()
        if (!cancelled) enterAnonymous()
        return
      }

      if (token === lastValidatedToken.current) {
        const storedUser = tokenStorage.getUserJson()
        const userInfo = storedUser ? (JSON.parse(storedUser) as UserInfo) : null
        if (!cancelled && userInfo) enterAuthenticated(token, userInfo)
        return
      }

      const result = await meRequest(token)
      if (cancelled) return

      if (result.outcome === 'rejected') {
        tokenStorage.clearAll()
        enterAnonymous()
        return
      }

      const storedUser = tokenStorage.getUserJson()
      const userInfo =
        result.outcome === 'valid' ? result.user : storedUser ? (JSON.parse(storedUser) as UserInfo) : null

      if (userInfo) {
        enterAuthenticated(token, userInfo)
      } else {
        enterAnonymous()
      }
    }

    void bootstrap()
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    return onSessionExpired(() => {
      enterAnonymous()
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (!isEntraConfigured()) return
    void initMsal().then((ok) => setIsEntraAvailable(ok))
  }, [])

  const value = useMemo(
    () => ({
      status,
      user,
      claims,
      isEntraAvailable,
      async login(email: string, password: string, mfaCode?: string) {
        const response = await loginRequest({ email, password, mfaCode })
        tokenStorage.setToken(response.token)
        tokenStorage.setRefreshToken(response.refreshToken)
        tokenStorage.setUserJson(JSON.stringify(response.user))
        enterAuthenticated(response.token, response.user)
      },
      async loginWithEntra(mfaCode?: string) {
        const result = await loginMicrosoft()
        if (!result.success || !result.idToken) {
          if (result.error === 'cancelled') return
          throw new AuthError(result.error ?? 'Microsoft sign-in failed')
        }
        // A fresh idToken every attempt (loginMicrosoft() re-runs MSAL, silently reusing its own
        // cached Entra session — no second Microsoft prompt) — the retry-with-code call is a new
        // token exchange, not a resubmission of the first one.
        const response = await entraIdLoginRequest({ idToken: result.idToken, mfaCode })
        tokenStorage.setToken(response.token)
        tokenStorage.setRefreshToken(response.refreshToken)
        tokenStorage.setUserJson(JSON.stringify(response.user))
        enterAuthenticated(response.token, response.user)
      },
      async logout() {
        const token = tokenStorage.getToken()
        if (token) await logoutRequest(token)
        tokenStorage.clearAll()
        enterAnonymous()
      },
      async refreshUser() {
        const token = tokenStorage.getToken()
        if (!token) return
        const result = await meRequest(token)
        if (result.outcome === 'valid') {
          tokenStorage.setUserJson(JSON.stringify(result.user))
          setUser(result.user)
        }
      },
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [status, user, claims, isEntraAvailable],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
