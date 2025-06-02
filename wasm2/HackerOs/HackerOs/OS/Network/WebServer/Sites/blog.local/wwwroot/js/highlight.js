/**
 * Syntax highlighting for code blocks
 */

// Simulated highlight.js implementation for the HackerOS simulator
const hljs = {
    highlightBlock: function(block) {
        // This would normally highlight code syntax
        // For the simulation, we'll just add a class
        block.classList.add('hljs-highlighted');
    }
};
