import { describe, it, expect } from 'vitest'
import { render } from '@testing-library/react'
import { createElement } from 'react'
import { LegacyNotice } from './LegacyNotice'

describe('LegacyNotice', () => {
  it('says the screen is legacy, names the feature and points at the replacement', () => {
    const { getByRole, unmount } = render(createElement(LegacyNotice, { feature: 'Automations' }))
    const text = getByRole('alert').textContent ?? ''
    expect(text).toContain('Legacy')
    expect(text).toContain('Automations')
    expect(text).toContain('AI Assistants')
    unmount()
  })
})
