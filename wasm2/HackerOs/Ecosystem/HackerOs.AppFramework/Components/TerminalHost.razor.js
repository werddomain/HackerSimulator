/*
 * Collocated JavaScript module for the TerminalHost component.
 * Kept minimal and side-effect free per the ecosystem rule that all JS lives in
 * a scoped ".razor.js" file rather than inline in markup.
 */

/**
 * Moves keyboard focus to the terminal surface so the user can start typing
 * immediately after an application launches.
 *
 * The window that hosts the terminal can still be animating into place (see
 * the `windowAppear` CSS animation) or settling its layout right after this
 * component's first render, so a single synchronous `.focus()` call can
 * silently lose the race and leave the terminal impossible to type into
 * until the user clicks it manually. Retry across a few animation frames
 * until the element actually reports itself as the active element.
 * @param {HTMLElement} host The terminal-host container element.
 */
export function focusTerminal(host) {
    if (!host) {
        return;
    }

    // The BlazorTerminal component renders a focusable container with
    // tabindex="0"; focus it (or the host as a fallback).
    const focusable = host.querySelector('.terminal-container') ?? host;

    const maxAttempts = 10;
    let attempts = 0;

    const tryFocus = () => {
        attempts++;

        if (document.activeElement === focusable) {
            return;
        }

        focusable.focus({ preventScroll: true });

        if (document.activeElement !== focusable && attempts < maxAttempts) {
            requestAnimationFrame(tryFocus);
        }
    };

    tryFocus();
}
