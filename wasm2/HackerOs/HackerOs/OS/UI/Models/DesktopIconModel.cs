using System;

namespace HackerOs.OS.UI.Models
{
    /// <summary>
    /// Model for desktop icons
    /// </summary>
    public class DesktopIconModel
    {
        /// <summary>
        /// The unique identifier for the icon
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The display name of the icon
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The path to the file or directory
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether the icon represents a directory
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// The path to the icon image
        /// </summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// The X position of the icon in the grid
        /// </summary>
        public int GridX { get; set; }

        /// <summary>
        /// The Y position of the icon in the grid
        /// </summary>
        public int GridY { get; set; }

        /// <summary>
        /// Whether the icon is selected
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Tooltip text for the icon
        /// </summary>
        public string Tooltip { get; set; } = string.Empty;
    }
}
