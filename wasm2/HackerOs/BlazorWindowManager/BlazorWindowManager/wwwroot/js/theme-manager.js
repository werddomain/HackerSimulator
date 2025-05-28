/**
 * Blazor Window Manager - Theme Management JavaScript Module
 * Handles CSS injection, theme switching, and variable management
 */

let loadedThemes = new Set();
let themeStyleElements = new Map();

/**
 * Adds a theme CSS class to the document body
 * @param {string} className - CSS class name to add
 */
export function addThemeClass(className) {
    document.body.classList.add(className);
}

/**
 * Removes a theme CSS class from the document body
 * @param {string} className - CSS class name to remove
 */
export function removeThemeClass(className) {
    document.body.classList.remove(className);
}

/**
 * Loads theme CSS from a file path
 * @param {string} themeId - Unique theme identifier
 * @param {string} cssFilePath - Path to the CSS file
 */
export function loadThemeCSS(themeId, cssFilePath) {
    // Remove existing theme CSS if it exists
    if (themeStyleElements.has(themeId)) {
        const existingElement = themeStyleElements.get(themeId);
        existingElement.remove();
        themeStyleElements.delete(themeId);
        loadedThemes.delete(themeId);
    }

    // Create and inject new theme CSS
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = cssFilePath;
    link.id = `bwm-theme-${themeId}`;
    link.setAttribute('data-theme-id', themeId);
    
    // Add to head
    document.head.appendChild(link);
    
    // Track the element
    themeStyleElements.set(themeId, link);
    loadedThemes.add(themeId);
    
    // Return a promise that resolves when CSS is loaded
    return new Promise((resolve, reject) => {
        link.onload = () => resolve();
        link.onerror = () => reject(new Error(`Failed to load theme CSS: ${cssFilePath}`));
    });
}

/**
 * Applies CSS custom properties to the document root
 * @param {Object} variables - Key-value pairs of CSS custom properties
 */
export function applyThemeVariables(variables) {
    const root = document.documentElement;
    
    for (const [property, value] of Object.entries(variables)) {
        // Ensure property starts with --
        const cssProperty = property.startsWith('--') ? property : `--${property}`;
        root.style.setProperty(cssProperty, value);
    }
}

/**
 * Sets a single custom CSS variable
 * @param {string} variableName - CSS custom property name
 * @param {string} value - CSS custom property value
 */
export function setCustomVariable(variableName, value) {
    const root = document.documentElement;
    const cssProperty = variableName.startsWith('--') ? variableName : `--${variableName}`;
    root.style.setProperty(cssProperty, value);
}

/**
 * Removes a custom CSS variable
 * @param {string} variableName - CSS custom property name to remove
 */
export function removeCustomVariable(variableName) {
    const root = document.documentElement;
    const cssProperty = variableName.startsWith('--') ? variableName : `--${variableName}`;
    root.style.removeProperty(cssProperty);
}

/**
 * Gets the current value of a CSS custom property
 * @param {string} variableName - CSS custom property name
 * @returns {string} Current value of the CSS property
 */
export function getCustomVariable(variableName) {
    const root = document.documentElement;
    const cssProperty = variableName.startsWith('--') ? variableName : `--${variableName}`;
    return getComputedStyle(root).getPropertyValue(cssProperty).trim();
}

/**
 * Saves theme settings to localStorage
 * @param {string} settingsJson - JSON string of theme settings
 */
export function saveSettings(settingsJson) {
    try {
        localStorage.setItem('bwm-theme-settings', settingsJson);
    } catch (error) {
        console.warn('Failed to save theme settings to localStorage:', error);
    }
}

/**
 * Loads theme settings from localStorage
 * @returns {string} JSON string of theme settings
 */
export function loadSettings() {
    try {
        return localStorage.getItem('bwm-theme-settings') || '';
    } catch (error) {
        console.warn('Failed to load theme settings from localStorage:', error);
        return '';
    }
}

/**
 * Clears all theme-related CSS and resets to defaults
 */
export function clearAllThemes() {
    // Remove all theme CSS files
    for (const [themeId, element] of themeStyleElements) {
        element.remove();
    }
    
    // Clear tracking
    themeStyleElements.clear();
    loadedThemes.clear();
    
    // Remove all theme classes from body
    const themeClasses = Array.from(document.body.classList)
        .filter(className => className.startsWith('bwm-theme-'));
    
    document.body.classList.remove(...themeClasses);
    
    // Reset CSS custom properties to defaults
    resetToDefaultVariables();
}

/**
 * Gets information about currently loaded themes
 * @returns {Object} Information about loaded themes
 */
export function getLoadedThemesInfo() {
    return {
        loadedThemes: Array.from(loadedThemes),
        activeClasses: Array.from(document.body.classList)
            .filter(className => className.startsWith('bwm-theme-')),
        customVariables: getAllCustomVariables()
    };
}

/**
 * Gets all CSS custom properties currently set on the document root
 * @returns {Object} Key-value pairs of all custom properties
 */
function getAllCustomVariables() {
    const root = document.documentElement;
    const computedStyle = getComputedStyle(root);
    const customProps = {};
    
    // Get all CSS custom properties
    for (let i = 0; i < computedStyle.length; i++) {
        const property = computedStyle[i];
        if (property.startsWith('--bwm-')) {
            customProps[property] = computedStyle.getPropertyValue(property).trim();
        }
    }
    
    return customProps;
}

/**
 * Resets CSS custom properties to their default values
 * This would typically load the default theme variables
 */
function resetToDefaultVariables() {
    // Remove all BWM custom properties
    const root = document.documentElement;
    const computedStyle = getComputedStyle(root);
    
    for (let i = 0; i < computedStyle.length; i++) {
        const property = computedStyle[i];
        if (property.startsWith('--bwm-')) {
            root.style.removeProperty(property);
        }
    }
}

/**
 * Applies theme transition effects when switching themes
 * @param {number} duration - Duration of the transition in milliseconds
 */
export function applyThemeTransition(duration = 300) {
    document.body.style.transition = `all ${duration}ms ease-in-out`;
    
    // Remove transition after it completes
    setTimeout(() => {
        document.body.style.transition = '';
    }, duration);
}

/**
 * Detects if the user prefers dark mode
 * @returns {boolean} True if dark mode is preferred
 */
export function prefersDarkMode() {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

/**
 * Listens for system theme changes and returns the callback ID
 * @param {Function} callback - Function to call when theme preference changes
 * @returns {MediaQueryList} Media query list for cleanup
 */
export function listenForSystemThemeChanges(callback) {
    if (window.matchMedia) {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        mediaQuery.addEventListener('change', callback);
        return mediaQuery;
    }
    return null;
}
