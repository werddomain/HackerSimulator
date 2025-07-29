using BlazorTerminal.Models;
using System.Drawing;
using System.Text;

namespace BlazorTerminal.Extensions
{
    /// <summary>
    /// Extension methods for terminal functionality
    /// </summary>
    public static class TerminalExtensions
    {
        /// <summary>
        /// Writes text in the specified color
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="text">The text to write</param>
        /// <param name="foreground">The foreground color</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal WriteColored(this Components.Terminal terminal, string text, Color foreground)
        {
            string coloredText = $"\u001b[38;2;{foreground.R};{foreground.G};{foreground.B}m{text}\u001b[0m";
            terminal.Write(coloredText);
            return terminal;
        }
        
        /// <summary>
        /// Writes text in the specified color with background
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="text">The text to write</param>
        /// <param name="foreground">The foreground color</param>
        /// <param name="background">The background color</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal WriteColored(this Components.Terminal terminal, string text, Color foreground, Color background)
        {
            string coloredText = $"\u001b[38;2;{foreground.R};{foreground.G};{foreground.B};48;2;{background.R};{background.G};{background.B}m{text}\u001b[0m";
            terminal.Write(coloredText);
            return terminal;
        }
        
        /// <summary>
        /// Writes a success message in green
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="text">The text to write</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal WriteSuccess(this Components.Terminal terminal, string text)
        {
            return terminal.WriteColored(text, Color.FromArgb(0, 204, 0));
        }
        
        /// <summary>
        /// Writes an error message in red
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="text">The text to write</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal WriteError(this Components.Terminal terminal, string text)
        {
            return terminal.WriteColored(text, Color.FromArgb(255, 50, 50));
        }
        
        /// <summary>
        /// Writes a warning message in yellow
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="text">The text to write</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal WriteWarning(this Components.Terminal terminal, string text)
        {
            return terminal.WriteColored(text, Color.FromArgb(255, 204, 0));
        }
        
        /// <summary>
        /// Writes an info message in blue
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="text">The text to write</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal WriteInfo(this Components.Terminal terminal, string text)
        {
            return terminal.WriteColored(text, Color.FromArgb(50, 150, 255));
        }
        
        /// <summary>
        /// Draws a box with a title and optional content
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="title">The box title</param>
        /// <param name="content">The box content (optional)</param>
        /// <param name="width">The box width (optional, auto-determined if not specified)</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal DrawBox(this Components.Terminal terminal, string title, string? content = null, int width = 0)
        {
            // Determine box width based on content
            int titleLength = title.Length;
            int contentMaxLength = 0;
            
            if (content != null)
            {
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    contentMaxLength = Math.Max(contentMaxLength, line.Length);
                }
            }
            
            int boxWidth = width > 0 ? width : Math.Max(titleLength + 4, contentMaxLength + 4);
            
            // Draw top border with title
            terminal.Write("┌─");
            terminal.WriteColored(title, Color.White);
            terminal.Write("─");
            
            int remainingWidth = boxWidth - title.Length - 3;
            for (int i = 0; i < remainingWidth; i++)
            {
                terminal.Write("─");
            }
            terminal.WriteLine("┐");
            
            // Draw content if provided
            if (content != null)
            {
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    terminal.Write("│ ");
                    terminal.Write(line);
                    
                    // Pad to box width
                    int padding = boxWidth - line.Length - 2;
                    for (int i = 0; i < padding; i++)
                    {
                        terminal.Write(" ");
                    }
                    
                    terminal.WriteLine(" │");
                }
            }
            
            // Draw bottom border
            terminal.Write("└");
            for (int i = 0; i < boxWidth; i++)
            {
                terminal.Write("─");
            }
            terminal.WriteLine("┘");
            
            return terminal;
        }
        
        /// <summary>
        /// Draws a progress bar
        /// </summary>
        /// <param name="terminal">The terminal</param>
        /// <param name="progress">The progress value (0-100)</param>
        /// <param name="width">The width of the progress bar</param>
        /// <returns>The terminal instance for chaining</returns>
        public static Components.Terminal DrawProgressBar(this Components.Terminal terminal, int progress, int width = 40)
        {
            // Ensure progress is within bounds
            progress = Math.Clamp(progress, 0, 100);
            
            // Calculate filled portion
            int filledWidth = (int)Math.Round(progress * width / 100.0);
            
            // Build progress bar
            var sb = new StringBuilder();
            sb.Append('[');
            
            // Filled portion
            for (int i = 0; i < filledWidth; i++)
            {
                sb.Append('█');
            }
            
            // Empty portion
            for (int i = filledWidth; i < width; i++)
            {
                sb.Append(' ');
            }
            
            sb.Append(']');
            sb.Append($" {progress}%");
            
            terminal.WriteLine(sb.ToString());
            return terminal;
        }
    }
}
