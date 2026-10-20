// Drag & drop DataTransfer read/write for FileView (FV-006, ADR 0058 section 1). Blazor's
// DragEventArgs/DataTransfer expose only read-only metadata (Types, EffectAllowed, ...): there is no
// getData/setData marshaled across the interop boundary, and the File objects of a drop from the host
// system are not reachable from C#. So the actual payload transfer happens here.
//
// setDragData relies on `window.event` still being the dragstart being dispatched (HTML5 requires
// setData synchronously within dragstart; Blazor WebAssembly runs the C# handler on the same call
// stack up to its first real await).
//
// Drops do not depend on that timing: attachDropZone installs native listeners on a FileView root,
// breadcrumb or drop zone. Its capture-phase `drop` listener runs before Blazor's delegated handler and
// stashes the HackerOS payload and the host File objects; the C# handler then reads the stash
// (getDragData / takeDroppedFiles). Host files pasted with Ctrl+V go through the same stash
// (attachPasteTarget). Its `dragover` listener sets dropEffect so the cursor shows a move,
// or a copy when Ctrl is held or the files come from the host, and highlights the folder under it.

const hostFileStore = new Map();
let nextToken = 1;
let lastDrop = null;

// An app dragged from the Start menu (ADR 0160 §6): dropped on a folder, it becomes a .desktop shortcut.
const appMimeType = 'application/vnd.hackeros.app+json';
// A desktop icon moved on its own desktop (ADR 0160 §3): the desktop places it, nothing is transferred.
const desktopItemMimeType = 'application/vnd.hackeros.desktop-item+json';

export function setDragData(mimeType, json) {
    const event = window.event;
    if (!event || !event.dataTransfer) {
        return false;
    }

    event.dataTransfer.effectAllowed = 'copyMove';
    event.dataTransfer.setData(mimeType, json);
    return true;
}

// Several payloads in one call, while dragstart is still being dispatched: a desktop drag carries the
// files (for other windows) and the icon keys (for its own desktop).
export function setDragDataMany(data) {
    const event = window.event;
    if (!event || !event.dataTransfer) {
        return false;
    }

    event.dataTransfer.effectAllowed = 'copyMove';
    for (const [mimeType, json] of Object.entries(data)) {
        event.dataTransfer.setData(mimeType, json);
    }

    return true;
}

export function getDragData(mimeType) {
    if (lastDrop && lastDrop.data && performance.now() - lastDrop.at < 5000 && mimeType in lastDrop.data) {
        return lastDrop.data[mimeType];
    }

    const event = window.event;
    if (!event || !event.dataTransfer) {
        return null;
    }

    const data = event.dataTransfer.getData(mimeType);
    return data ? data : null;
}

// Returns { token, files: [{ name, size, type }] } for the host files of the last drop, keeping the
// File objects under `token` until releaseDroppedFiles(token).
export function takeDroppedFiles() {
    const files = lastDrop && performance.now() - lastDrop.at < 5000 ? lastDrop.files : [];
    lastDrop = null;
    if (files.length === 0) {
        return { token: 0, files: [] };
    }

    const token = nextToken++;
    hostFileStore.set(token, files);
    return { token, files: files.map(f => ({ name: f.name, size: f.size, type: f.type || '' })) };
}

// The File (a Blob) comes back to .NET as an IJSStreamReference.
export function openDroppedFile(token, index) {
    const files = hostFileStore.get(token);
    if (!files || index < 0 || index >= files.length) {
        throw new Error('The dropped file is no longer available.');
    }

    return files[index];
}

export function releaseDroppedFiles(token) {
    hostFileStore.delete(token);
}

