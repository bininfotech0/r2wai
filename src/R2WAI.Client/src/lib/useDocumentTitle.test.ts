import { describe, it, expect } from 'vitest'
import { render } from '@testing-library/react'
import { createElement } from 'react'
import { useDocumentTitle } from './useDocumentTitle'

function Probe({ title }: { title: string }) {
  useDocumentTitle(title)
  return null
}

describe('useDocumentTitle', () => {
  it('sets document.title and updates it when the title prop changes', () => {
    const { rerender, unmount } = render(createElement(Probe, { title: 'Automations · R2WAI Studio' }))
    expect(document.title).toBe('Automations · R2WAI Studio')

    rerender(createElement(Probe, { title: 'Runs · R2WAI Studio' }))
    expect(document.title).toBe('Runs · R2WAI Studio')

    unmount()
  })

  it('restores the previous title on unmount', () => {
    document.title = 'R2WAI Studio'
    const { unmount } = render(createElement(Probe, { title: 'Settings · R2WAI Studio' }))
    expect(document.title).toBe('Settings · R2WAI Studio')

    unmount()
    expect(document.title).toBe('R2WAI Studio')
  })
})
