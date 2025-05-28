// ClipboardInterop.js
// This file contains JavaScript functions for clipboard operations and other terminal-related functionality

/**
 * Copies text to the clipboard
 * @param {string} text - The text to copy
 * @returns {Promise<boolean>} - Whether the copy was successful
 */
export async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (error) {
        console.error("Error copying to clipboard", error);
        return false;
    }
}

/**
 * Reads text from the clipboard
 * @returns {Promise<string>} - The text from the clipboard
 */
export async function readFromClipboard() {
    try {
        return await navigator.clipboard.readText();
    } catch (error) {
        console.error("Error reading from clipboard", error);
        return "";
    }
}

/**
 * Initializes terminal-related event listeners for a container element
 * @param {string} containerId - The ID of the terminal container element
 */
export function initializeTerminalEvents(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;
    
    // Ensure focus is maintained when clicking in the terminal
    container.addEventListener('click', () => {
        container.focus();
    });
    
    // Prevent default browser behaviors that might interfere with terminal
    container.addEventListener('contextmenu', (event) => {
        // Allow default context menu for now, but could add custom terminal menu later
    });
}

/**
 * Scrolls a terminal element to a specific position
 * @param {string} containerId - The ID of the terminal container element
 * @param {number} position - The scroll position (0-1, where 1 is bottom)
 */
export function scrollTerminal(containerId, position) {
    const container = document.getElementById(containerId);
    if (!container) return;
    
    const scrollHeight = container.scrollHeight - container.clientHeight;
    container.scrollTop = scrollHeight * Math.max(0, Math.min(1, position));
}

/**
 * Scrolls a terminal to the bottom
 * @param {string} containerId - The ID of the terminal container element
 */
export function scrollToBottom(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;
    
    container.scrollTop = container.scrollHeight;
}

/**
 * Gets the current scroll position of a terminal
 * @param {string} containerId - The ID of the terminal container element
 * @returns {number} The scroll position (0-1, where 1 is bottom)
 */
export function getScrollPosition(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return 1;
    
    const scrollHeight = container.scrollHeight - container.clientHeight;
    if (scrollHeight <= 0) return 1;
    
    return container.scrollTop / scrollHeight;
}
