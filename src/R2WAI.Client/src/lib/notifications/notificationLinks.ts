const INTERNAL_NOTIFICATION_ORIGIN = 'https://r2wai.invalid'

/** Accept only root-relative notification destinations and reject URL-normalized external hosts. */
export function getSafeNotificationPath(link: string | null | undefined): string | null {
  if (!link?.startsWith('/')) return null
  try {
    const target = new URL(link, INTERNAL_NOTIFICATION_ORIGIN)
    if (target.origin !== INTERNAL_NOTIFICATION_ORIGIN) return null
    return `${target.pathname}${target.search}${target.hash}`
  } catch {
    return null
  }
}
