// Turns the model's [^n] markers into footnotes using the SSE "citations"
// event. n is CitationDto.Index; markers whose number the server never sent
// were invented by the model and are dropped. DOM-only, never innerHTML: source
// names and excerpts come from uploaded documents.

export interface Citation {
  index: number
  sourceName: string
  content: string
}

const MARK = /\[\^(\d{1,3})\]/g

export const stripCitations = (text: string) => text.replace(MARK, '')

export function renderWithCitations(bubble: HTMLElement, text: string, citations: Citation[]) {
  const byIndex = new Map(citations.map((c) => [c.index, c]))
  const cited = new Set<Citation>()
  let last = 0
  bubble.textContent = ''
  for (const m of text.matchAll(MARK)) {
    bubble.append(text.slice(last, m.index))
    last = m.index + m[0].length
    const c = byIndex.get(Number(m[1]))
    if (!c) continue
    cited.add(c)
    const sup = document.createElement('sup')
    sup.className = 'cite'
    sup.textContent = `[${c.index}]`
    sup.title = c.sourceName
    bubble.append(sup)
  }
  bubble.append(text.slice(last))

  // Small local models often answer correctly without emitting markers; still
  // say where the answer came from rather than showing nothing.
  const sources = cited.size ? [...cited] : citations
  const list = document.createElement('ol')
  list.className = 'sources'
  for (const c of sources) {
    const item = document.createElement('li')
    item.value = c.index
    item.textContent = c.sourceName
    item.title = c.content.slice(0, 280)
    list.append(item)
  }
  bubble.append(list)
}
