// Drag & drop DataTransfer read/write for FileView (FV-006). Blazor's DragEventArgs/DataTransfer expose
// only read-only metadata (Types, EffectAllowed, ...) — there is no getData/setData marshaled across the
// interop boundary, so the actual payload transfer has to happen here. Both functions rely on `window.event`
// still referring to the DOM event currently being dispatched: in Blazor WebAssembly, C# event handlers run
// synchronously on the same call stack as the native event up until their first `await` of real async work,
// and a same-process JS interop call (like this one) does not yield that stack — so as long as the C# side
// calls straight into these before awaiting anything else, `window.event` is still the live drag event.
// (HTML5 requires dataTransfer.setData to be called synchronously within dragstart for the same reason.)

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
    const event = window.event;
    if (!event || !event.dataTransfer) {
        return null;
    }

    const data = event.dataTransfer.getData(mimeType);
    return data ? data : null;
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
