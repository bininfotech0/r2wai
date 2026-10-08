import { fetchPublicInfo, newSessionId, postFeedback, streamChat, uploadAttachment, type PublicInfo } from './api'

export interface WidgetConfig {
  chatbotId: string
  baseUrl: string
  title?: string
  color?: string
  position?: 'bottom-right' | 'bottom-left'
}

const STYLE = `
  :host { all: initial; }
  * { box-sizing: border-box; font-family: Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  button, textarea { font: inherit; }
  button:focus-visible, textarea:focus-visible { outline: 3px solid color-mix(in srgb, var(--r2wai-color, #6d28d9) 30%, transparent); outline-offset: 2px; }
  .launcher {
    position: fixed; bottom: 24px; width: 56px; height: 56px; border-radius: 50%;
    background: var(--r2wai-color, #6d28d9); color: #fff; border: none; cursor: pointer;
    box-shadow: 0 8px 24px color-mix(in srgb, var(--r2wai-color, #6d28d9) 35%, transparent), 0 2px 6px rgba(15,23,42,.16);
    display: grid; place-items: center; z-index: 2147483000;
    transition: transform 160ms ease, box-shadow 160ms ease;
  }
  .launcher:hover { transform: translateY(-2px); box-shadow: 0 12px 28px color-mix(in srgb, var(--r2wai-color, #6d28d9) 40%, transparent), 0 3px 8px rgba(15,23,42,.18); }
  .launcher svg { width: 25px; height: 25px; }
  .launcher.right { right: 24px; } .launcher.left { left: 24px; }
  .panel {
    position: fixed; bottom: 96px; width: min(400px, calc(100vw - 32px)); height: min(620px, calc(100dvh - 120px));
    min-height: min(360px, calc(100dvh - 120px)); background: #fff; border: 1px solid rgba(15,23,42,.08);
    border-radius: 22px; box-shadow: 0 24px 70px rgba(15,23,42,.20), 0 4px 14px rgba(15,23,42,.08);
    display: flex; flex-direction: column; overflow: hidden; z-index: 2147483000;
    animation: r2wai-panel-in 180ms cubic-bezier(.2,.8,.2,1) both;
  }
  .panel.right { right: 24px; } .panel.left { left: 24px; }
  .panel.hidden, .launcher.hidden { display: none; }
  .header {
    position: relative; background: var(--r2wai-color, #6d28d9); color: #fff; padding: 18px 18px 17px;
    display: flex; align-items: center; gap: 12px; font-size: 14px; flex: 0 0 auto;
  }
  .brandMark {
    display: grid; place-items: center; width: 38px; height: 38px; flex: 0 0 38px; border-radius: 13px;
    background: rgba(255,255,255,.17); border: 1px solid rgba(255,255,255,.24);
  }
  .brandMark svg { width: 20px; height: 20px; }
  .headerCopy { min-width: 0; flex: 1; }
  .headerTitle { display: block; font-size: 15px; font-weight: 650; line-height: 1.25; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .headerSubtitle { display: flex; align-items: center; gap: 6px; margin-top: 4px; color: rgba(255,255,255,.82); font-size: 11.5px; line-height: 1; }
  .header button {
    display: grid; place-items: center; width: 34px; height: 34px; flex: 0 0 34px; margin-left: auto;
    border: 1px solid rgba(255,255,255,.2); border-radius: 11px; color: #fff; background: rgba(255,255,255,.12); cursor: pointer;
  }
  .header button:hover { background: rgba(255,255,255,.22); }
  .header button svg { width: 17px; height: 17px; }
  .messages { flex: 1; min-height: 0; overflow-y: auto; padding: 20px 16px; display: flex; flex-direction: column; gap: 11px; scroll-behavior: smooth; }
  .bubble {
    max-width: 88%; padding: 11px 14px; border: 1px solid transparent; border-radius: 17px;
    font-size: 13.5px; line-height: 1.52; white-space: pre-wrap; overflow-wrap: anywhere;
    animation: r2wai-message-in 160ms ease both;
  }
  .bubble.user { align-self: flex-end; color: #fff; background: var(--r2wai-color, #6d28d9); border-bottom-right-radius: 6px; }
  .bubble.bot { align-self: flex-start; color: #202431; background: #f4f5f8; border-color: #eceef2; border-bottom-left-radius: 6px; }
  .bubble.error { align-self: flex-start; color: #991b1b; background: #fff4f2; border-color: #fee2e2; }
  .inputRow { flex: 0 0 auto; padding: 12px 14px 10px; border-top: 1px solid #eef0f4; background: #fff; }
  .composer {
    border: 1px solid #dfe3ea; border-radius: 17px; padding: 11px 10px 8px 14px; background: #fff;
    box-shadow: 0 2px 8px rgba(15,23,42,.035); transition: border-color 140ms ease, box-shadow 140ms ease;
  }
  .composer:focus-within { border-color: color-mix(in srgb, var(--r2wai-color, #6d28d9) 55%, #dfe3ea); box-shadow: 0 0 0 3px color-mix(in srgb, var(--r2wai-color, #6d28d9) 10%, transparent); }
  .composer textarea {
    display: block; width: 100%; min-height: 28px; max-height: 88px; resize: none; border: 0; outline: 0;
    padding: 2px 0 8px; background: transparent; color: #202431; font-size: 13.5px; line-height: 1.5;
  }
  .composer textarea::placeholder { color: #98a0ae; }
  .composer textarea:focus-visible { outline: none; }
  .composerActions { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
  .composerTools { display: flex; align-items: center; gap: 5px; }
  .inputRow button { cursor: pointer; transition: background-color 130ms ease, border-color 130ms ease, transform 130ms ease, opacity 130ms ease; }
  .inputRow button:disabled { opacity: 0.48; cursor: default; }
  .toolBtn {
    display: inline-flex; align-items: center; justify-content: center; width: 34px; height: 34px; padding: 0;
    color: #667085; background: transparent; border: 0; border-radius: 11px;
  }
  .toolBtn:hover:not(:disabled) { color: var(--r2wai-color, #6d28d9); background: color-mix(in srgb, var(--r2wai-color, #6d28d9) 8%, white); }
  .toolBtn svg { width: 18px; height: 18px; }
  .toolBtn.hidden { display: none; }
  .sendBtn {
    display: inline-flex; align-items: center; justify-content: center; gap: 7px; min-width: 38px; height: 38px; padding: 0 13px;
    color: #fff; background: var(--r2wai-color, #6d28d9); border: 0; border-radius: 12px; font-size: 12px; font-weight: 650;
    box-shadow: 0 3px 8px color-mix(in srgb, var(--r2wai-color, #6d28d9) 25%, transparent);
  }
  .sendBtn:hover:not(:disabled) { transform: translateY(-1px); filter: brightness(1.04); }
  .sendBtn svg { width: 16px; height: 16px; }
  .composerHint { padding: 8px 3px 0; color: #a0a6b2; text-align: center; font-size: 10.5px; line-height: 1.3; }
  .fileInput { display: none !important; }
  .micBtn.listening { color: #dc2626; background: #fff1f2; animation: r2wai-pulse 1.2s ease-in-out infinite; }
  .speakerBtn.enabled { color: var(--r2wai-color, #6d28d9); background: color-mix(in srgb, var(--r2wai-color, #6d28d9) 9%, white); }
  @keyframes r2wai-pulse { 0%, 100% { box-shadow: 0 0 0 0 rgba(220,38,38,.12); } 50% { box-shadow: 0 0 0 5px rgba(220,38,38,.08); } }
  @keyframes r2wai-panel-in { from { opacity: 0; transform: translateY(8px) scale(.985); } to { opacity: 1; transform: translateY(0) scale(1); } }
  @keyframes r2wai-message-in { from { opacity: 0; transform: translateY(3px); } to { opacity: 1; transform: translateY(0); } }
  .suggestions { display: flex; flex-direction: column; gap: 7px; align-self: flex-start; max-width: 94%; margin-top: 2px; }
  .suggestionLabel { margin: 1px 0 2px 3px; color: #8b93a1; font-size: 9.5px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
  .suggestion-chip {
    background: #fff; color: #424958; border: 1px solid #e3e6ec;
    border-radius: 14px; padding: 9px 13px; font-size: 12px; text-align: left; cursor: pointer;
    box-shadow: 0 1px 2px rgba(15,23,42,.025); transition: border-color 130ms ease, background-color 130ms ease, color 130ms ease, transform 130ms ease;
  }
  .suggestion-chip:hover { background: color-mix(in srgb, var(--r2wai-color, #6d28d9) 6%, white); color: var(--r2wai-color, #6d28d9); border-color: color-mix(in srgb, var(--r2wai-color, #6d28d9) 35%, #e3e6ec); transform: translateY(-1px); }
  .suggestion-chip:disabled { opacity: 0.5; cursor: default; }
  .feedbackRow { display: flex; gap: 5px; align-self: flex-start; margin-top: -5px; }
  .feedbackRow button {
    background: #fff; border: 1px solid #e8eaf0; cursor: pointer; font-size: 12px; padding: 4px 7px;
    color: #7b8493; border-radius: 9px; line-height: 1;
  }
  .feedbackRow button:hover { color: var(--r2wai-color, #6d28d9); background: #f8f7fc; border-color: #d9d5e7; }
  .feedbackRow button:disabled { cursor: default; }
  .feedbackRow button.selected { color: var(--r2wai-color, #6d28d9); border-color: color-mix(in srgb, var(--r2wai-color, #6d28d9) 30%, white); background: color-mix(in srgb, var(--r2wai-color, #6d28d9) 7%, white); }
  .feedbackRow.hidden { display: none; }
  .feedbackThanks { font-size: 11px; color: #8b93a1; align-self: flex-start; margin-top: -5px; }
  @media (max-width: 480px) {
    .launcher.right { right: 16px; } .launcher.left { left: 16px; }
    .panel { bottom: 88px; width: calc(100vw - 24px); height: calc(100dvh - 104px); max-height: 680px; min-height: min(340px, calc(100dvh - 104px)); border-radius: 20px; }
    .panel.right { right: 12px; } .panel.left { left: 12px; }
    .messages { padding: 16px 13px; }
    .inputRow { padding: 10px 11px 8px; }
  }
  @media (prefers-reduced-motion: reduce) {
    *, *::before, *::after { scroll-behavior: auto !important; animation-duration: .01ms !important; transition-duration: .01ms !important; }
  }
`

