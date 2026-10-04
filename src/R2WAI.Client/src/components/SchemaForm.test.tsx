import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { SchemaForm, type JsonSchema } from './SchemaForm'

const schema: JsonSchema = {
  type: 'object',
  required: ['employeeId'],
  properties: {
    employeeId: { type: 'string', title: 'Employee ID' },
    retryCount: { type: 'number', title: 'Retry Count' },
    notify: { type: 'boolean', title: 'Notify on failure' },
    risk: { type: 'string', title: 'Risk', enum: ['Low', 'Medium', 'High'] },
  },
}

describe('SchemaForm', () => {
  it('renders a field per schema property with the right control type', () => {
    render(<SchemaForm schema={schema} value={{}} onChange={vi.fn()} />)
    expect(screen.getByLabelText(/Employee ID/)).toBeInTheDocument()
    expect(screen.getByLabelText(/Retry Count/)).toHaveAttribute('type', 'number')
    expect(screen.getByText('Notify on failure')).toBeInTheDocument()
    expect(screen.getByLabelText('Risk')).toBeInTheDocument()
  })

  it('calls onChange with the merged value when a text field changes', () => {
    const onChange = vi.fn()
    render(<SchemaForm schema={schema} value={{ retryCount: 3 }} onChange={onChange} />)
    fireEvent.change(screen.getByLabelText(/Employee ID/), { target: { value: 'E123' } })
    expect(onChange).toHaveBeenCalledWith({ retryCount: 3, employeeId: 'E123' })
  })

  it('coerces number fields to a number', () => {
    const onChange = vi.fn()
    render(<SchemaForm schema={schema} value={{}} onChange={onChange} />)
    fireEvent.change(screen.getByLabelText(/Retry Count/), { target: { value: '5' } })
    expect(onChange).toHaveBeenCalledWith({ retryCount: 5 })
  })

  it('toggles boolean fields', () => {
    const onChange = vi.fn()
    render(<SchemaForm schema={schema} value={{}} onChange={onChange} />)
    fireEvent.click(screen.getByRole('switch'))
    expect(onChange).toHaveBeenCalledWith({ notify: true })
  })
})
