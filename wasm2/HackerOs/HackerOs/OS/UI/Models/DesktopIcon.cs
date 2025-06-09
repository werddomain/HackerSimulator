using System;
using System.Text.Json.Serialization;

namespace HackerOs.OS.UI.Models
{
    /// <summary>
    /// Represents an icon on the desktop that can launch applications or open files
    /// </summary>
    public class DesktopIcon
    {
        /// <summary>
        /// Unique identifier for the desktop icon
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Display name of the icon shown to the user
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Path to the icon image
        /// </summary>
        public string IconPath { get; set; }

        /// <summary>
        /// The application ID or file path that this icon opens
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Determines if this icon launches an application (true) or opens a file (false)
        /// </summary>
        public bool IsApplication { get; set; }

        /// <summary>
        /// X position on the desktop grid
        /// </summary>
        public int GridX { get; set; }

        /// <summary>
        /// Y position on the desktop grid
        /// </summary>
        public int GridY { get; set; }

        /// <summary>
        /// Whether the icon is currently selected
        /// </summary>
        [JsonIgnore]
        public bool IsSelected { get; set; }

        /// <summary>
        /// Optional tooltip text shown when hovering over the icon
        /// </summary>
        public string Tooltip { get; set; }

        /// <summary>
        /// Timestamp when the icon was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Optional parameters to pass when launching the application or opening the file
        /// </summary>
        public string LaunchParameters { get; set; }

        /// <summary>
        /// Creates a new desktop icon for an application
        /// </summary>
        public static DesktopIcon CreateApplicationIcon(string applicationId, string displayName, string iconPath, int gridX = 0, int gridY = 0)
        {
            return new DesktopIcon
            {
                DisplayName = displayName,
                IconPath = iconPath,
                Target = applicationId,
                IsApplication = true,
                GridX = gridX,
                GridY = gridY
            };
        }

        /// <summary>
        /// Creates a new desktop icon for a file
        /// </summary>
        public static DesktopIcon CreateFileIcon(string filePath, string displayName, string iconPath, int gridX = 0, int gridY = 0)
        {
            return new DesktopIcon
            {
                DisplayName = displayName,
                IconPath = iconPath,
                Target = filePath,
                IsApplication = false,
                GridX = gridX,
                GridY = gridY
            };
        }
    }
}
