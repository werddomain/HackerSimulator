/**
 * JavaScript interop functions for the Calculator component
 */

/**
 * Sets up event listeners for keyboard events
 * @param {any} component - The .NET component reference
 */
export function setupKeyboardListeners(component) {
    // Remove any existing listeners first
    document.removeEventListener('keydown', handleKeyDown);
    
    // Set up the new listener
    document.addEventListener('keydown', handleKeyDown);
    
    /**
     * Handles keydown events and calls the appropriate .NET method
     * @param {KeyboardEvent} event - The keyboard event
     */
    function handleKeyDown(event) {
        // Only process keyboard events if the calculator has focus
        const calculatorContainer = document.querySelector('.calculator-container');
        if (!calculatorContainer || !calculatorContainer.contains(document.activeElement)) {
            return;
        }
        
        // Prevent default for calculator keys to avoid affecting other elements
        const calculatorKeys = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 
                                '+', '-', '*', '/', '=', 'Enter', '.', ',', 
                                'Backspace', 'Delete', 'Escape', '%'];
                                
        if (calculatorKeys.includes(event.key)) {
            event.preventDefault();
        }
        
        // Call the .NET method
        component.invokeMethodAsync('OnKeyDown', {
            key: event.key,
            altKey: event.altKey,
            ctrlKey: event.ctrlKey,
            shiftKey: event.shiftKey
        });
    }
}

/**
 * Cleans up event listeners when the component is disposed
 */
export function cleanupKeyboardListeners() {
    document.removeEventListener('keydown', handleKeyDown);
}

/**
 * Focuses the calculator when it's initialized
 */
export function focusCalculator() {
    const calculatorContainer = document.querySelector('.calculator-container');
    if (calculatorContainer) {
        calculatorContainer.focus();
    }
}
