const registrations = new WeakMap();

// Mirrors HackerOs.Windowing.Blazor/WindowChrome.razor.js's pointer-capture drag pattern (ADR
// 0015: small collocated JS, no domain logic here -- C# decides what a swipe means).
export function attachSwipeDownGesture(handleElement, dotNetReference) {
    detachSwipeDownGesture(handleElement);

    const openThreshold = 32;

    const onPointerDown = event => {
        if (event.button !== 0) {
            return;
        }

        const startY = event.clientY;
        let triggered = false;
        try {
            handleElement.setPointerCapture(event.pointerId);
        } catch (error) {
            if (error.name !== "NotFoundError") {
                throw error;
            }
        }
        // Without this, a real trusted mousedown/move can be diverted into native drag/text-selection
        // handling instead of delivering pointermove -- matches WindowChrome.razor.js's working drag.
        event.preventDefault();

        const onPointerMove = moveEvent => {
            if (triggered) {
                return;
            }

            if (moveEvent.clientY - startY >= openThreshold) {
                triggered = true;
                dotNetReference.invokeMethodAsync("OnSwipeDownDetected");
            }
        };

        const finish = finishEvent => {
            handleElement.removeEventListener("pointermove", onPointerMove);
            handleElement.removeEventListener("pointerup", finish);
            handleElement.removeEventListener("pointercancel", finish);
            if (handleElement.hasPointerCapture(finishEvent.pointerId)) {
                handleElement.releasePointerCapture(finishEvent.pointerId);
            }
        };

        handleElement.addEventListener("pointermove", onPointerMove);
        handleElement.addEventListener("pointerup", finish);
        handleElement.addEventListener("pointercancel", finish);
    };

    handleElement.addEventListener("pointerdown", onPointerDown);
    registrations.set(handleElement, onPointerDown);
    // Lets tests (and any other caller) know the gesture listener is actually live, since attaching
    // happens after OnAfterRenderAsync -- the handle can be visible in the DOM slightly before this.
    handleElement.dataset.swipeGestureAttached = "true";
}

export function detachSwipeDownGesture(handleElement) {
    const handler = registrations.get(handleElement);
    if (handler) {
        handleElement.removeEventListener("pointerdown", handler);
        registrations.delete(handleElement);
    }
}
