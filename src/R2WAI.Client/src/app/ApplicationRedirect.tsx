import { Navigate, useParams } from 'react-router-dom'

export function ApplicationRedirect() {
  const { id } = useParams<{ id: string }>()
  return <Navigate to={`/connections/${id}`} replace />
}