export function attachDropZone(element, mimeType) {
    if (!element || element.__hackerOsDropZone) {
        return false;
    }

    let highlighted = null;
    const clearHighlight = () => {
        if (highlighted) {
            highlighted.classList.remove('file-view-drop-hover');
            highlighted = null;
        }
    };

    const onDragOver = e => {
        const transfer = e.dataTransfer;
        if (!transfer) {
            return;
        }

        const types = Array.from(transfer.types || []);
        if (types.includes(desktopItemMimeType)) {
            transfer.dropEffect = 'move';
        } else if (types.includes(mimeType)) {
            transfer.dropEffect = e.ctrlKey ? 'copy' : 'move';
        } else if (types.includes('Files') || types.includes(appMimeType)) {
            transfer.dropEffect = 'copy';
        } else {
            return;
        }

        const target = e.target instanceof Element ? e.target.closest('[data-drop-target]') : null;
        if (target !== highlighted) {
            clearHighlight();
            if (target && element.contains(target)) {
                target.classList.add('file-view-drop-hover');
                highlighted = target;
            }
        }
    };

    const onDrop = e => {
        clearHighlight();
        const transfer = e.dataTransfer;
        if (!transfer) {
            lastDrop = null;
            return;
        }

        const data = {};
        for (const type of [mimeType, appMimeType, desktopItemMimeType]) {
            const value = transfer.getData(type);
            data[type] = value ? value : null;
        }

        lastDrop = {
            at: performance.now(),
            data,
            files: Array.from(transfer.files || [])
        };
    };

    element.addEventListener('dragover', onDragOver);
    element.addEventListener('dragleave', e => {
        if (!element.contains(e.relatedTarget)) {
            clearHighlight();
        }
    });
    element.addEventListener('drop', onDrop, true);
    element.__hackerOsDropZone = true;
    return true;
}

// Ctrl+V on the view (ADR 0065). The browser's `paste` event, not the keydown, drives the paste: it
// fires after the host clipboard bridge (hostClipboard.js of HackerOs.Infrastructure.Browser, listening
// on the document in the capture phase) updated the OS clipboard, and it carries the host files of a
// paste from the real machine. The bridge marks the event with `hackerOsHostFiles` (whether those files
// are new host content allowed by clipboard.config); without the bridge, any pasted file counts.
// Some browsers (Safari) fire no paste on a non-editable element: a Ctrl+V keydown not followed by a
// paste within 250 ms still pastes the OS clipboard.
function isEditable(target) {
    return target instanceof Element
        && (target.isContentEditable || target.closest('input, textarea, select, [contenteditable="true"]') !== null);
}

export function attachPasteTarget(element, callbacks) {
    if (!element || element.__hackerOsPasteTarget) {
        return false;
    }

    let pending = null;
    const cancelPending = () => {
        if (pending !== null) {
            clearTimeout(pending);
            pending = null;
        }
    };

    element.addEventListener('keydown', e => {
        if (!(e.ctrlKey || e.metaKey) || e.altKey || e.key.toLowerCase() !== 'v' || isEditable(e.target)) {
            return;
        }

        cancelPending();
        pending = setTimeout(() => {
            pending = null;
            callbacks.invokeMethodAsync('OnPasteShortcutAsync', false);
        }, 250);
    });

    element.addEventListener('paste', e => {
        if (isEditable(e.target)) {
            return;
        }

        cancelPending();
        e.preventDefault();
        const files = Array.from((e.clipboardData && e.clipboardData.files) || []);
        const hostFiles = files.length > 0 && e.hackerOsHostFiles !== false;
        if (hostFiles) {
            lastDrop = { at: performance.now(), data: {}, files };
        }

        callbacks.invokeMethodAsync('OnPasteShortcutAsync', hostFiles);
    });

    element.__hackerOsPasteTarget = true;
    return true;
}

// Reveal (ADR 0096, section 1): scrolls the row/tile/node for `path` to the middle of its scroller,
// then focuses it without scrolling again, so "Show in folder" lands on a visible, selected item.
export function revealItem(containerEl, path) {
    for (const el of containerEl.querySelectorAll('[data-item-path]')) {
        if (el.getAttribute('data-item-path') === path) {
            el.scrollIntoView({ block: 'center', inline: 'nearest' });
            el.focus({ preventScroll: true });
            return true;
        }
    }

    return false;
}

// Roving-tabindex keyboard navigation (FV-011): moves real DOM focus to the row/tile/node for `path`
// after an arrow key changes the active item. Every renderer tags its row's root element with
// data-item-path, so this one lookup works across Details/Icons/Tree without knowing which mode is active.
export function focusItem(containerEl, path) {
    for (const el of containerEl.querySelectorAll('[data-item-path]')) {
        if (el.getAttribute('data-item-path') === path) {
            el.focus();
            return true;
        }
    }

    return false;
}
