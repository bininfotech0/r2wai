/**
 * sessionStorage schema — must stay byte-compatible with Blazor's
 * TokenStorageService (src/R2WAI.Web/Services/TokenStorageService.cs) so a
 * session survives navigating between not-yet-migrated Blazor pages and
 * already-cut-over React pages on the same origin during the strangler-fig
 * migration (see Phase 3 of the migration plan).
 */
const TOKEN_KEY = 'r2wai_token'
const REFRESH_TOKEN_KEY = 'r2wai_refresh_token'
const USER_KEY = 'r2wai_user'

function safeGet(key: string): string | null {
  try {
    return window.sessionStorage.getItem(key)
  } catch {
    return null
  }
}

function safeSet(key: string, value: string): void {
  try {
    window.sessionStorage.setItem(key, value)
  } catch {
    // sessionStorage unavailable (e.g. private browsing) — session just won't persist
  }
}

function safeRemove(key: string): void {
  try {
    window.sessionStorage.removeItem(key)
  } catch {
    // ignore
  }
}

export const tokenStorage = {
  getToken: () => safeGet(TOKEN_KEY),
  setToken: (token: string) => safeSet(TOKEN_KEY, token),
  getRefreshToken: () => safeGet(REFRESH_TOKEN_KEY),
  setRefreshToken: (token: string) => safeSet(REFRESH_TOKEN_KEY, token),
  getUserJson: () => safeGet(USER_KEY),
  setUserJson: (json: string) => safeSet(USER_KEY, json),
  clearAll: () => {
    safeRemove(TOKEN_KEY)
    safeRemove(REFRESH_TOKEN_KEY)
    safeRemove(USER_KEY)
  },
}
