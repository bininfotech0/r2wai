let _dotNetRef = null;
let _keyHandler = null;

export function register(dotNetRef) {
    _dotNetRef = dotNetRef;
    if (_keyHandler) {
        document.removeEventListener('keydown', _keyHandler);
    }
    _keyHandler = function (e) {
        const tag = (e.target.tagName || '').toLowerCase();
        const isInput = tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable;

        // Ctrl+K — Command Palette (always)
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('ToggleCommandPalette');
            return;
        }

        // Ctrl+/ — Toggle Copilot (always)
        if ((e.ctrlKey || e.metaKey) && e.key === '/') {
            e.preventDefault();
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('ToggleCopilotShortcut');
            return;
        }

        // Escape — Close panels/dialogs
        if (e.key === 'Escape') {
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('HandleEscape');
            return;
        }

        // Shortcuts below only fire when not focused in an input
        if (isInput) return;

        // Ctrl+N — New (context-aware)
        if ((e.ctrlKey || e.metaKey) && e.key === 'n') {
            e.preventDefault();
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('HandleNewShortcut');
            return;
        }

        // G then H — Go Home (vim-style navigation)
        // G then A — Go Assistants
        // G then W — Go Workflows
        // G then D — Go Documents
        // G then I — Go Inbox
        // G then O — Go Operations
        if (e.key === 'g' && !e.ctrlKey && !e.metaKey && !e.altKey) {
            _waitingForG = true;
            setTimeout(() => { _waitingForG = false; }, 800);
            return;
        }
        if (_waitingForG) {
            _waitingForG = false;
            const nav = {
                'h': '/', 'a': '/assistant-studio', 'w': '/workflow-studio',
                'd': '/documents', 'i': '/inbox', 'o': '/operations',
                's': '/admin/users'
            }[e.key];
            if (nav && _dotNetRef) {
                e.preventDefault();
                _dotNetRef.invokeMethodAsync('NavigateTo', nav);
            }
            return;
        }

        // ? — Show keyboard shortcuts help
        if (e.key === '?' && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            if (_dotNetRef) _dotNetRef.invokeMethodAsync('ShowShortcutsHelp');
            return;
        }
    };
    document.addEventListener('keydown', _keyHandler);
}

let _waitingForG = false;

export function dispose() {
    if (_keyHandler) {
        document.removeEventListener('keydown', _keyHandler);
        _keyHandler = null;
    }
    _dotNetRef = null;
}
