import { CHATBOT_CHANNEL_TYPES, type ChatbotChannelType, type ChatbotDto } from '../chatbots/types'

/**
 * Settings that the widget applies from the embed snippet's `data-*` attributes.
 *
 * The shipped widget bundle (src/R2WAI.Widget/src/widget.ts) reads title/colour/position
 * straight off the script tag's attributes, not from an API call — so the snippet itself
 * stays the source of truth the browser actually uses, and a rename here would ship a tag
 * the bundle silently ignores. What used to be missing was persistence *of the admin's
 * choice*: `Chatbot.WidgetSettings`/`EmbedScript` are real columns with a real domain
 * setter (`Chatbot.UpdateWidget`) but had no DTO/command wiring, so this form reset to
 * defaults on every visit and on every other device. `PUT /chatbots/{id}/widget`
 * (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #50/#51) now saves both the encoded config
 * and the generated snippet, so the page can re-seed from the last saved choice instead of
 * always starting blank.
 */
export interface WidgetAppearanceConfig {
  title: string
  color: string
  position: 'bottom-right' | 'bottom-left'
}

/** The widget bundle's own fallback, used when the customer strips the attribute. */
export const WIDGET_DEFAULT_COLOR = '#6d28d9'

export const WIDGET_COLOR_PRESETS: { label: string; value: string }[] = [
  { label: 'Indigo', value: '#6d28d9' },
  { label: 'Blue', value: '#2563eb' },
  { label: 'Teal', value: '#0d9488' },
  { label: 'Green', value: '#16a34a' },
  { label: 'Amber', value: '#d97706' },
  { label: 'Red', value: '#dc2626' },
  { label: 'Slate', value: '#334155' },
]

/** Accepts #rgb / #rrggbb only — the value is injected into CSS as a custom property. */
export function isValidWidgetColor(value: string): boolean {
  return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(value.trim())
}

/** Parses a persisted `Chatbot.WidgetSettings` value back into form state. Malformed or
 * absent JSON (never saved yet, or a hand-edited row) falls back to the bundle's own
 * defaults rather than failing the page load. */
