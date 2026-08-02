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
        let previousX = event.clientX;
        let previousY = event.clientY;
        try {
            target.setPointerCapture(event.pointerId);
        } catch (error) {
            if (error.name !== "NotFoundError") {
                throw error;
            }
        }
        event.preventDefault();

        const onPointerMove = moveEvent => {
            const deltaX = moveEvent.clientX - previousX;
            const deltaY = moveEvent.clientY - previousY;
            previousX = moveEvent.clientX;
            previousY = moveEvent.clientY;
            void dotNetReference.invokeMethodAsync("ReportPointerDeltaAsync", mode, edge, deltaX, deltaY);
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