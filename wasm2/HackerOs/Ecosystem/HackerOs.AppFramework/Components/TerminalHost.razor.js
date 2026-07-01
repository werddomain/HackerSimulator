/*
 * Collocated JavaScript module for the TerminalHost component.
 * Kept minimal and side-effect free per the ecosystem rule that all JS lives in
 * a scoped ".razor.js" file rather than inline in markup.
 */

/**
 * Moves keyboard focus to the terminal surface so the user can start typing
 * immediately after an application launches.
 * @param {HTMLElement} host The terminal-host container element.
 */
export function focusTerminal(host) {
    if (!host) {
        return;
    }

    // The BlazorTerminal component renders a focusable container with
    // tabindex="0"; focus it (or the host as a fallback).
    const focusable = host.querySelector('.terminal-container') ?? host;
    focusable.focus({ preventScroll: true });
}
