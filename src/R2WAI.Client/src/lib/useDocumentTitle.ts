import { useEffect } from 'react'

/** Sets document.title for basic browser-tab/bookmark usability. Restores the prior title on unmount. */
export function useDocumentTitle(title: string) {
  useEffect(() => {
    const previous = document.title
    document.title = title
    return () => {
      document.title = previous
    }
  }, [title])
}
