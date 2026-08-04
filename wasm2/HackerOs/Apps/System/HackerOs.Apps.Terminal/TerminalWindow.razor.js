export function observeTerminal(element, dotNetReference) {
    const measure = () => {
        const style = getComputedStyle(element);
        const probe = document.createElement("canvas").getContext("2d");
        probe.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
        const cellWidth = Math.max(1, probe.measureText("M").width);
        const fontSize = Number.parseFloat(style.fontSize) || 14;
        const lineHeight = Number.parseFloat(style.lineHeight) || fontSize * 1.45;
        const columns = Math.max(1, Math.floor(element.clientWidth / cellWidth));
        const rows = Math.max(3, Math.floor(element.clientHeight / lineHeight));
        dotNetReference.invokeMethodAsync("UpdateTerminalViewport", columns, rows);
    };

    const observer = new ResizeObserver(measure);
    observer.observe(element);
    measure();

    return {
        dispose() {
            observer.disconnect();
        }
    };
}
