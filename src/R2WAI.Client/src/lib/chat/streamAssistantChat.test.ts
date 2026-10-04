import { describe, it, expect, vi, beforeEach } from 'vitest'

const authFetchMock = vi.fn()
vi.mock('../auth/authClient', () => ({
  authFetch: (...args: unknown[]) => authFetchMock(...args),
}))

const { streamAssistantChat, chatWithAssistant } = await import('./streamAssistantChat')

function sseResponse(body: string, ok = true, status = 200): Response {
  const encoder = new TextEncoder()
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(encoder.encode(body))
      controller.close()
    },
  })
  return new Response(stream, { status, statusText: ok ? 'OK' : 'Error' })
}

beforeEach(() => {
  authFetchMock.mockReset()
})

describe('streamAssistantChat', () => {
  it('parses chunk, citations, and done events in order', async () => {
    const body =
      'event: chunk\ndata: {"content":"Hel"}\n\n' +
      'event: chunk\ndata: {"content":"lo"}\n\n' +
      'event: citations\ndata: {"citations":[{"sourceName":"Doc","content":"c","score":0.9,"index":1}]}\n\n' +
      'event: done\ndata: {"message":"Stream complete"}\n\n'
    authFetchMock.mockResolvedValue(sseResponse(body))

    const chunks: string[] = []
    let citations: unknown = null
    let done = false

    await streamAssistantChat('a1', 'hi', undefined, {
      onChunk: (c) => chunks.push(c),
      onCitations: (c) => {
        citations = c
      },
      onDone: () => {
        done = true
      },
      onError: () => {
        throw new Error('should not error')
      },
    })

    expect(chunks.join('')).toBe('Hello')
    expect(citations).toEqual([{ sourceName: 'Doc', content: 'c', score: 0.9, index: 1 }])
    expect(done).toBe(true)
  })

  it('stops at the error event and reports it', async () => {
    const body = 'event: error\ndata: {"message":"Assistant not found"}\n\n'
    authFetchMock.mockResolvedValue(sseResponse(body))

    let errorMessage: string | null = null
    await streamAssistantChat('missing', 'hi', undefined, {
      onChunk: () => {
        throw new Error('should not chunk')
      },
      onCitations: () => {},
      onDone: () => {
        throw new Error('should not complete')
      },
      onError: (m) => {
        errorMessage = m
      },
    })

    expect(errorMessage).toBe('Assistant not found')
  })

  it('skips malformed frames instead of throwing', async () => {
    const body = 'event: chunk\ndata: not-json\n\n' + 'event: chunk\ndata: {"content":"ok"}\n\n' + 'data: [DONE]\n\n'
    authFetchMock.mockResolvedValue(sseResponse(body))

    const chunks: string[] = []
    await streamAssistantChat('a1', 'hi', undefined, {
      onChunk: (c) => chunks.push(c),
      onCitations: () => {},
      onDone: () => {},
      onError: () => {
        throw new Error('should not error on malformed frame')
      },
    })

    expect(chunks).toEqual(['ok'])
  })

  it('parses toolCallStarted/toolCallCompleted events into onToolCall', async () => {
    const body =
      'event: toolCallStarted\ndata: {"toolName":"Get Leave Balance"}\n\n' +
      'event: toolCallCompleted\ndata: {"toolName":"Get Leave Balance","success":true}\n\n' +
      'event: done\ndata: {"message":"Stream complete"}\n\n'
    authFetchMock.mockResolvedValue(sseResponse(body))

    const events: unknown[] = []
    await streamAssistantChat('a1', 'hi', undefined, {
      onChunk: () => {},
      onCitations: () => {},
      onDone: () => {},
      onError: () => {
        throw new Error('should not error')
      },
      onToolCall: (e) => events.push(e),
    })

    expect(events).toEqual([
      { toolName: 'Get Leave Balance', status: 'started' },
      { toolName: 'Get Leave Balance', status: 'completed', success: true },
    ])
  })

  it('reports a request failure via onError', async () => {
    authFetchMock.mockResolvedValue(new Response(null, { status: 500 }))
    let errorMessage: string | null = null
    await streamAssistantChat('a1', 'hi', undefined, {
      onChunk: () => {},
      onCitations: () => {},
      onDone: () => {},
      onError: (m) => {
        errorMessage = m
      },
    })
    expect(errorMessage).toBe('Request failed (500)')
  })
})

describe('chatWithAssistant', () => {
  it('returns the parsed non-streaming reply', async () => {
    authFetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({ reply: 'hi there', conversationId: 'c1', tokensUsed: 10, citations: [] }),
        { status: 200 },
      ),
    )
    const result = await chatWithAssistant('a1', 'hello')
    expect(result.reply).toBe('hi there')
    expect(result.conversationId).toBe('c1')
  })

  it('throws on a failed request', async () => {
    authFetchMock.mockResolvedValue(new Response(null, { status: 404 }))
    await expect(chatWithAssistant('a1', 'hello')).rejects.toThrow('Request failed (404)')
  })
})
