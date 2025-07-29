// This file is kept for compatibility but should be replaced with LauncherModelExtensions.cs
// Implementation moved to HackerOs.OS.UI.LauncherModelExtensions

using HackerOs.OS.UI.Models;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Model representing an application category in the launcher
    /// </summary>
    public class AppCategoryModel
    {
        /// <summary>
        /// The category ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// The display name of the category
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// The path to the category icon
        /// </summary>
        public string IconPath { get; set; } = string.Empty;
        
        /// <summary>
        /// The applications in this category
        /// </summary>
        public List<LauncherAppModel> Applications { get; set; } = new List<LauncherAppModel>();
        
        /// <summary>
        /// Whether the category is expanded in the UI
        /// </summary>
        public bool IsExpanded { get; set; }
    }
}
