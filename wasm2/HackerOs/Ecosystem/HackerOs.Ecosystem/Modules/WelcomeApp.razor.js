/*
 * Collocated JavaScript module for the Welcome application.
 * Demonstrates the scoped ".razor.js" convention: no inline scripts in markup.
 */

/**
 * Returns a short human readable description of the current browser runtime.
 * @returns {string}
 */
export function describeEnvironment() {
    const platform = navigator.platform || 'unknown platform';
    const cores = navigator.hardwareConcurrency || '?';
    return `Blazor WebAssembly \u00B7 ${platform} \u00B7 ${cores} logical cores`;
}
