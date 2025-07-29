/**
 * Blazor Window Manager - Keyboard Navigation Module
 * Handles keyboard shortcuts, window switching, and accessibility features
 * Uses browser-safe key combinations instead of Alt+Tab
 */

let keyboardNavigationService = null;
let config = null;
let isInitialized = false;
let activeKeyHandlers = new Map();
let windowSwitcherTimeout = null;

/**
 * Initialize the keyboard navigation system
 * @param {object} dotNetService - Reference to the .NET KeyboardNavigationService
 * @param {object} navigationConfig - Configuration options
 */
export function initialize(dotNetService, navigationConfig) {
    if (isInitialized) {
        console.warn('Keyboard navigation already initialized');
        return;
    }

    keyboardNavigationService = dotNetService;
    config = navigationConfig || {};
    isInitialized = true;

    // Set up global keyboard event listeners
    setupGlobalKeyboardListeners();

    console.log('Keyboard navigation initialized with browser-safe shortcuts', config);
}

/**
 * Set up global keyboard event listeners
 */
function setupGlobalKeyboardListeners() {
    // Handle keydown events for shortcuts and navigation
    document.addEventListener('keydown', handleGlobalKeyDown, true);
    document.addEventListener('keyup', handleGlobalKeyUp, true);

    // Handle focus events for window management
    document.addEventListener('focusin', handleFocusIn);
    document.addEventListener('focusout', handleFocusOut);
}

/**
 * Handle global keydown events with browser-safe shortcuts
 * @param {KeyboardEvent} event 
 */
