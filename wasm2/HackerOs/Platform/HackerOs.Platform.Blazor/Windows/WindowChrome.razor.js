const registrations = new WeakMap();

export function attachWindowGestures(root, dotNetReference) {
    detachWindowGestures(root);

    const onPointerDown = event => {
        if (event.button !== 0 || event.target.closest("button")) {
            return;
        }

        const target = event.target.closest("[data-window-gesture]");
        if (!target || !root.contains(target)) {
            return;
        }

        const mode = target.dataset.windowGesture;
        const edge = target.dataset.resizeEdge ?? null;
        const startX = event.clientX;
        const startY = event.clientY;
        const host = root.closest("[data-window-id]");
        const originX = Number(host?.dataset.windowX ?? 0);
        const originY = Number(host?.dataset.windowY ?? 0);
        const originWidth = Number(host?.dataset.windowWidth ?? 0);
        const originHeight = Number(host?.dataset.windowHeight ?? 0);
        let invocationQueue = Promise.resolve();
        try {
            target.setPointerCapture(event.pointerId);
        } catch (error) {
            if (error.name !== "NotFoundError") {
                throw error;
            }
        }
        event.preventDefault();

        const onPointerMove = moveEvent => {
            const deltaX = moveEvent.clientX - startX;
            const deltaY = moveEvent.clientY - startY;
            invocationQueue = invocationQueue.then(() =>
                dotNetReference.invokeMethodAsync(
                    "ReportPointerDeltaAsync",
                    mode,
                    edge,
                    deltaX,
                    deltaY,
                    originX,
                    originY,
                    originWidth,
                    originHeight));
        };

        const finish = finishEvent => {
            target.removeEventListener("pointermove", onPointerMove);
            target.removeEventListener("pointerup", finish);
            target.removeEventListener("pointercancel", finish);
            if (target.hasPointerCapture(finishEvent.pointerId)) {
                target.releasePointerCapture(finishEvent.pointerId);
            }
        };

        target.addEventListener("pointermove", onPointerMove);
        target.addEventListener("pointerup", finish);
        target.addEventListener("pointercancel", finish);
    };

    root.addEventListener("pointerdown", onPointerDown);
    registrations.set(root, onPointerDown);
}

export function detachWindowGestures(root) {
    const handler = registrations.get(root);
    if (handler) {
        root.removeEventListener("pointerdown", handler);
        registrations.delete(root);
    }
}
