/**
 * HackerOS DesktopArea Resize Observer Interop
 */
export function observeDesktopSize(element, dotNetHelper) {
    if (!element || !window.ResizeObserver) {
        return;
    }

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const width = Math.round(entry.contentRect.width);
            const height = Math.round(entry.contentRect.height);
            if (width > 0 && height > 0) {
                dotNetHelper.invokeMethodAsync('OnDesktopResized', width, height);
            }
        }
    });

    observer.observe(element);
}
