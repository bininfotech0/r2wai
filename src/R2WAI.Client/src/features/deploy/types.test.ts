import { describe, expect, it } from 'vitest'
import {
  buildWidgetScript,
  encodeWidgetAppearance,
  isValidWidgetColor,
  parseWidgetAppearance,
  WIDGET_DEFAULT_COLOR,
  type WidgetAppearanceConfig,
} from './types'

const base: WidgetAppearanceConfig = { title: '', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' }

function build(overrides: Partial<WidgetAppearanceConfig> = {}, chatbotId = 'abc-123', origin = 'https://app.r2wai.com') {
  return buildWidgetScript({ origin, chatbotId, appearance: { ...base, ...overrides } })
}

describe('buildWidgetScript', () => {
  it('always emits the required chatbot id, base url and script path', () => {
    const script = build()
    expect(script).toContain(`src="https://app.r2wai.com/widget/widget.js"`)
    expect(script).toContain('data-chatbot-id="abc-123"')
    expect(script).toContain('data-base-url="https://app.r2wai.com"')
    expect(script).toContain('async')
  })

  it('uses the deployment origin rather than a hardcoded domain', () => {
    expect(build({}, 'x', 'http://localhost:5173')).toContain('src="http://localhost:5173/widget/widget.js"')
    expect(build({}, 'x', 'https://on-prem.internal')).toContain('data-base-url="https://on-prem.internal"')
  })

  // The bundle reads exactly these dataset keys and no others, so a rename here would
  // ship a snippet that silently does not boot.
  it('matches the data-* attribute names the shipped widget bundle reads', () => {
    const script = build({ title: 'Support', color: '#2563eb', position: 'bottom-left' })
    expect(script).toContain('data-title="Support"')
    expect(script).toContain('data-color="#2563eb"')
    expect(script).toContain('data-position="bottom-left"')
  })

  it('omits appearance attributes that equal the bundle defaults', () => {
    const script = build()
    expect(script).not.toContain('data-title')
    expect(script).not.toContain('data-color')
    expect(script).not.toContain('data-position')
  })

  it('emits position only for the non-default side', () => {
    expect(build({ position: 'bottom-right' })).not.toContain('data-position')
    expect(build({ position: 'bottom-left' })).toContain('data-position="bottom-left"')
  })

  it('escapes quotes and angle brackets in the title', () => {
    const script = build({ title: 'Acme "Support" <bot>' })
    expect(script).toContain('data-title="Acme &quot;Support&quot; &lt;bot&gt;"')
    // The injected value must not be able to close the attribute and add its own one.
    expect(script).not.toContain('data-title="Acme "Support"')
  })

  it('treats a blank title as "use the bundle default" rather than emitting an empty attr', () => {
    expect(build({ title: '   ' })).not.toContain('data-title')
  })

  it('drops an invalid colour instead of emitting broken CSS', () => {
    const script = build({ color: 'javascript:alert(1)' })
    expect(script).not.toContain('data-color')
  })

  it('is case-insensitive about the default colour', () => {
    expect(build({ color: WIDGET_DEFAULT_COLOR.toUpperCase() })).not.toContain('data-color')
  })
})

describe('isValidWidgetColor', () => {
  it('accepts the formats the widget CSS custom property can use', () => {
    expect(isValidWidgetColor('#fff')).toBe(true)
    expect(isValidWidgetColor('#6d28d9')).toBe(true)
    expect(isValidWidgetColor('#6D28D9')).toBe(true)
    expect(isValidWidgetColor('  #6d28d9  ')).toBe(true)
  })

  it('rejects anything that is not a plain hex colour', () => {
    // The value is injected straight into `style.setProperty('--r2wai-color', value)`,
    // so named colours, functions and var() references are all out of scope.
    expect(isValidWidgetColor('red')).toBe(false)
    expect(isValidWidgetColor('rgb(1,2,3)')).toBe(false)
    expect(isValidWidgetColor('var(--x)')).toBe(false)
    expect(isValidWidgetColor('#12345')).toBe(false)
    expect(isValidWidgetColor('#gggggg')).toBe(false)
    expect(isValidWidgetColor('url(javascript:alert(1))')).toBe(false)
  })
})

describe('parseWidgetAppearance / encodeWidgetAppearance', () => {
  it('round-trips a saved appearance', () => {
    const config: WidgetAppearanceConfig = { title: 'Support', color: '#2563eb', position: 'bottom-left' }
    expect(parseWidgetAppearance(encodeWidgetAppearance(config))).toEqual(config)
  })

  it('falls back to bundle defaults when nothing was ever saved', () => {
    expect(parseWidgetAppearance(null)).toEqual({ title: '', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' })
    expect(parseWidgetAppearance(undefined)).toEqual({ title: '', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' })
    expect(parseWidgetAppearance('')).toEqual({ title: '', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' })
  })

  it('falls back to bundle defaults on malformed JSON rather than throwing', () => {
    expect(parseWidgetAppearance('not json')).toEqual({ title: '', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' })
  })

  it('discards an invalid colour or position instead of trusting a hand-edited row', () => {
    const parsed = parseWidgetAppearance('{"title":"Hi","color":"javascript:alert(1)","position":"top-left"}')
    expect(parsed).toEqual({ title: 'Hi', color: WIDGET_DEFAULT_COLOR, position: 'bottom-right' })
  })
})
