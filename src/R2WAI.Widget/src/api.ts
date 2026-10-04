// Talks directly to ChatbotsController's [AllowAnonymous] routes — no embed
// token exists server-side (see ChatbotEmbedDialog's warning to admins), the
// chatbot's id in the URL is the only "credential". Mirrors the working
// reference implementation at src/R2WAI.Web/Components/Pages/ChatbotWidget.razor.

export interface PublicInfo {
  name: string
  welcomeMessage: string | null
  voiceEnabled: boolean
  suggestedQuestions: string[] | null
}

export async function fetchPublicInfo(baseUrl: string, chatbotId: string): Promise<PublicInfo> {
  const response = await fetch(`${baseUrl}/api/v1/chatbots/${chatbotId}/public-info`)
  if (!response.ok) throw new Error(`Failed to load chatbot (HTTP ${response.status})`)
  return (await response.json()) as PublicInfo
}

export interface StreamHandlers {
  onChunk: (content: string) => void
  onDone: () => void
  onError: (message: string) => void
}

/**
 * Parses the backend's SSE frames (event:/data: line pairs, blank-line
 * delimited) from POST .../chat/stream. No SSE library — plain fetch +
 * ReadableStream keeps this package dependency-light per the migration plan.
 */
export async function streamChat(baseUrl: string, chatbotId: string, message: string, handlers: StreamHandlers): Promise<void> {
  const response = await fetch(`${baseUrl}/api/v1/chatbots/${chatbotId}/chat/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message }),
  })
  if (!response.ok || !response.body) {
    handlers.onError(`Request failed (HTTP ${response.status})`)
    return
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let currentEvent = ''

  try {
    for (;;) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })

      const lines = buffer.split('\n')
      buffer = lines.pop() ?? ''

      for (const line of lines) {
        const trimmed = line.trimEnd()
        if (trimmed.startsWith('event:')) {
          currentEvent = trimmed.slice(6).trim()
        } else if (trimmed.startsWith('data:')) {
          const data = trimmed.slice(5).trim()
          handleEvent(currentEvent, data, handlers)
        }
      }
    }
  } catch {
    handlers.onError('Connection lost while streaming.')
  }
}

export interface AttachmentUploadResult {
  url: string
  fileName: string
  contentType: string
  sizeBytes: number
}

export async function uploadAttachment(baseUrl: string, chatbotId: string, file: File): Promise<AttachmentUploadResult | null> {
  try {
    const form = new FormData()
    form.append('file', file)
    const response = await fetch(`${baseUrl}/api/v1/chatbots/${chatbotId}/messages/attachment`, {
      method: 'POST',
      body: form,
    })
    if (!response.ok) return null
    return (await response.json()) as AttachmentUploadResult
  } catch {
    return null
  }
}

export async function postFeedback(baseUrl: string, chatbotId: string, rating: 'up' | 'down'): Promise<boolean> {
  try {
    const response = await fetch(`${baseUrl}/api/v1/chatbots/${chatbotId}/feedback`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ rating }),
    })
    return response.ok
  } catch {
    return false
  }
}

function handleEvent(event: string, data: string, handlers: StreamHandlers) {
  if (event === 'chunk') {
    try {
      const parsed = JSON.parse(data) as { content?: string }
      if (parsed.content) handlers.onChunk(parsed.content)
    } catch {
      // Malformed frame — skip it rather than crash the stream.
    }
  } else if (event === 'done') {
    handlers.onDone()
  } else if (event === 'error') {
    try {
      const parsed = JSON.parse(data) as { message?: string }
      handlers.onError(parsed.message ?? 'The assistant hit an error.')
    } catch {
      handlers.onError('The assistant hit an error.')
    }
  }
  // "citations" events are ignored — the widget shows plain reply text only.
}
