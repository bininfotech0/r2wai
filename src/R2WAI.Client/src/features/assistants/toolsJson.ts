/**
 * Enabled tools are stored on AssistantDefinition.Tools as a JSON array of capability ids. Shared by
 * the Tools tab and the Overview's Tools card so both count the same thing.
 */
export function parseEnabledIds(toolsJson: string | null): string[] {
  if (!toolsJson) return []
  try {
    const parsed = JSON.parse(toolsJson) as unknown
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === 'string') : []
  } catch {
    return []
  }
}
