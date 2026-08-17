export function renderRgba(canvas, width, height, pixels) {
    if (!canvas) return;
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d", { alpha: true });
    context.putImageData(new ImageData(new Uint8ClampedArray(pixels), width, height), 0, 0);
}

export function capturePointer(canvas, pointerId) { canvas?.setPointerCapture(pointerId); }
export function releasePointer(canvas, pointerId) { if (canvas?.hasPointerCapture(pointerId)) canvas.releasePointerCapture(pointerId); }
export function setViewport(canvas, x, y, scale) { if (canvas) canvas.style.transform = `translate(${x}px, ${y}px) scale(${scale})`; }
