import { describe, expect, it } from 'vitest'
import { describePromptPlaceholders, findUnsupportedPlaceholders } from './promptPlaceholders'

describe('findUnsupportedPlaceholders', () => {
  it('ignores the placeholders the server fills, whatever their spacing or case', () => {
    expect(findUnsupportedPlaceholders('Hi {{user.name}} of {{ Tenant.Name }} on {{current_date}}')).toEqual([])
  })

  it('lists each unsupported placeholder once and ignores plain braces', () => {
    expect(findUnsupportedPlaceholders('{{ticket.id}} {"a":1} {{ticket.id}} {{order_no}}')).toEqual(['ticket.id', 'order_no'])
  })
})

describe('describePromptPlaceholders', () => {
  it('lists what can be used when nothing is wrong', () => {
    const result = describePromptPlaceholders('You help {{user.name}}.')
    expect(result.isWarning).toBe(false)
    expect(result.message).toContain('{{tenant.name}}')
  })

  it('warns about placeholders that would reach the model literally', () => {
    expect(describePromptPlaceholders('Ticket {{ticket.id}}')).toEqual({
      message: "{{ticket.id}} isn't supported and will be sent to the model as written.",
      isWarning: true,
    })
  })
})
