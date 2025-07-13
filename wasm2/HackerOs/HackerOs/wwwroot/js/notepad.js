/**
 * Notepad.js - JavaScript helper functions for the NotepadApp component
 */

/**
 * Initialize the notepad textarea with enhanced functionality
 * @param {HTMLTextAreaElement} textArea - Reference to the textarea element
 */
export function initNotepad(textArea) {
    // Store a reference to the text area
    window.hackerOsNotepad = window.hackerOsNotepad || {};
    window.hackerOsNotepad.textArea = textArea;
    
    // Add tab key support
    textArea.addEventListener('keydown', handleTabKey);
    
    // Track cursor position for line and column display
    textArea.addEventListener('click', updateCursorPosition);
    textArea.addEventListener('keyup', updateCursorPosition);
    
    console.log("Notepad JS module initialized");
}

/**
 * Handle tab key in the textarea
 * @param {KeyboardEvent} e - Keyboard event
 */
function handleTabKey(e) {
    if (e.key === 'Tab') {
        e.preventDefault();
        
        const start = this.selectionStart;
        const end = this.selectionEnd;
        
        // Insert tab at cursor position
        this.value = this.value.substring(0, start) + '\t' + this.value.substring(end);
        
        // Set cursor position after the tab
        this.selectionStart = this.selectionEnd = start + 1;
    }
}

/**
 * Update cursor position statistics
 */
function updateCursorPosition() {
    const textArea = window.hackerOsNotepad.textArea;
    if (!textArea) return;
    
    const cursorPosition = textArea.selectionStart;
    const text = textArea.value.substring(0, cursorPosition);
    const lines = text.split('\n');
    
    const lineNumber = lines.length;
    const columnNumber = lines[lines.length - 1].length + 1;
    
    // Make the information available to the component
    window.hackerOsNotepad.cursorInfo = {
        line: lineNumber,
        column: columnNumber,
        position: cursorPosition
    };
    
    // Dispatch custom event with cursor information
    const event = new CustomEvent('notepad-cursor-changed', {
        detail: window.hackerOsNotepad.cursorInfo
    });
    
    document.dispatchEvent(event);
}

/**
 * Get cursor position information
 * @returns {Object} Object containing line, column, and absolute position
 */
export function getCursorPosition() {
    return window.hackerOsNotepad?.cursorInfo || { line: 1, column: 1, position: 0 };
}

/**
 * Get document statistics
 * @param {string} text - Text content to analyze
 * @returns {Object} Object containing line count, word count, and character count
 */
export function getDocumentStats(text) {
    if (!text) {
        return { lines: 0, words: 0, chars: 0 };
    }
    
    const lines = text.split('\n').length;
    const words = text.trim() ? text.trim().split(/\s+/).length : 0;
    const chars = text.length;
    
    return { lines, words, chars };
}

/**
 * Set selection in the textarea
 * @param {number} start - Start position for selection
 * @param {number} end - End position for selection
 */
export function setSelection(start, end) {
    const textArea = window.hackerOsNotepad?.textArea;
    if (!textArea) return;
    
    textArea.focus();
    textArea.selectionStart = start;
    textArea.selectionEnd = end || start;
}

/**
 * Get the current selection in the textarea
 * @returns {Object} Object containing start, end, and selected text
 */
export function getSelection() {
    const textArea = window.hackerOsNotepad?.textArea;
    if (!textArea) {
        return { start: 0, end: 0, text: '' };
    }
    
    return {
        start: textArea.selectionStart,
        end: textArea.selectionEnd,
        text: textArea.value.substring(textArea.selectionStart, textArea.selectionEnd)
    };
}

/**
 * Find text in the document
 * @param {string} searchText - Text to search for
 * @param {boolean} caseSensitive - Whether search is case sensitive
 * @param {boolean} wholeWord - Whether to match whole words only
 * @param {number} startPosition - Position to start search from
 * @returns {Object|null} Object with start and end positions if found, null otherwise
 */
export function findText(searchText, caseSensitive = false, wholeWord = false, startPosition = 0) {
    const textArea = window.hackerOsNotepad?.textArea;
    if (!textArea || !searchText) return null;
    
    let text = textArea.value;
    let search = searchText;
    
    if (!caseSensitive) {
        text = text.toLowerCase();
        search = search.toLowerCase();
    }
    
    let startIndex = text.indexOf(search, startPosition);
    
    if (startIndex === -1) {
        // Wrap around to beginning if not found
        startIndex = text.indexOf(search, 0);
    }
    
    if (startIndex !== -1) {
        // Check for whole word match if required
        if (wholeWord) {
            const beforeChar = startIndex === 0 ? ' ' : text.charAt(startIndex - 1);
            const afterChar = startIndex + search.length >= text.length ? ' ' : text.charAt(startIndex + search.length);
            
            if (/\w/.test(beforeChar) || /\w/.test(afterChar)) {
                // Not a whole word match, try to find next
                return findText(searchText, caseSensitive, wholeWord, startIndex + 1);
            }
        }
        
        return {
            start: startIndex,
            end: startIndex + search.length
        };
    }
    
    return null;
}

/**
 * Clean up resources when component is disposed
 */
export function dispose() {
    const textArea = window.hackerOsNotepad?.textArea;
    if (textArea) {
        textArea.removeEventListener('keydown', handleTabKey);
        textArea.removeEventListener('click', updateCursorPosition);
        textArea.removeEventListener('keyup', updateCursorPosition);
    }
    
    // Clear references
    window.hackerOsNotepad = null;
    
    console.log("Notepad JS module disposed");
}
