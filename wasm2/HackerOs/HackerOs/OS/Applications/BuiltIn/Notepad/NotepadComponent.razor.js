export function initializeNotepad(textArea) {
    // Set up key handlers
    textArea.addEventListener('keydown', handleKeyDown);
    
    // Focus the text area
    textArea.focus();
    
    // Set up keyboard shortcuts
    document.addEventListener('keydown', handleGlobalKeyDown);
    
    return {
        // This is called when the module is disposed
        dispose: () => {
            textArea.removeEventListener('keydown', handleKeyDown);
            document.removeEventListener('keydown', handleGlobalKeyDown);
        }
    };
}

function handleKeyDown(event) {
    // Handle tab key to insert spaces instead of changing focus
    if (event.key === 'Tab') {
        event.preventDefault();
        
        const start = event.target.selectionStart;
        const end = event.target.selectionEnd;
        
        // Insert 4 spaces at the current cursor position
        event.target.value = 
            event.target.value.substring(0, start) + 
            '    ' + 
            event.target.value.substring(end);
            
        // Set cursor position after the inserted tab
        event.target.selectionStart = 
        event.target.selectionEnd = start + 4;
        
        // Trigger input event to update the bound value
        event.target.dispatchEvent(new Event('input', { bubbles: true }));
    }
    
    // Handle auto-indent on Enter key
    if (event.key === 'Enter') {
        const start = event.target.selectionStart;
        const text = event.target.value;
        
        // Find the start of the current line
        let lineStart = start;
        while (lineStart > 0 && text[lineStart - 1] !== '\n') {
            lineStart--;
        }
        
        // Calculate the indentation of the current line
        let indentation = '';
        for (let i = lineStart; i < start; i++) {
            if (text[i] === ' ' || text[i] === '\t') {
                indentation += text[i];
            } else {
                break;
            }
        }
        
        // If we have indentation, handle it manually
        if (indentation.length > 0) {
            event.preventDefault();
            
            // Insert newline and indentation
            event.target.value = 
                text.substring(0, start) + 
                '\n' + indentation + 
                text.substring(start);
                
            // Set cursor position after the indentation
            const newPosition = start + 1 + indentation.length;
            event.target.selectionStart = 
            event.target.selectionEnd = newPosition;
            
            // Trigger input event to update the bound value
            event.target.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }
}

function handleGlobalKeyDown(event) {
    // Ctrl+S for save
    if (event.ctrlKey && event.key === 's') {
        event.preventDefault();
        // Call the C# SaveFile method via DotNet.invokeMethodAsync
        // This would require setting up the JS interop in the component
    }
    
    // Ctrl+O for open
    if (event.ctrlKey && event.key === 'o') {
        event.preventDefault();
        // Call the C# OpenFile method
    }
    
    // Ctrl+N for new
    if (event.ctrlKey && event.key === 'n') {
        event.preventDefault();
        // Call the C# NewFile method
    }
}

// Helper function to get text statistics (lines, words, characters)
export function getTextStatistics(text) {
    if (!text) return { lines: 0, words: 0, characters: 0 };
    
    const lines = text.split('\n').length;
    const words = text.split(/\s+/).filter(word => word.length > 0).length;
    const characters = text.length;
    
    return { lines, words, characters };
}

// Function to scroll to a specific line
export function scrollToLine(textArea, lineNumber) {
    const text = textArea.value;
    const lines = text.split('\n');
    
    if (lineNumber < 1 || lineNumber > lines.length) {
        return false;
    }
    
    // Calculate the position of the start of the requested line
    let position = 0;
    for (let i = 0; i < lineNumber - 1; i++) {
        position += lines[i].length + 1; // +1 for the newline character
    }
    
    // Set the selection to the start of the line
    textArea.focus();
    textArea.selectionStart = position;
    textArea.selectionEnd = position;
    
    // Scroll to make the selection visible
    textArea.scrollTop = textArea.scrollHeight * (position / text.length);
    
    return true;
}
