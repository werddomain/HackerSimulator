// welcome-animation.js
// Matrix-like code animation for the welcome page

export function initializeCodeAnimation() {
    const container = document.getElementById('codeBackground');
    if (!container) return;
    
    // Clear any existing elements
    while (container.firstChild) {
        container.removeChild(container.firstChild);
    }

    // Matrix code characters
    const matrixChars = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz{}<>[]()$#@!%^&*";
    
    // Create code lines
    const numLines = 30;
    for (let i = 0; i < numLines; i++) {
        createCodeLine(container, matrixChars);
    }
}

function createCodeLine(container, chars) {
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
    line.style.opacity = 0.1 + Math.random() * 0.2;
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
        line.remove();
        createCodeLine(container, chars);
    }, (duration + delay) * 1000);
}

// Initialize animation when document is loaded
//if (typeof window !== 'undefined') {
//    // Make the function available globally so Blazor can call it
//    window.initializeCodeAnimation = initializeCodeAnimation;
//}

// No need to export for module use since we're using window global
