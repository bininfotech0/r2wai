import { QueryClient } from '@tanstack/react-query'
import { ApiRequestError } from './fetchJson'

/** Shared cache and retry policy for server state across the client. */
export function createAppQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: (failureCount, error) => {
          if (error instanceof ApiRequestError && error.status < 500) return false
          return failureCount < 1
        },
      },
      mutations: {
        retry: false,
      },
    },
  })
}
