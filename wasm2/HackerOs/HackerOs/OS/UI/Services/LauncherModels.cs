using System;
using System.Collections.Generic;

namespace HackerOs.OS.UI.Services
{
    /// <summary>
    /// Represents a category of applications in the launcher.
    /// </summary>
    public class AppCategoryModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the category.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the category.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the category.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the icon for the category.
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the color scheme for the category.
        /// </summary>
        public string Color { get; set; } = "#333333";

        /// <summary>
        /// Gets or sets the sort order for the category.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets whether the category is visible in the launcher.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of applications in this category.
        /// </summary>
        public List<LauncherAppModel> Applications { get; set; } = new();

        /// <summary>
        /// Gets the number of applications in this category.
        /// </summary>
        public int ApplicationCount => Applications.Count;

        /// <summary>
        /// Initializes a new instance of the AppCategoryModel class.
        /// </summary>
        public AppCategoryModel() { }

        /// <summary>
        /// Initializes a new instance of the AppCategoryModel class with the specified parameters.
        /// </summary>
        public AppCategoryModel(string id, string name, string description = "", string icon = "", string color = "#333333")
        {
            Id = id;
            Name = name;
            Description = description;
            Icon = icon;
            Color = color;
        }
    }

    /// <summary>
    /// Represents an application model for the launcher.
    /// </summary>
    public class LauncherAppModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the application.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the application.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the application.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the icon for the application.
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category ID this application belongs to.
        /// </summary>
        public string CategoryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the component type name for the application.
        /// </summary>
        public string ComponentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the application.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the developer/author of the application.
        /// </summary>
        public string Developer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the application is installed.
        /// </summary>
        public bool IsInstalled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the application is currently running.
        /// </summary>
        public bool IsRunning { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the application is pinned to the launcher.
        /// </summary>
        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the application is visible in the launcher.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Gets or sets the sort order for the application within its category.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets the minimum window width for the application.
        /// </summary>
        public int MinWidth { get; set; } = 300;

        /// <summary>
        /// Gets or sets the minimum window height for the application.
        /// </summary>
        public int MinHeight { get; set; } = 200;

        /// <summary>
        /// Gets or sets the default window width for the application.
        /// </summary>
        public int DefaultWidth { get; set; } = 800;

        /// <summary>
        /// Gets or sets the default window height for the application.
        /// </summary>
        public int DefaultHeight { get; set; } = 600;

        /// <summary>
        /// Gets or sets additional metadata for the application.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the tags associated with the application for search and categorization.
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets the last time the application was launched.
        /// </summary>
        public DateTime? LastLaunched { get; set; }

        /// <summary>
        /// Gets or sets the number of times the application has been launched.
        /// </summary>
        public int LaunchCount { get; set; } = 0;

        /// <summary>
        /// Initializes a new instance of the LauncherAppModel class.
        /// </summary>
        public LauncherAppModel() { }

        /// <summary>
        /// Initializes a new instance of the LauncherAppModel class with the specified parameters.
        /// </summary>
        public LauncherAppModel(string id, string name, string componentType, string categoryId = "")
        {
            Id = id;
            Name = name;
            ComponentType = componentType;
            CategoryId = categoryId;
        }

        /// <summary>
        /// Increments the launch count and updates the last launched time.
        /// </summary>
        public void RecordLaunch()
        {
            LaunchCount++;
            LastLaunched = DateTime.UtcNow;
        }
    }
}
