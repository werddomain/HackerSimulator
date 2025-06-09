using HackerOs.OS.Applications;
using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Application launcher model that represents an application in the launcher
    /// </summary>
    public class LauncherAppModel
    {
        /// <summary>
        /// The application ID
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// The display name of the application
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// The path to the application icon
        /// </summary>
        public string IconPath { get; set; }
        
        /// <summary>
        /// The application description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// The category of the application
        /// </summary>
        public string Category { get; set; }
        
        /// <summary>
        /// Keywords for search
        /// </summary>
        public List<string> Keywords { get; set; } = new List<string>();
        
        /// <summary>
        /// The timestamp when the application was last launched
        /// </summary>
        public DateTime? LastLaunched { get; set; }
        
        /// <summary>
        /// Whether the application is pinned to the quick launch area
        /// </summary>
        public bool IsPinned { get; set; }
    }
    
    // AppCategoryModel moved to a separate file
}
