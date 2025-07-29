// Window interaction handling for Blazor Window Manager
// Provides mouse event handling for dragging and resizing windows

let currentDragTarget = null;
let currentResizeTarget = null;
let isMouseDown = false;

// Initialize global mouse event listeners
document.addEventListener('mousemove', handleGlobalMouseMove);
document.addEventListener('mouseup', handleGlobalMouseUp);
document.addEventListener('selectstart', handleSelectStart);

/**
 * Starts dragging mode for a window
 * @param {object} dotNetRef - DotNet object reference for callbacks
 */
export function startDragging(dotNetRef) {
    currentDragTarget = dotNetRef;
    isMouseDown = true;
    document.body.style.cursor = 'move';
    document.body.style.userSelect = 'none';
    
    // Add visual feedback
    document.body.classList.add('window-dragging');
}

/**
 * Starts resizing mode for a window
 * @param {object} dotNetRef - DotNet object reference for callbacks
 * @param {string} direction - Direction of resize (n, s, e, w, ne, nw, se, sw)
 */
export function startResizing(dotNetRef, direction) {
    currentResizeTarget = dotNetRef;
    isMouseDown = true;
    document.body.style.userSelect = 'none';
    
    // Set appropriate cursor for resize direction
    const cursors = {
        'n': 'n-resize',
        's': 's-resize',
        'e': 'e-resize',
        'w': 'w-resize',
        'ne': 'ne-resize',
        'nw': 'nw-resize',
        'se': 'se-resize',
        'sw': 'sw-resize'
    };
    
    document.body.style.cursor = cursors[direction] || 'default';
    document.body.classList.add('window-resizing');
}

/**
 * Handles global mouse move events for dragging and resizing
 * @param {MouseEvent} e - Mouse event
 */
function handleGlobalMouseMove(e) {
    if (!isMouseDown) return;
    
    // Prevent default to avoid unwanted selections
    e.preventDefault();
    
    if (currentDragTarget) {
        // Call the Blazor component's drag handler
        currentDragTarget.invokeMethodAsync('OnDragMove', e.clientX, e.clientY);
    } else if (currentResizeTarget) {
        // Call the Blazor component's resize handler
        currentResizeTarget.invokeMethodAsync('OnResizeMove', e.clientX, e.clientY);
    }
}

/**
 * Handles global mouse up events to end dragging/resizing
 * @param {MouseEvent} e - Mouse event
 */
function handleGlobalMouseUp(e) {
    if (!isMouseDown) return;
    
    isMouseDown = false;
    
    // Reset cursor and selection
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
    document.body.classList.remove('window-dragging', 'window-resizing');
    
    if (currentDragTarget) {
        // Notify the component that dragging ended
        currentDragTarget.invokeMethodAsync('OnDragEnd');
        currentDragTarget = null;
    } else if (currentResizeTarget) {
        // Notify the component that resizing ended
        currentResizeTarget.invokeMethodAsync('OnResizeEnd');
        currentResizeTarget = null;
    }
}

/**
 * Prevents text selection during window operations
 * @param {Event} e - Select start event
 */
function handleSelectStart(e) {
    if (isMouseDown && (currentDragTarget || currentResizeTarget)) {
        e.preventDefault();
        return false;
    }
}

/**
 * Gets the current mouse position relative to an element
 * @param {MouseEvent} e - Mouse event
 * @param {Element} element - Element to get relative position for
 * @returns {object} Object with x and y coordinates
 */
export function getRelativeMousePosition(e, element) {
    const rect = element.getBoundingClientRect();
    return {
        x: e.clientX - rect.left,
        y: e.clientY - rect.top
    };
}

/**
 * Constrains a value within minimum and maximum bounds
 * @param {number} value - Value to constrain
 * @param {number} min - Minimum value
 * @param {number} max - Maximum value
 * @returns {number} Constrained value
 */
export function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

/**
 * Gets the boundaries of the desktop area for window constraints
 * @param {Element} desktopElement - Desktop container element
 * @returns {object} Object with width and height of available area
 */
export function getDesktopBounds(desktopElement) {
    if (!desktopElement) {
        return {
            width: window.innerWidth,
            height: window.innerHeight,
            left: 0,
            top: 0
        };
    }
    
    const rect = desktopElement.getBoundingClientRect();
    return {
        width: rect.width,
        height: rect.height,
        left: rect.left,
        top: rect.top
    };
}

/**
 * Calculates snapping position for window edges
 * @param {object} windowBounds - Current window bounds
 * @param {object} desktopBounds - Desktop area bounds
 * @param {number} snapThreshold - Distance threshold for snapping
 * @returns {object} Adjusted bounds with snapping applied
 */
export function calculateSnapPosition(windowBounds, desktopBounds, snapThreshold = 10) {
    let { left, top, width, height } = windowBounds;
    const { width: deskWidth, height: deskHeight, left: deskLeft, top: deskTop } = desktopBounds;
    
    // Snap to left edge
    if (Math.abs(left - deskLeft) < snapThreshold) {
        left = deskLeft;
    }
    
    // Snap to right edge
    if (Math.abs((left + width) - (deskLeft + deskWidth)) < snapThreshold) {
        left = deskLeft + deskWidth - width;
    }
    
    // Snap to top edge
    if (Math.abs(top - deskTop) < snapThreshold) {
        top = deskTop;
    }
    
    // Snap to bottom edge
    if (Math.abs((top + height) - (deskTop + deskHeight)) < snapThreshold) {
        top = deskTop + deskHeight - height;
    }
    
    return { left, top, width, height };
}

/**
 * Adds visual feedback during window operations
 */
export function addVisualFeedback() {
    // Add custom styles for better visual feedback
    if (!document.getElementById('window-manager-styles')) {
        const style = document.createElement('style');
        style.id = 'window-manager-styles';
        style.textContent = `
            .window-dragging * {
                pointer-events: none !important;
            }
            
            .window-resizing * {
                pointer-events: none !important;
            }
            
            .window-snap-preview {
                position: absolute;
                border: 2px dashed #00ff00;
                background: rgba(0, 255, 0, 0.1);
                pointer-events: none;
                z-index: 9999;
                border-radius: 4px;
            }
        `;
        document.head.appendChild(style);
    }
}

// Initialize visual feedback styles when module loads
addVisualFeedback();
