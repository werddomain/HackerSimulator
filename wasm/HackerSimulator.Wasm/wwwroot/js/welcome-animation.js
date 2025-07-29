// welcome-animation.js
// Animations for welcome page: Matrix-like code animation and code typing animation

export function initializeCodeAnimation(showTypingAnimation = false) {
    const container = document.getElementById('codeBackground');
    if (!container) return;
    
    // Clear any existing elements
    while (container.firstChild) {
        container.removeChild(container.firstChild);
    }
    
    // Matrix code characters
    const matrixChars = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz{}<>[]()$#@!%^&*";
    
    if (showTypingAnimation) {
        // Show typing animation prominently and fewer matrix codes in the background
        createTypingCodeBlock(container);
        
        // Add some subtle matrix animation in the background (fewer lines)
        const numLines = 15;
        for (let i = 0; i < numLines; i++) {
            createCodeLine(container, matrixChars, 0.1); // Lower opacity for matrix in code mode
        }
    } else {
        // Matrix mode - more matrix code lines and no typing
        const numLines = 30;
        for (let i = 0; i < numLines; i++) {
            createCodeLine(container, matrixChars, 0.15); // Regular opacity for matrix mode
        }
    }
}

function createCodeLine(container, chars, baseOpacity = 0.15) {
    const line = document.createElement('div');
    line.className = 'code-line';
    
    // Random position and speed
    const x = Math.random() * 100;
    const duration = 5 + Math.random() * 20;
    const delay = Math.random() * 5;
    const fontSize = 12 + Math.random() * 8;
    
    // Set random styles
    line.style.left = `${x}%`;
    line.style.fontSize = `${fontSize}px`;
    line.style.opacity = baseOpacity + Math.random() * 0.1; // Use provided base opacity
    line.style.animationDuration = `${duration}s`;
    line.style.animationDelay = `${delay}s`;
    
    // Create random matrix text
    const length = 10 + Math.floor(Math.random() * 20);
    let text = '';
    for (let i = 0; i < length; i++) {
        text += chars[Math.floor(Math.random() * chars.length)];
    }
    line.textContent = text;
    
    // Add to container
    container.appendChild(line);
    
    // Replace the line when animation completes
    setTimeout(() => {
        if (container.isConnected) { // Check if container still exists
            line.remove();
            createCodeLine(container, chars, baseOpacity);
        }
    }, (duration + delay) * 1000);
}

// Typing code animation
function createTypingCodeBlock(container) {
    const codeBlock = document.createElement('div');
    codeBlock.className = 'typing-code-block';
    container.appendChild(codeBlock);
    
    // Sample code for hacking-themed typing animation with syntax highlighting
    const codeLines = [
        { text: '#!/usr/bin/env python', class: 'comment' },
        { text: 'import sys', class: 'keyword' },
        { text: 'import socket', class: 'keyword' },
        { text: 'import time', class: 'keyword' },
        { text: 'import threading', class: 'keyword' },
        { text: 'from cryptography.fernet import Fernet', class: 'keyword' },
        { text: '', class: '' },
        { text: 'class HackerOS:', class: 'class-def' },
        { text: '    def __init__(self):', class: 'function' },
        { text: '        self.target = "192.168.1.1"', class: 'string' },
        { text: '        self.port = 22', class: 'number' },
        { text: '        self.key = Fernet.generate_key()', class: 'variable' },
        { text: '        self.cipher = Fernet(self.key)', class: 'variable' },
        { text: '', class: '' },
        { text: '    def scan_network(self):', class: 'function' },
        { text: '        print("Scanning network...")', class: 'string' },
        { text: '        for i in range(1, 255):', class: 'control' },
        { text: '            ip = f"192.168.1.{i}"', class: 'string' },
        { text: '            # Check if host is up', class: 'comment' },
        { text: '            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)', class: 'variable' },
        { text: '            sock.settimeout(0.5)', class: 'variable' },
        { text: '            result = sock.connect_ex((ip, 80))', class: 'variable' },
        { text: '            if result == 0:', class: 'control' },
        { text: '                print(f"Host {ip} is up")', class: 'string' },
        { text: '            sock.close()', class: 'function' },
        { text: '', class: '' },
        { text: '    def secure_connection(self):', class: 'function' },
        { text: '        encrypted = self.cipher.encrypt(b"CONNECT TO TARGET")', class: 'variable' },
        { text: '        # Establish encrypted tunnel', class: 'comment' },
        { text: '        return encrypted', class: 'keyword' },
        { text: '', class: '' },
        { text: '# Initialize system', class: 'comment' },
        { text: 'if __name__ == "__main__":', class: 'control' },
        { text: '    os = HackerOS()', class: 'variable' },
        { text: '    os.scan_network()', class: 'function' },
        { text: '    connection = os.secure_connection()', class: 'variable' },
        { text: '    print("System initialized.")', class: 'string' }
    ];
    
    // Function to type out code with syntax highlighting
    function typeCode() {
        let lineIndex = 0;
        let charIndex = 0;
        
        // Create a div for the current line
        let currentLine = document.createElement('div');
        currentLine.className = 'code-typed-line';
        codeBlock.appendChild(currentLine);
        
        // Typing effect interval
        const typingInterval = setInterval(() => {
            // If we've finished all lines, clear the interval
            if (lineIndex >= codeLines.length) {
                clearInterval(typingInterval);
                
                // Start over after a delay
                setTimeout(() => {
                    codeBlock.innerHTML = '';
                    typeCode();
                }, 5000);
                
                return;
            }
            
            const currentLineData = codeLines[lineIndex];
            
            // If we're at the start of a line, add proper class
            if (charIndex === 0) {
                currentLine.className = `code-typed-line ${currentLineData.class}`;
            }
            
            // If we've finished the current line
            if (charIndex >= currentLineData.text.length) {
                lineIndex++;
                charIndex = 0;
                
                // If there are more lines, create a new line element
                if (lineIndex < codeLines.length) {
                    currentLine = document.createElement('div');
                    currentLine.className = 'code-typed-line';
                    codeBlock.appendChild(currentLine);
                }
                
                return;
            }
            
            // Type the next character
            currentLine.textContent += currentLineData.text[charIndex];
            charIndex++;
            
            // Scroll to bottom if needed
            codeBlock.scrollTop = codeBlock.scrollHeight;
            
        }, 30 + Math.random() * 50); // Random typing speed for realism
    }
    
    // Start typing animation
    typeCode();
}

// Initialize animation when document is loaded
//if (typeof window !== 'undefined') {
//    // Make the function available globally so Blazor can call it
//    window.initializeCodeAnimation = initializeCodeAnimation;
//}

// No need to export for module use since we're using window global
