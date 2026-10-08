// Mirrors R2WAI.Application.Common.AI.PromptPlaceholders — the placeholders the server fills in a
// system prompt before the model sees it. Anything else in {{ }} is sent to the model as written.
export const SUPPORTED_PROMPT_PLACEHOLDERS = [
  'tenant.name',
  'workspace.name',
  'user.name',
  'user.role',
  'current_date',
] as const

const PLACEHOLDER_PATTERN = /\{\{\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}/g

/** Placeholders in `text` the server will not fill, de-duplicated, in order of appearance. */
export function findUnsupportedPlaceholders(text: string): string[] {
  const supported = new Set<string>(SUPPORTED_PROMPT_PLACEHOLDERS)
  const unsupported = new Set<string>()
  for (const match of text.matchAll(PLACEHOLDER_PATTERN)) {
    const name = match[1].toLowerCase()
    if (!supported.has(name)) unsupported.add(match[1])
  }
  return [...unsupported]
}

/** Helper text for a prompt editor: what can be used, plus a warning for anything that can't. */
export function describePromptPlaceholders(text: string): { message: string; isWarning: boolean } {
  const unsupported = findUnsupportedPlaceholders(text)
  if (unsupported.length > 0) {
    const list = unsupported.map((name) => `{{${name}}}`).join(', ')
    return {
      message: `${list} ${unsupported.length === 1 ? "isn't" : "aren't"} supported and will be sent to the model as written.`,
      isWarning: true,
    }
  }
  return {
    message: `You can use ${SUPPORTED_PROMPT_PLACEHOLDERS.map((name) => `{{${name}}}`).join(', ')}.`,
    isWarning: false,
  }
}
