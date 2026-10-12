import { useSyncExternalStore } from 'react'

/**
 * The workspace picked in the top-bar switcher. authFetch sends it as X-Workspace-Id on every API
 * call, and the server narrows list queries to it (an id the server does not recognise for this
 * tenant falls back to the default workspace). Kept per browser in localStorage, so it is a
 * convenience, never a permission: the server decides what the id is allowed to show.
 */
const STORAGE_KEY = 'r2wai.workspace'
const listeners = new Set<() => void>()

function read(): string | null {
  try {
    return window.localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

let current: string | null = typeof window === 'undefined' ? null : read()

export function getActiveWorkspaceId(): string | null {
  return current
}

export function setActiveWorkspaceId(id: string | null) {
  if (id === current) return
  current = id
  try {
    if (id) window.localStorage.setItem(STORAGE_KEY, id)
    else window.localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Storage unavailable (private mode): the choice still holds for this tab.
  }
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function useActiveWorkspaceId(): string | null {
  return useSyncExternalStore(subscribe, getActiveWorkspaceId, () => null)
}
