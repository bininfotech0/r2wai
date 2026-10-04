import { describe, it, expect } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { ThemeModeProvider, useThemeMode } from './ThemeModeProvider'

function Probe() {
  const { isDark, toggle } = useThemeMode()
  return (
    <button onClick={toggle}>{isDark ? 'dark' : 'light'}</button>
  )
}

describe('ThemeModeProvider', () => {
  it('toggles and persists mode', () => {
    render(
      <ThemeModeProvider>
        <Probe />
      </ThemeModeProvider>,
    )
    const button = screen.getByRole('button')
    expect(button).toHaveTextContent('light')
    fireEvent.click(button)
    expect(button).toHaveTextContent('dark')
    expect(window.localStorage.getItem('r2wai_dark_mode')).toBe('true')
  })
})