function handleGlobalKeyDown(event) {
    if (!isInitialized || !keyboardNavigationService) return;

    // Don't handle if user is typing in input elements
    if (isTypingContext(event.target)) return;

    const key = event.key;
    const ctrlKey = event.ctrlKey;
    const altKey = event.altKey;
    const shiftKey = event.shiftKey;
    const metaKey = event.metaKey;

    // Create a list of browser-safe keyboard shortcuts
    const keyboardShortcuts = [
        // Window switcher - Ctrl+` (backtick)
        { keys: ['`'], modifiers: { ctrl: true }, action: 'windowSwitcher' },
        // Close window - Ctrl+Shift+W
        { keys: ['w', 'W'], modifiers: { ctrl: true, shift: true }, action: 'closeWindow' },
        // Maximize window - Ctrl+Shift+M
        { keys: ['m', 'M'], modifiers: { ctrl: true, shift: true }, action: 'maximizeWindow' },
        // Minimize window - Ctrl+Shift+N
        { keys: ['n', 'N'], modifiers: { ctrl: true, shift: true }, action: 'minimizeWindow' },
        // Cycle windows - Ctrl+Tab
        { keys: ['Tab'], modifiers: { ctrl: true }, action: 'cycleNext' },
        // Cycle windows backward - Ctrl+Shift+Tab
        { keys: ['Tab'], modifiers: { ctrl: true, shift: true }, action: 'cyclePrevious' },
        // Window context menu - Ctrl+Shift+Space
        { keys: [' '], modifiers: { ctrl: true, shift: true }, action: 'contextMenu' },
        // Move window - Ctrl+Arrow
        { keys: ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'], modifiers: { ctrl: true }, action: 'moveWindow' },
        // Resize window - Ctrl+Shift+Arrow
        { keys: ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'], modifiers: { ctrl: true, shift: true }, action: 'resizeWindow' }
    ];

    // Check if current key combination matches any shortcut
    for (const shortcut of keyboardShortcuts) {
        if (matchesShortcut(key, { ctrl: ctrlKey, alt: altKey, shift: shiftKey, meta: metaKey }, shortcut)) {
            event.preventDefault();
            event.stopPropagation();
            
            // Use the centralized HandleKeyboardEvent method
            keyboardNavigationService.invokeMethodAsync('HandleKeyboardEvent', key, ctrlKey, altKey, shiftKey, metaKey);
            return;
        }
    }
}

/**
 * Handle global keyup events
 * @param {KeyboardEvent} event 
 */
function handleGlobalKeyUp(event) {
    if (!isInitialized || !keyboardNavigationService) return;

    // Handle Escape key to cancel window switcher (universal cancel)
    if (event.key === 'Escape' && activeKeyHandlers.size > 0) {
        activeKeyHandlers.clear();
        keyboardNavigationService.invokeMethodAsync('CancelWindowSwitcher');
    }

    // Handle specific key releases for window switcher
    if (event.key === 'Control' && activeKeyHandlers.has('windowSwitcher')) {
        activeKeyHandlers.delete('windowSwitcher');
        keyboardNavigationService.invokeMethodAsync('ConfirmWindowSwitcher');
    }
}

/**
 * Handle focus events
 * @param {FocusEvent} event 
 */
function handleFocusIn(event) {
    // Track focus for accessibility
    if (event.target.hasAttribute('data-window-id')) {
        const windowId = event.target.getAttribute('data-window-id');
        // Could notify the service about focus change
    }
}

/**
 * Handle focus out events
 * @param {FocusEvent} event 
 */
function handleFocusOut(event) {
    // Handle focus leaving windows
}

/**
 * Check if a key combination matches a defined shortcut
 * @param {string} key - The pressed key
 * @param {object} currentModifiers - Current modifier state
 * @param {object} shortcut - Shortcut definition
 * @returns {boolean}
 */
function matchesShortcut(key, currentModifiers, shortcut) {
    // Check if key matches
    if (!shortcut.keys.includes(key)) return false;

    // Check modifiers
    const requiredModifiers = shortcut.modifiers;
    return (currentModifiers.ctrl === !!requiredModifiers.ctrl) &&
           (currentModifiers.alt === !!requiredModifiers.alt) &&
           (currentModifiers.shift === !!requiredModifiers.shift) &&
           (currentModifiers.meta === !!requiredModifiers.meta);
}

/**
 * Check if the user is in a typing context (input, textarea, contenteditable)
 * @param {Element} element 
 * @returns {boolean}
 */
function isTypingContext(element) {
    if (!element) return false;
    
    const tagName = element.tagName.toLowerCase();
    return tagName === 'input' || 
           tagName === 'textarea' || 
           element.contentEditable === 'true' ||
           element.hasAttribute('contenteditable');
}

/**
 * Focus on a specific window
 * @param {string} windowId 
 */
export function focusWindow(windowId) {
    const windowElement = document.querySelector(`[data-window-id="${windowId}"]`);
    if (windowElement) {
        // Focus the window element
        windowElement.focus();
        
        // Add focus styling
        windowElement.classList.add('bwm-window-focused');
        
        // Remove focus from other windows
        document.querySelectorAll('.bwm-window-focused').forEach(el => {
            if (el !== windowElement) {
                el.classList.remove('bwm-window-focused');
            }
        });

        // Ensure the window is visible and accessible
        ensureWindowAccessible(windowElement);
    }
}

/**
 * Highlight a window for the window switcher
 * @param {string} windowId 
 */
export function highlightWindow(windowId) {
    const windowElement = document.querySelector(`[data-window-id="${windowId}"]`);
    if (windowElement) {
        // Add highlight styling
        windowElement.classList.add('bwm-window-switcher-highlight');
        
        // Remove highlight from other windows
        document.querySelectorAll('.bwm-window-switcher-highlight').forEach(el => {
            if (el !== windowElement) {
                el.classList.remove('bwm-window-switcher-highlight');
            }
        });

        // Ensure the highlighted window is visible
        ensureWindowVisible(windowElement);
        
        // Set active key handler for window switcher
        activeKeyHandlers.set('windowSwitcher', true);
    }
}

/**
 * Clear all window highlighting
 */
export function clearWindowHighlighting() {
    document.querySelectorAll('.bwm-window-switcher-highlight').forEach(el => {
        el.classList.remove('bwm-window-switcher-highlight');
    });
    activeKeyHandlers.delete('windowSwitcher');
}

/**
 * Ensure window is accessible (proper ARIA attributes, etc.)
 * @param {HTMLElement} windowElement 
 */
function ensureWindowAccessible(windowElement) {
    // Add ARIA attributes if missing
    if (!windowElement.hasAttribute('role')) {
        windowElement.setAttribute('role', 'dialog');
    }
    
    if (!windowElement.hasAttribute('aria-label')) {
        const titleElement = windowElement.querySelector('.bwm-window-title');
        if (titleElement) {
            windowElement.setAttribute('aria-label', titleElement.textContent || 'Window');
        }
    }

    // Ensure focusable
    if (!windowElement.hasAttribute('tabindex')) {
        windowElement.setAttribute('tabindex', '-1');
    }

    // Announce to screen readers if configured
    if (config.ShowVisualFeedback) {
        announceToScreenReader(`Window focused: ${windowElement.getAttribute('aria-label')}`);
    }
}

/**
 * Ensure window is visible on screen
 * @param {HTMLElement} windowElement 
 */
function ensureWindowVisible(windowElement) {
    const rect = windowElement.getBoundingClientRect();
    const viewport = {
        width: window.innerWidth,
        height: window.innerHeight
    };

    // Check if window is mostly visible
    const isVisible = rect.left >= 0 && rect.top >= 0 && 
                     rect.right <= viewport.width && rect.bottom <= viewport.height;

    if (!isVisible) {
        // Scroll into view if needed
        windowElement.scrollIntoView({
            behavior: 'smooth',
            block: 'center',
            inline: 'center'
        });
    }
}

/**
 * Announce text to screen readers
 * @param {string} text 
 */
function announceToScreenReader(text) {
    // Create or reuse live region for announcements
    let liveRegion = document.getElementById('bwm-screen-reader-announcements');
    if (!liveRegion) {
        liveRegion = document.createElement('div');
        liveRegion.id = 'bwm-screen-reader-announcements';
        liveRegion.setAttribute('aria-live', 'polite');
        liveRegion.setAttribute('aria-atomic', 'true');
        liveRegion.style.position = 'absolute';
        liveRegion.style.left = '-10000px';
        liveRegion.style.width = '1px';
        liveRegion.style.height = '1px';
        liveRegion.style.overflow = 'hidden';
        document.body.appendChild(liveRegion);
    }

    // Clear and set new text
    liveRegion.textContent = '';
    setTimeout(() => {
        liveRegion.textContent = text;
    }, 100);
}

/**
 * Get the current browser-safe shortcut mapping
 * @returns {object} Shortcut mapping object
 */
export function getShortcutMapping() {
    return {
        windowSwitcher: 'Ctrl + `',
        closeWindow: 'Ctrl + Shift + W',
        maximizeWindow: 'Ctrl + Shift + M',
        minimizeWindow: 'Ctrl + Shift + N',
        cycleNext: 'Ctrl + Tab',
        cyclePrevious: 'Ctrl + Shift + Tab',
        contextMenu: 'Ctrl + Shift + Space',
        moveWindow: 'Ctrl + Arrow Keys',
        resizeWindow: 'Ctrl + Shift + Arrow Keys'
    };
}

/**
 * Dispose of the keyboard navigation system
 */
export function dispose() {
    if (!isInitialized) return;

    // Remove event listeners
    document.removeEventListener('keydown', handleGlobalKeyDown, true);
    document.removeEventListener('keyup', handleGlobalKeyUp, true);
    document.removeEventListener('focusin', handleFocusIn);
    document.removeEventListener('focusout', handleFocusOut);

    // Clear any active handlers
    activeKeyHandlers.clear();

    // Clear timeouts
    if (windowSwitcherTimeout) {
        clearTimeout(windowSwitcherTimeout);
        windowSwitcherTimeout = null;
    }

    // Clean up references
    keyboardNavigationService = null;
    config = null;
    isInitialized = false;

    console.log('Keyboard navigation disposed');
}
