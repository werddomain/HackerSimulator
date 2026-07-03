/*
 * Collocated JavaScript module for the AppLauncher component.
 * Registers a single document-level click handler so the start menu closes when
 * the user clicks anywhere outside of it. All JS lives here rather than inline in
 * markup, per the ecosystem scoped-file rule.
 */

let handler = null;

/**
 * Registers an outside-click handler that notifies .NET when a click lands
 * outside the launcher root element.
 * @param {HTMLElement} root The launcher root element.
 * @param {any} dotnetRef A DotNetObjectReference exposing CloseFromOutside().
 */
export function registerOutsideClick(root, dotnetRef) {
    unregisterOutsideClick();

    handler = (event) => {
        if (root && !root.contains(event.target)) {
            dotnetRef.invokeMethodAsync('CloseFromOutside');
        }
    };

    // Defer to the next tick so the click that opened the menu is not counted.
    setTimeout(() => document.addEventListener('click', handler, true), 0);
}

/** Removes the outside-click handler if one is registered. */
export function unregisterOutsideClick() {
    if (handler) {
        document.removeEventListener('click', handler, true);
        handler = null;
    }
}
