/**
 * Projects C#-authoritative geometry through CSS custom properties without
 * placing a dynamic style attribute in Razor source.
 */
export function projectWindowGeometry(element, x, y, width, height, zOrder) {
    element.style.setProperty("--window-x", `${x}px`);
    element.style.setProperty("--window-y", `${y}px`);
    element.style.setProperty("--window-width", `${width}px`);
    element.style.setProperty("--window-height", `${height}px`);
    element.style.setProperty("--window-z", `${zOrder}`);
}