export function parseWidgetAppearance(json: string | null | undefined): WidgetAppearanceConfig {
  const fallback: WidgetAppearanceConfig = { title: '', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' }
  if (!json) return fallback
  try {
    const parsed = JSON.parse(json) as Partial<WidgetAppearanceConfig>
    return {
      title: typeof parsed.title === 'string' ? parsed.title : fallback.title,
      color: typeof parsed.color === 'string' && isValidWidgetColor(parsed.color) ? parsed.color : fallback.color,
      position: parsed.position === 'bottom-left' ? 'bottom-left' : 'bottom-right',
    }
  } catch {
    return fallback
  }
}

export function encodeWidgetAppearance(appearance: WidgetAppearanceConfig): string {
  return JSON.stringify(appearance)
}

/**
 * Builds the exact snippet the widget bundle auto-initialises from.
 *
 * Uses the deployment's own origin rather than a hardcoded domain, so the snippet is
 * correct on localhost, on-prem, and in every environment without a rebuild. The
 * attribute names must stay in lockstep with the auto-init block at the bottom of
 * src/R2WAI.Widget/src/widget.ts — `data-chatbot-id` is required (no init call happens
 * without it) and `data-position` is the only value the bundle treats as non-default.
 */
export function buildWidgetScript(options: {
  origin: string
  chatbotId: string
  appearance: WidgetAppearanceConfig
}): string {
  const { origin, chatbotId, appearance } = options
  const attributes = [
    `src="${origin}/widget/widget.js"`,
    `data-chatbot-id="${chatbotId}"`,
    `data-base-url="${origin}"`,
    // Only emit appearance attributes that differ from the bundle's defaults, so the
    // snippet stays as short as possible for the common case.
    appearance.title.trim() ? `data-title="${escapeAttribute(appearance.title)}"` : null,
    isValidWidgetColor(appearance.color) && appearance.color.toLowerCase() !== WIDGET_DEFAULT_COLOR
      ? `data-color="${appearance.color}"`
      : null,
    appearance.position === 'bottom-left' ? `data-position="bottom-left"` : null,
  ].filter((a): a is string => a !== null)

  return `<script\n  ${attributes.join('\n  ')}\n  async>\n</script>`
}

function escapeAttribute(value: string): string {
  return value.replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/**
 * Telegram is absent from `ChatbotChannelType` (Teams | Slack | WhatsApp | Sms) and has
 * no provider adapter, so `POST /chatbots/{id}/channels/telegram` would be rejected by
 * enum validation. It is listed here so the channel overview can show it as explicitly
 * unavailable rather than silently omitting it, which reads as an oversight.
 */
export interface UnavailableChannel {
  channel: string
  reason: string
  specRef: string
}

export const UNAVAILABLE_CHANNELS: UnavailableChannel[] = [
  {
    channel: 'Telegram',
    reason:
      'Not supported by this deployment. Telegram is not in the platform channel type and has no provider adapter or inbound webhook.',
    specRef: 'docs/api/MISSING-BACKEND-ENDPOINTS.md §2.3',
  },
]

/**
 * Per-channel backend capability, as the API actually behaves today.
 *
 * `POST /chatbots/{id}/channels/{channel}` validates the enum and then stores the
 * submitted payload encrypted (ChatbotChannelConfiguration). There is no outbound
 * sender and no inbound provider webhook, so a stored configuration is NOT proof of a
 * working connection and the UI must never present it as one.
 */
export interface ChannelCapability {
  /** Configuration can be saved through the real endpoint. */
  canConfigure: boolean
  /** A live provider handshake exists to verify the credentials. */
  canVerify: boolean
  /** Outbound messages actually reach the provider. */
  canDeliver: boolean
  /** Why this channel can or cannot be called "connected", shown verbatim in the UI. */
  note: string
}

export const CHANNEL_CAPABILITIES: Record<ChatbotChannelType, ChannelCapability> = {
  WhatsApp: {
    canConfigure: true,
    canVerify: false,
    canDeliver: false,
    note: 'Credentials are stored encrypted. No WhatsApp Cloud API adapter is deployed, so the platform cannot yet confirm the business account, send a test message, or receive inbound webhooks.',
  },
  Teams: {
    canConfigure: true,
    canVerify: false,
    canDeliver: false,
    note: 'Configuration is stored encrypted. No Microsoft Graph adapter is deployed, so delivery is not yet active.',
  },
  Slack: {
    canConfigure: true,
    canVerify: false,
    canDeliver: false,
    note: 'Configuration is stored encrypted. No Slack adapter is deployed, so delivery is not yet active.',
  },
  Sms: {
    canConfigure: true,
    canVerify: false,
    canDeliver: false,
    note: 'Configuration is stored encrypted. No SMS provider adapter is deployed, so delivery is not yet active.',
  },
}

/**
 * How a channel row should read on the overview. Kept as a pure function so the widget,
 * WhatsApp and overview surfaces cannot disagree about what "configured" means.
 *
 * The `canVerify && canDeliver` branch is the important one: it is what this function
 * will return the day a provider adapter ships. Adding the adapter and flipping those two
 * flags is all it takes for the overview to start reporting "Connected" — no UI change,
 * and no possibility of the flag and the wording drifting apart.
 */
export function describeChannelState(
  channel: ChatbotChannelType,
  chatbot: Pick<ChatbotDto, 'status'>,
  isConfigured: boolean,
): { label: string; tone: 'success' | 'warning' | 'default' | 'error' } {
  if (chatbot.status !== 'Active') return { label: 'Chatbot not active', tone: 'default' }
  if (!isConfigured) return { label: 'Not configured', tone: 'default' }
  const capability = CHANNEL_CAPABILITIES[channel]
  if (capability.canVerify && capability.canDeliver) return { label: 'Connected', tone: 'success' }
  // Configured but unverifiable: deliberately 'warning', never 'success'. Reporting a
  // messaging channel as connected when nothing can deliver a message would be a lie.
  return { label: 'Configured, unverified', tone: 'warning' }
}

export const ALL_CHANNEL_TYPES: ChatbotChannelType[] = CHATBOT_CHANNEL_TYPES
