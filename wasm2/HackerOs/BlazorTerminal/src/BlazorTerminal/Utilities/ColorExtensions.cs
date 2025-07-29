using System.Drawing;

namespace BlazorTerminal.Utilities
{
    /// <summary>
    /// Extension methods for Color handling
    /// </summary>
    public static class ColorExtensions
    {
        /// <summary>
        /// Converts a Color to an HTML/CSS compatible string
        /// </summary>
        /// <param name="color">The color to convert</param>
        /// <returns>A CSS color string (hex or rgba)</returns>
        public static string ToHtmlString(this Color color)
        {
            if (color.A < 255)
            {
                // Use rgba for transparent colors
                return $"rgba({color.R}, {color.G}, {color.B}, {color.A / 255.0:F2})";
            }
            
            // Use hex for opaque colors
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        
        /// <summary>
        /// Gets the ANSI 16-color palette color
        /// </summary>
        /// <param name="index">Index from 0-15</param>
        /// <returns>The corresponding color</returns>
        public static Color GetAnsiColor(int index)
        {
            // Standard 16-color ANSI palette
            switch (index)
            {
                case 0: return Color.FromArgb(0, 0, 0);       // Black
                case 1: return Color.FromArgb(205, 49, 49);   // Red
                case 2: return Color.FromArgb(13, 188, 121);  // Green
                case 3: return Color.FromArgb(229, 229, 16);  // Yellow
                case 4: return Color.FromArgb(36, 114, 200);  // Blue
                case 5: return Color.FromArgb(188, 63, 188);  // Magenta
                case 6: return Color.FromArgb(17, 168, 205);  // Cyan
                case 7: return Color.FromArgb(229, 229, 229); // White
                
                case 8: return Color.FromArgb(102, 102, 102);  // Bright Black
                case 9: return Color.FromArgb(241, 76, 76);    // Bright Red
                case 10: return Color.FromArgb(35, 209, 139);   // Bright Green
                case 11: return Color.FromArgb(245, 245, 67);   // Bright Yellow
                case 12: return Color.FromArgb(59, 142, 234);   // Bright Blue
                case 13: return Color.FromArgb(214, 112, 214);  // Bright Magenta
                case 14: return Color.FromArgb(41, 184, 219);   // Bright Cyan
                case 15: return Color.FromArgb(255, 255, 255);  // Bright White
                
                default: return Color.White; // Default to white for invalid indices
            }
        }
        
        /// <summary>
        /// Converts from a 0-255 color index to RGB color
        /// </summary>
        /// <param name="index">Color index (0-255)</param>
        /// <returns>The corresponding color</returns>
        public static Color From256ColorIndex(int index)
        {
            // Handle the standard 16 colors (indices 0-15)
            if (index < 16)
            {
                return GetAnsiColor(index);
            }
            
            // Handle the 6x6x6 color cube (indices 16-231)
            if (index >= 16 && index <= 231)
            {
                int adjustedIndex = index - 16;
                int r = adjustedIndex / 36;
                int g = (adjustedIndex % 36) / 6;
                int b = adjustedIndex % 6;
                
                // Convert to 0-255 range
                r = r > 0 ? 55 + r * 40 : 0;
                g = g > 0 ? 55 + g * 40 : 0;
                b = b > 0 ? 55 + b * 40 : 0;
                
                return Color.FromArgb(r, g, b);
            }
            
            // Handle the grayscale ramp (indices 232-255)
            if (index >= 232 && index <= 255)
            {
                int gray = (index - 232) * 10 + 8;
                return Color.FromArgb(gray, gray, gray);
            }
            
            // Default for invalid indices
            return Color.White;
        }
    }
}
