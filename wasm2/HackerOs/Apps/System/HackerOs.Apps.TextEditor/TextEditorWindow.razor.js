export function selectRange(textarea, start, end) {
    if (!textarea) {
        return;
    }

    textarea.focus({ preventScroll: true });
    textarea.setSelectionRange(start, end);

    const totalLength = textarea.value.length || 1;
    const scrollableHeight = textarea.scrollHeight - textarea.clientHeight;
    if (scrollableHeight > 0) {
        const ratio = start / totalLength;
        textarea.scrollTop = Math.max(0, (ratio * textarea.scrollHeight) - (textarea.clientHeight / 2));
    }
}
