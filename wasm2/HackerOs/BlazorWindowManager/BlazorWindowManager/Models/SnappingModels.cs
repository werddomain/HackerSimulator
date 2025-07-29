using System;
using BlazorWindowManager.Services;

namespace BlazorWindowManager.Models
{
    /// <summary>
    /// Information about a snap preview being shown
    /// </summary>
    public class SnapPreviewInfo
    {
        /// <summary>
        /// Type of snap being previewed
        /// </summary>
        public SnapType SnapType { get; set; }

        /// <summary>
        /// Target bounds where the window would be placed
        /// </summary>
        public WindowBounds TargetBounds { get; set; } = new();

        /// <summary>
        /// Description of the snap operation
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether this preview is currently active
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Result of a snap operation being applied
    /// </summary>
    public class SnapResult
    {
        /// <summary>
        /// Type of snap that was applied
        /// </summary>
        public SnapType SnapType { get; set; }

        /// <summary>
        /// ID of the window that was snapped
        /// </summary>
        public Guid WindowId { get; set; }

        /// <summary>
        /// Original bounds before snapping
        /// </summary>
        public WindowBounds OriginalBounds { get; set; } = new();

        /// <summary>
        /// New bounds after snapping
        /// </summary>
        public WindowBounds NewBounds { get; set; } = new();

        /// <summary>
        /// When the snap was applied
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Description of the snap operation
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether the snap was successful
        /// </summary>
        public bool Success { get; set; } = true;
    }
}
