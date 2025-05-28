using Microsoft.AspNetCore.Components.Web;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// Service for handling keyboard input in the terminal
    /// </summary>
    public class KeyboardService
    {
        /// <summary>
        /// Event that fires when input is received
        /// </summary>
        public event EventHandler<string>? InputReceived;

        /// <summary>
        /// Processes a keyboard event and converts it to terminal input
        /// </summary>
        /// <param name="e">The keyboard event</param>
        /// <returns>The processed input string or null if no input was produced</returns>
        public string? ProcessKeyEvent(KeyboardEventArgs e)
        {
            // Check for control/meta key combinations
            if (e.CtrlKey)
            {
                string? ctrlInput = ProcessCtrlKey(e.Key);
                if (!string.IsNullOrEmpty(ctrlInput))
                {
                    RaiseInputReceived(ctrlInput);
                    return ctrlInput;
                }
            }

            // Process standard keys
            string? input = ProcessKey(e.Key);
            if (!string.IsNullOrEmpty(input))
            {
                RaiseInputReceived(input);
                return input;
            }

            return null;
        }

        /// <summary>
        /// Handles a key down event
        /// </summary>
        /// <param name="e">The keyboard event</param>
        public void HandleKeyDown(KeyboardEventArgs e)
        {
            ProcessKeyEvent(e);
        }

        /// <summary>
        /// Processes a standard key and converts it to terminal input
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The input string or null</returns>
        private string? ProcessKey(string key)
        {
            switch (key)
            {
                // Control characters
                case "Enter":
                    return "\r";
                case "Tab":
                    return "\t";
                case "Escape":
                    return "\u001b";
                case "Backspace":
                    return "\b";
                case "Delete":
                    return "\u001b[3~";

                // Arrow keys (ANSI escape sequences)
                case "ArrowUp":
                    return "\u001b[A";
                case "ArrowDown":
                    return "\u001b[B";
                case "ArrowRight":
                    return "\u001b[C";
                case "ArrowLeft":
                    return "\u001b[D";

                // Navigation keys
                case "Home":
                    return "\u001b[H";
                case "End":
                    return "\u001b[F";
                case "PageUp":
                    return "\u001b[5~";
                case "PageDown":
                    return "\u001b[6~";

                // Standard character
                default:
                    // Only process single characters
                    if (key.Length == 1)
                    {
                        return key;
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// Processes a control key combination
        /// </summary>
        /// <param name="key">The key pressed with Ctrl</param>
        /// <returns>The control character or null</returns>
        private string? ProcessCtrlKey(string key)
        {
            // Convert the key to uppercase for processing
            string upperKey = key.ToUpperInvariant();

            // Handle Ctrl+A through Ctrl+Z
            if (upperKey.Length == 1 && upperKey[0] >= 'A' && upperKey[0] <= 'Z')
            {
                // ASCII control characters are calculated as:
                // Ctrl+A = 1, Ctrl+B = 2, etc.
                char controlChar = (char)(upperKey[0] - 'A' + 1);
                return new string(controlChar, 1);
            }

            // Special control combinations
            switch (upperKey)
            {
                case "@": // Ctrl+@
                    return "\u0000";
                case "[": // Ctrl+[
                    return "\u001b"; // ESC
                case "\\": // Ctrl+\
                    return "\u001c"; // File Separator
                case "]": // Ctrl+]
                    return "\u001d"; // Group Separator
                case "^": // Ctrl+^
                    return "\u001e"; // Record Separator
                case "_": // Ctrl+_
                    return "\u001f"; // Unit Separator
                case "?": // Ctrl+?
                    return "\u007f"; // DEL
            }

            return null;
        }

        /// <summary>
        /// Raises the InputReceived event with the provided input
        /// </summary>
        /// <param name="input">The input string</param>
        private void RaiseInputReceived(string input)
        {
            InputReceived?.Invoke(this, input);
        }
    }
}