// Minimal ambient typing for the (non-standard, vendor-prefixed-in-some-browsers) Web Speech
// API — not in TypeScript's default DOM lib. Kept narrow to exactly what this widget uses.
interface MinimalSpeechRecognition extends EventTarget {
  lang: string
  continuous: boolean
  interimResults: boolean
  start(): void
  stop(): void
  onresult: ((event: { results: ArrayLike<ArrayLike<{ transcript: string }>> }) => void) | null
  onerror: ((event: { error: string }) => void) | null
  onend: (() => void) | null
}
type SpeechRecognitionCtor = new () => MinimalSpeechRecognition

function getSpeechRecognitionCtor(): SpeechRecognitionCtor | null {
  const w = window as unknown as { SpeechRecognition?: SpeechRecognitionCtor; webkitSpeechRecognition?: SpeechRecognitionCtor }
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null
}

function escapeAttempt(text: string): string {
  // Messages are rendered via textContent below, not innerHTML — this is
  // just for the small number of places a string briefly touches template
  // literals (e.g. the header title), kept for defense in depth.
  return text.replace(/[<>&]/g, (c) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;' })[c] ?? c)
}

function createIcon(paths: string[]): SVGSVGElement {
  const namespace = 'http://www.w3.org/2000/svg'
  const icon = document.createElementNS(namespace, 'svg')
  icon.setAttribute('viewBox', '0 0 24 24')
  icon.setAttribute('fill', 'none')
  icon.setAttribute('stroke', 'currentColor')
  icon.setAttribute('stroke-width', '1.8')
  icon.setAttribute('stroke-linecap', 'round')
  icon.setAttribute('stroke-linejoin', 'round')
  icon.setAttribute('aria-hidden', 'true')
  for (const d of paths) {
    const path = document.createElementNS(namespace, 'path')
    path.setAttribute('d', d)
    icon.appendChild(path)
  }
  return icon
}

function createWidget(config: WidgetConfig) {
  const position = config.position === 'bottom-left' ? 'left' : 'right'
  const host = document.createElement('div')
  host.id = 'r2wai-widget-host'
  document.body.appendChild(host)
  const shadow = host.attachShadow({ mode: 'open' })

  const style = document.createElement('style')
  style.textContent = STYLE
  shadow.appendChild(style)

  if (config.color) {
    host.style.setProperty('--r2wai-color', config.color)
  }

  const launcher = document.createElement('button')
  launcher.className = `launcher ${position}`
  launcher.setAttribute('aria-label', 'Open chat')
  launcher.setAttribute('aria-expanded', 'false')
  launcher.appendChild(createIcon(['M21 11.5a8.4 8.4 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.4 8.4 0 0 1-3.8-.9L3 21l1.9-5.7a8.4 8.4 0 0 1-.9-3.8A8.5 8.5 0 0 1 8.7 3.9a8.4 8.4 0 0 1 3.8-.9h.5a8.5 8.5 0 0 1 8 8z']))
  shadow.appendChild(launcher)

  const panel = document.createElement('div')
  panel.className = `panel ${position} hidden`
  panel.setAttribute('role', 'dialog')
  panel.setAttribute('aria-label', `${config.title ?? 'Chat'} conversation`)
  shadow.appendChild(panel)

  const header = document.createElement('div')
  header.className = 'header'
  const brandMark = document.createElement('span')
  brandMark.className = 'brandMark'
  brandMark.appendChild(createIcon(['M12 3v3', 'M12 18v3', 'M3 12h3', 'M18 12h3', 'm5.64 5.64 2.12 2.12', 'm16.24 16.24 2.12 2.12', 'm5.64 18.36 2.12-2.12', 'm16.24 7.76 2.12-2.12']))
  const headerCopy = document.createElement('span')
  headerCopy.className = 'headerCopy'
  const headerTitle = document.createElement('span')
  headerTitle.className = 'headerTitle'
  headerTitle.textContent = escapeAttempt(config.title ?? 'Chat')
  const headerSubtitle = document.createElement('span')
  headerSubtitle.className = 'headerSubtitle'
  headerSubtitle.textContent = 'AI assistant'
  headerCopy.appendChild(headerTitle)
  headerCopy.appendChild(headerSubtitle)
  const newChatBtn = document.createElement('button')
  newChatBtn.type = 'button'
  newChatBtn.appendChild(createIcon(['M3 12a9 9 0 1 0 3-6.7', 'M3 4v5h5']))
  newChatBtn.setAttribute('aria-label', 'Start a new chat')
  newChatBtn.title = 'New chat'
  const closeBtn = document.createElement('button')
  closeBtn.type = 'button'
  closeBtn.appendChild(createIcon(['M18 6 6 18', 'm6 6 12 12']))
  closeBtn.setAttribute('aria-label', 'Close chat')
  header.appendChild(brandMark)
  header.appendChild(headerCopy)
  header.appendChild(newChatBtn)
  header.appendChild(closeBtn)
  panel.appendChild(header)

  const messages = document.createElement('div')
  messages.className = 'messages'
  messages.setAttribute('aria-live', 'polite')
  panel.appendChild(messages)

  const inputRow = document.createElement('div')
  inputRow.className = 'inputRow'
  const composer = document.createElement('div')
  composer.className = 'composer'
  const textarea = document.createElement('textarea')
  textarea.rows = 1
  textarea.setAttribute('aria-label', 'Message')
  textarea.placeholder = 'Type a message…'
  function resizeTextarea() {
    textarea.style.height = 'auto'
    textarea.style.height = `${Math.min(textarea.scrollHeight, 88)}px`
    textarea.style.overflowY = textarea.scrollHeight > 88 ? 'auto' : 'hidden'
  }
  // Matches ChatbotsController.AllowedAttachmentContentTypes exactly — a mismatch here would just
  // mean the server rejects a file the picker let through, not a security issue, but keeping them
  // in sync avoids a confusing round trip for the visitor.
  const fileInput = document.createElement('input')
  fileInput.type = 'file'
  fileInput.accept = 'image/jpeg,image/png,image/gif,image/webp,application/pdf,text/plain'
  fileInput.className = 'fileInput'
  const attachBtn = document.createElement('button')
  attachBtn.type = 'button'
  attachBtn.className = 'toolBtn attachBtn'
  attachBtn.setAttribute('aria-label', 'Attach a file')
  attachBtn.appendChild(createIcon(['m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l9.2-9.19a4 4 0 0 1 5.65 5.65l-9.2 9.2a2 2 0 0 1-2.82-2.83l8.49-8.48']))
  const micBtn = document.createElement('button')
  micBtn.type = 'button'
  micBtn.className = 'toolBtn micBtn hidden'
  micBtn.appendChild(createIcon(['M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z', 'M19 10v2a7 7 0 0 1-14 0v-2', 'M12 19v3', 'M8 22h8']))
  micBtn.setAttribute('aria-label', 'Speak your message')
  const speakerBtn = document.createElement('button')
  speakerBtn.type = 'button'
  speakerBtn.className = 'toolBtn speakerBtn hidden'
  speakerBtn.appendChild(createIcon(['M11 5 6 9H2v6h4l5 4z', 'M15.54 8.46a5 5 0 0 1 0 7.07', 'M19.07 4.93a10 10 0 0 1 0 14.14']))
  speakerBtn.setAttribute('aria-label', 'Read replies aloud')
  speakerBtn.setAttribute('aria-pressed', 'false')
  const sendBtn = document.createElement('button')
  sendBtn.type = 'button'
  sendBtn.className = 'sendBtn'
  sendBtn.setAttribute('aria-label', 'Send')
  sendBtn.append('Send')
  sendBtn.appendChild(createIcon(['M22 2 11 13', 'm22 2-7 20-4-9-9-4z']))
  const composerActions = document.createElement('div')
  composerActions.className = 'composerActions'
  const composerTools = document.createElement('div')
  composerTools.className = 'composerTools'
  composerTools.appendChild(fileInput)
  composerTools.appendChild(attachBtn)
  composerTools.appendChild(micBtn)
  composerTools.appendChild(speakerBtn)
  composerActions.appendChild(composerTools)
  composerActions.appendChild(sendBtn)
  composer.appendChild(textarea)
  composer.appendChild(composerActions)
  inputRow.appendChild(composer)
  const composerHint = document.createElement('div')
  composerHint.className = 'composerHint'
  composerHint.textContent = 'Enter to send · Shift + Enter for a new line'
  inputRow.appendChild(composerHint)
  panel.appendChild(inputRow)

  function addBubble(role: 'user' | 'bot' | 'error', text: string): HTMLDivElement {
    const bubble = document.createElement('div')
    bubble.className = `bubble ${role}`
    bubble.textContent = text
    messages.appendChild(bubble)
    messages.scrollTop = messages.scrollHeight
    return bubble
  }

  let infoLoaded = false
  let info: PublicInfo | null = null
  let sending = false
  let sessionId = newSessionId()

  // Speech-to-text input (browser SpeechRecognition API) and text-to-speech output (browser
  // speechSynthesis) — both no server component, no API keys. Recognized text lands in the
  // textarea for the user to review/edit before sending; it is never auto-sent. TTS is an opt-in
  // toggle, off by default — only speaks a reply once fully streamed, never mid-stream.
  let recognition: MinimalSpeechRecognition | null = null
  let listening = false
  let ttsEnabled = false

  function setupVoiceOutput() {
    if (!('speechSynthesis' in window)) return
    speakerBtn.classList.remove('hidden')
  }

  function speak(text: string) {
    if (!ttsEnabled || !text || !('speechSynthesis' in window)) return
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = document.documentElement.lang || 'en-US'
    window.speechSynthesis.speak(utterance)
  }

  speakerBtn.addEventListener('click', () => {
    ttsEnabled = !ttsEnabled
    speakerBtn.classList.toggle('enabled', ttsEnabled)
    speakerBtn.setAttribute('aria-pressed', String(ttsEnabled))
    if (!ttsEnabled) window.speechSynthesis?.cancel()
  })

  function stopListening() {
    listening = false
    micBtn.classList.remove('listening')
    recognition?.stop()
  }

  function setupVoiceInput() {
    const Ctor = getSpeechRecognitionCtor()
    if (!Ctor) return
    recognition = new Ctor()
    recognition.lang = document.documentElement.lang || 'en-US'
    recognition.continuous = false
    recognition.interimResults = false
    recognition.onresult = (event) => {
      const transcript = event.results[event.results.length - 1]?.[0]?.transcript ?? ''
      if (transcript) {
        textarea.value = textarea.value ? `${textarea.value} ${transcript}` : transcript
        resizeTextarea()
        textarea.focus()
      }
    }
    recognition.onerror = () => stopListening()
    recognition.onend = () => stopListening()
    micBtn.classList.remove('hidden')
  }

  micBtn.addEventListener('click', () => {
    if (!recognition) return
    if (listening) {
      stopListening()
      return
    }
    listening = true
    micBtn.classList.add('listening')
    recognition.start()
  })

  // "Attach then reference", not a rich attachment UI: the uploaded file's URL is inserted as
  // plain text into the textarea for the visitor to send along with their message. Bubbles are
  // rendered via textContent, not innerHTML (XSS defense in depth, see escapeAttempt above) — a
  // clickable link would need HTML rendering, a materially bigger change than this endpoint's own
  // scope, so the URL stays inspectable/copyable plain text instead.
  attachBtn.addEventListener('click', () => fileInput.click())
  fileInput.addEventListener('change', () => {
    void (async () => {
      const file = fileInput.files?.[0]
      fileInput.value = ''
      if (!file) return

      attachBtn.disabled = true
      const result = await uploadAttachment(config.baseUrl, config.chatbotId, file)
      attachBtn.disabled = false

      if (!result) {
        addBubble('error', `Could not attach "${file.name}". It may be too large or an unsupported type.`)
        return
      }

      const reference = `[Attached: ${result.fileName}] ${result.url}`
      textarea.value = textarea.value ? `${textarea.value}\n${reference}` : reference
      resizeTextarea()
      textarea.focus()
    })()
  })

  // Configurable in the admin UI (Chatbot detail page's "Suggested questions" field) since it
  // shipped, but never once exposed to the widget until now — clicking a chip sends it immediately
  // rather than just filling the textarea, since these are pre-authored complete questions, not
  // uncertain input that needs review (unlike the voice-recognized text above).
  function renderSuggestions(questions: string[]) {
    const container = document.createElement('div')
    container.className = 'suggestions'
    const label = document.createElement('div')
    label.className = 'suggestionLabel'
    label.textContent = 'Suggested questions'
    container.appendChild(label)
    for (const question of questions) {
      const chip = document.createElement('button')
      chip.type = 'button'
      chip.className = 'suggestion-chip'
      chip.textContent = question
      chip.addEventListener('click', () => {
        if (sending) return
        container.remove()
        textarea.value = question
        void handleSend()
      })
      container.appendChild(chip)
    }
    messages.appendChild(container)
    messages.scrollTop = messages.scrollHeight
  }

  function renderGreeting() {
    if (!info) return
    if (info.welcomeMessage) addBubble('bot', info.welcomeMessage)
    if (info.suggestedQuestions?.length) renderSuggestions(info.suggestedQuestions)
  }

  // Clears the transcript and starts a new server-side session, so the bot forgets this one.
  function startNewChat() {
    if (sending) return
    if (listening) stopListening()
    if ('speechSynthesis' in window) window.speechSynthesis.cancel()
    sessionId = newSessionId()
    messages.replaceChildren()
    textarea.value = ''
    resizeTextarea()
    renderGreeting()
    textarea.focus()
  }

  async function ensureInfoLoaded() {
    if (infoLoaded) return
    infoLoaded = true
    try {
      info = await fetchPublicInfo(config.baseUrl, config.chatbotId)
      headerTitle.textContent = escapeAttempt(config.title ?? info.name)
      panel.setAttribute('aria-label', `${config.title ?? info.name} conversation`)
      if (info.voiceEnabled) {
        setupVoiceInput()
        setupVoiceOutput()
      }
      renderGreeting()
    } catch {
      addBubble('error', 'Could not load this chatbot.')
    }
  }

  // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #55. Not shown on an error bubble (nothing useful
  // to rate) and only ever asked once per reply — clicking either button disables both immediately
  // rather than allowing a rating to be changed, since the backend counter is a one-way increment,
  // not a settable value.
  function addFeedbackRow() {
    const row = document.createElement('div')
    row.className = 'feedbackRow'
    const up = document.createElement('button')
    up.type = 'button'
    up.textContent = '👍'
    up.setAttribute('aria-label', 'Good response')
    const down = document.createElement('button')
    down.type = 'button'
    down.textContent = '👎'
    down.setAttribute('aria-label', 'Poor response')
    row.appendChild(up)
    row.appendChild(down)
    messages.appendChild(row)
    messages.scrollTop = messages.scrollHeight

    function rate(rating: 'up' | 'down', button: HTMLButtonElement) {
      up.disabled = true
      down.disabled = true
      button.classList.add('selected')
      void postFeedback(config.baseUrl, config.chatbotId, rating).then((ok) => {
        const thanks = document.createElement('div')
        thanks.className = 'feedbackThanks'
        thanks.textContent = ok ? 'Thanks for your feedback.' : 'Could not record your feedback.'
        row.replaceWith(thanks)
      })
    }

    up.addEventListener('click', () => rate('up', up))
    down.addEventListener('click', () => rate('down', down))
  }

  async function handleSend() {
    const text = textarea.value.trim()
    if (!text || sending) return
    if (listening) stopListening()
    sending = true
    sendBtn.disabled = true
    textarea.value = ''
    resizeTextarea()
    addBubble('user', text)
    const botBubble = addBubble('bot', '')

    await streamChat(config.baseUrl, config.chatbotId, text, sessionId, {
      onChunk: (chunk) => {
        botBubble.textContent += chunk
        messages.scrollTop = messages.scrollHeight
      },
      onDone: () => {
        sending = false
        sendBtn.disabled = false
        speak(botBubble.textContent ?? '')
        addFeedbackRow()
      },
      onError: (message) => {
        botBubble.classList.add('error')
        botBubble.textContent = botBubble.textContent || message
        sending = false
        sendBtn.disabled = false
      },
    })
  }

  launcher.addEventListener('click', () => {
    panel.classList.remove('hidden')
    launcher.classList.add('hidden')
    launcher.setAttribute('aria-expanded', 'true')
    void ensureInfoLoaded()
  })
  newChatBtn.addEventListener('click', startNewChat)
  closeBtn.addEventListener('click', () => {
    if (listening) stopListening()
    panel.classList.add('hidden')
    launcher.classList.remove('hidden')
    launcher.setAttribute('aria-expanded', 'false')
    launcher.focus()
  })
  panel.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') closeBtn.click()
  })
  sendBtn.addEventListener('click', () => void handleSend())
  textarea.addEventListener('input', resizeTextarea)
  textarea.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void handleSend()
    }
  })

  return { host, open: () => launcher.click() }
}

function init(config: WidgetConfig) {
  if (!config.chatbotId || !config.baseUrl) {
    console.error('[R2WAIWidget] init requires chatbotId and baseUrl')
    return
  }
  createWidget(config)
}

declare global {
  interface Window {
    R2WAIWidget?: { init: typeof init }
  }
}

window.R2WAIWidget = { init }

// Auto-init from the <script> tag's data-* attributes, mirroring common
// embeddable-widget conventions (Intercom/Crisp-style) — no extra init call
// needed for the common case.
const currentScript = document.currentScript as HTMLScriptElement | null
if (currentScript?.dataset.chatbotId) {
  init({
    chatbotId: currentScript.dataset.chatbotId,
    baseUrl: currentScript.dataset.baseUrl ?? window.location.origin,
    title: currentScript.dataset.title,
    color: currentScript.dataset.color,
    position: currentScript.dataset.position === 'bottom-left' ? 'bottom-left' : 'bottom-right',
  })
}
