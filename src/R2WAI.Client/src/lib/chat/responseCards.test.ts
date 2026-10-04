import { describe, it, expect } from 'vitest'
import { parseContentBlocks } from './types'

describe('parseContentBlocks', () => {
  it('returns an empty array for null/undefined/empty', () => {
    expect(parseContentBlocks(null)).toEqual([])
    expect(parseContentBlocks(undefined)).toEqual([])
    expect(parseContentBlocks('')).toEqual([])
  })

  it('never throws on malformed JSON — returns empty instead', () => {
    expect(parseContentBlocks('not json')).toEqual([])
    expect(parseContentBlocks('{"broken')).toEqual([])
  })

  it('returns an empty array when the JSON is valid but not an array', () => {
    expect(parseContentBlocks('{"type":"status"}')).toEqual([])
  })

  it('parses a real status card', () => {
    const json = JSON.stringify([
      { type: 'status', title: 'Invoice Approval', fields: [{ label: 'Status', value: 'Running' }] },
    ])
    expect(parseContentBlocks(json)).toEqual([
      { type: 'status', title: 'Invoice Approval', fields: [{ label: 'Status', value: 'Running' }] },
    ])
  })

  it('parses a real table card', () => {
    const json = JSON.stringify([
      { type: 'table', title: 'Pending Approvals', columns: ['Workflow', 'Requester'], rows: [['Leave Request', 'Jane Doe']] },
    ])
    expect(parseContentBlocks(json)).toEqual([
      { type: 'table', title: 'Pending Approvals', columns: ['Workflow', 'Requester'], rows: [['Leave Request', 'Jane Doe']] },
    ])
  })
})
