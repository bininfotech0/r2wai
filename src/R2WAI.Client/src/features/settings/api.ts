import { authFetch } from '../../lib/auth/authClient'
import { ApiRequestError, fetchJson } from '../../lib/api/fetchJson'
import { DEFAULT_FEATURE_FLAGS, DEFAULT_LIMITS, type SettingsDto, type TenantFeatureFlags, type TenantLimits, type UpdateSettingsInput } from './types'

export function getSettings() {
  return fetchJson<SettingsDto>('/admin/settings')
}

async function updateSettingsRaw(input: UpdateSettingsInput): Promise<SettingsDto> {
  const response = await authFetch('/api/v1/admin/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!response.ok) throw new ApiRequestError(response.status, 'Failed to save settings')
  return (await response.json()) as SettingsDto
}

export function updateOrganizationDetails(name: string, slug: string, domain: string | undefined) {
  return updateSettingsRaw({ tenantName: name, tenantSlug: slug, tenantDomain: domain })
}

export function parseLimits(tenantSettingsJson: string | null): TenantLimits {
  if (!tenantSettingsJson) return DEFAULT_LIMITS
  try {
    const parsed = JSON.parse(tenantSettingsJson) as Record<string, unknown>
    const limits = parsed.limits as Partial<TenantLimits> | undefined
    return { ...DEFAULT_LIMITS, ...limits }
  } catch {
    return DEFAULT_LIMITS
  }
}

// Merge-safe: reads the current TenantSettings blob, replaces only the
// "limits" key, and writes the whole object back — other keys already
// stored there (e.g. "contentModeration", written by the real, working
// POST /admin/content-moderation endpoint) are preserved untouched.
export async function updateLimits(currentTenantSettingsJson: string | null, limits: TenantLimits): Promise<SettingsDto> {
  let merged: Record<string, unknown> = {}
  if (currentTenantSettingsJson) {
    try {
      merged = JSON.parse(currentTenantSettingsJson) as Record<string, unknown>
    } catch {
      merged = {}
    }
  }
  merged.limits = limits
  return updateSettingsRaw({ tenantSettings: JSON.stringify(merged) })
}

export function parseFeatureFlags(featuresJson: string | null): TenantFeatureFlags {
  if (!featuresJson) return DEFAULT_FEATURE_FLAGS
  try {
    const parsed = JSON.parse(featuresJson) as Record<string, unknown>
    // Tolerate the legacy PascalCase shape the (broken) Blazor page would
    // have written, in addition to the camelCase this app writes.
    return {
      assistants: (parsed.assistants ?? parsed.Assistants ?? DEFAULT_FEATURE_FLAGS.assistants) as boolean,
      workflows: (parsed.workflows ?? parsed.Workflows ?? DEFAULT_FEATURE_FLAGS.workflows) as boolean,
      approvals: (parsed.approvals ?? parsed.Approvals ?? DEFAULT_FEATURE_FLAGS.approvals) as boolean,
      knowledge: (parsed.knowledge ?? parsed.Knowledge ?? DEFAULT_FEATURE_FLAGS.knowledge) as boolean,
      integrations: (parsed.integrations ?? parsed.Integrations ?? DEFAULT_FEATURE_FLAGS.integrations) as boolean,
      analytics: (parsed.analytics ?? parsed.Analytics ?? DEFAULT_FEATURE_FLAGS.analytics) as boolean,
    }
  } catch {
    return DEFAULT_FEATURE_FLAGS
  }
}

export function updateFeatureFlags(flags: TenantFeatureFlags) {
  return updateSettingsRaw({ features: JSON.stringify(flags) })
}
