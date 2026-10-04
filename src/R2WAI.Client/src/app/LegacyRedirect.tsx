import { Navigate, useLocation, useParams } from 'react-router-dom'
import type { Params } from 'react-router-dom'

export function LegacyRedirect({ to }: { to: (params: Readonly<Params<string>>) => string }) {
  const params = useParams()
  const location = useLocation()
  return <Navigate to={{ pathname: to(params), search: location.search, hash: location.hash }} replace />
}
