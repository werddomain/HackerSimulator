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
// (getDragData / takeDroppedFiles). Its `dragover` listener sets dropEffect so the cursor shows a move,
// or a copy when Ctrl is held or the files come from the host, and highlights the folder under it.

const hostFileStore = new Map();
let nextToken = 1;
let lastDrop = null;

export function setDragData(mimeType, json) {
    const event = window.event;
    if (!event || !event.dataTransfer) {
        return false;
    }

    event.dataTransfer.effectAllowed = 'copyMove';
    event.dataTransfer.setData(mimeType, json);
    return true;
}

export function getDragData(mimeType) {
    if (lastDrop && lastDrop.mimeType === mimeType && performance.now() - lastDrop.at < 5000) {
        return lastDrop.json;
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
        if (types.includes(mimeType)) {
            transfer.dropEffect = e.ctrlKey ? 'copy' : 'move';
        } else if (types.includes('Files')) {
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

        const json = transfer.getData(mimeType);
        lastDrop = {
            mimeType,
            at: performance.now(),
            json: json ? json : null,
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
