// Text Editor JavaScript interop functions

window.textEditorApp = {
    // Focus the text editor
    focusEditor: function() {
        const textarea = document.querySelector('.text-content');
        if (textarea) {
            textarea.focus();
            return true;
        }
        return false;
    },
    
    // Get cursor position in the textarea
    getCursorPosition: function() {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return { line: 1, column: 1 };
        
        const value = textarea.value;
        const selectionStart = textarea.selectionStart;
        
        // Count lines and columns
        let line = 1;
        let column = 1;
        
        for (let i = 0; i < selectionStart; i++) {
            if (value[i] === '\n') {
                line++;
                column = 1;
            } else {
                column++;
            }
        }
        
        return { line, column };
    },
    
    // Set cursor position in the textarea
    setCursorPosition: function(position) {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return false;
        
        const value = textarea.value;
        let currentLine = 1;
        let currentCol = 1;
        let index = 0;
        
        // Find the index based on line and column
        while (index < value.length) {
            if (currentLine === position.line && currentCol === position.column) {
                break;
            }
            
            if (value[index] === '\n') {
                currentLine++;
                currentCol = 1;
            } else {
                currentCol++;
            }
            
            index++;
        }
        
        textarea.focus();
        textarea.setSelectionRange(index, index);
        return true;
    },
    
    // Select text at specified range
    selectText: function(start, length) {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return false;
        
        textarea.focus();
        textarea.setSelectionRange(start, start + length);
        
        // Scroll to selection
        const textBeforeSelection = textarea.value.substring(0, start);
        const linesBeforeSelection = textBeforeSelection.split('\n').length;
        
        // Create a temporary div with same styling
        const temp = document.createElement('div');
        temp.style.cssText = window.getComputedStyle(textarea, null).cssText;
        temp.style.height = 'auto';
        temp.style.position = 'absolute';
        temp.style.visibility = 'hidden';
        temp.innerText = textBeforeSelection;
        document.body.appendChild(temp);
        
        // Calculate scroll position
        const lineHeight = parseInt(window.getComputedStyle(textarea).lineHeight) || 16;
        const scrollTop = (linesBeforeSelection - 1) * lineHeight;
        
        // Scroll to position
        textarea.scrollTop = scrollTop;
        
        // Clean up
        document.body.removeChild(temp);
        
        return true;
    },
    
    // Check if text is selected
    hasSelection: function() {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return false;
        
        return textarea.selectionStart !== textarea.selectionEnd;
    },
    
    // Get selected text
    getSelectedText: function() {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return '';
        
        return textarea.value.substring(textarea.selectionStart, textarea.selectionEnd);
    },
    
    // Replace selected text
    replaceSelection: function(newText) {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return false;
        
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;
        
        if (start === end) return false;
        
        // Use input event to trigger binding update
        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value").set;
        const newValue = textarea.value.substring(0, start) + newText + textarea.value.substring(end);
        
        nativeInputValueSetter.call(textarea, newValue);
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
        
        // Set selection after the replaced text
        textarea.selectionStart = start + newText.length;
        textarea.selectionEnd = start + newText.length;
        
        return true;
    },
    
    // Insert text at current cursor position
    insertTextAtCursor: function(text) {
        const textarea = document.querySelector('.text-content');
        if (!textarea) return false;
        
        const start = textarea.selectionStart;
        
        // Use input event to trigger binding update
        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value").set;
        const newValue = textarea.value.substring(0, start) + text + textarea.value.substring(textarea.selectionEnd);
        
        nativeInputValueSetter.call(textarea, newValue);
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
        
        // Set cursor after inserted text
        textarea.selectionStart = start + text.length;
        textarea.selectionEnd = start + text.length;
        
        return true;
    }
};
