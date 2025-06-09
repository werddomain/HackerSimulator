using System;
using System.Collections.Generic;
using System.Linq;
using HackerOs.OS.Applications;

namespace HackerOs.OS.UI
{
    /// <summary>
    /// Extension methods for converting between launcher model types in different namespaces
    /// </summary>
    public static class LauncherModelExtensions
    {
        /// <summary>
        /// Converts a service model to a component model
        /// </summary>
        public static Components.LauncherAppModel ToComponentModel(this Services.LauncherAppModel model)
        {
            return new Components.LauncherAppModel
            {
                Id = model.Id,
                DisplayName = model.Name,
                IconPath = model.Icon,
                Description = model.Description,
                Category = model.CategoryId,
                Keywords = model.Tags ?? new List<string>(),
                LastLaunched = model.LastLaunched,
                IsPinned = model.IsPinned
            };
        }
        
        /// <summary>
        /// Converts a service category model to a component category model
        /// </summary>
        public static Components.AppCategoryModel ToComponentModel(this Services.AppCategoryModel model)
        {
            return new Components.AppCategoryModel
            {
                Id = model.Id,
                DisplayName = model.Name,
                IconPath = model.Icon,
                Applications = model.Applications?.Select(a => a.ToComponentModel()).ToList() ?? new List<Components.LauncherAppModel>(),
                IsExpanded = model.IsVisible
            };
        }
        
        /// <summary>
        /// Converts a component model to a service model
        /// </summary>
        public static Services.LauncherAppModel ToServiceModel(this Components.LauncherAppModel model)
        {
            return new Services.LauncherAppModel
            {
                Id = model.Id,
                Name = model.DisplayName,
                Icon = model.IconPath,
                Description = model.Description,
                CategoryId = model.Category,
                Tags = model.Keywords ?? new List<string>(),
                LastLaunched = model.LastLaunched,
                IsPinned = model.IsPinned
            };
        }
        
        /// <summary>
        /// Converts a component category model to a service category model
        /// </summary>
        public static Services.AppCategoryModel ToServiceModel(this Components.AppCategoryModel model)
        {
            return new Services.AppCategoryModel
            {
                Id = model.Id,
                Name = model.DisplayName,
                Icon = model.IconPath,
                Applications = model.Applications?.Select(a => a.ToServiceModel()).ToList() ?? new List<Services.LauncherAppModel>(),
                IsVisible = model.IsExpanded
            };
        }
        
        /// <summary>
        /// Converts an ApplicationManifest to a LauncherAppModel
        /// </summary>
        public static Services.LauncherAppModel ToLauncherAppModel(this ApplicationManifest app, bool isPinned = false)
        {
            return new Services.LauncherAppModel
            {
                Id = app.Id,
                Name = app.Name,
                Icon = app.IconPath ?? "/images/icons/default-app.png",
                Description = app.Description ?? string.Empty,
                CategoryId = app.Category ?? "Other",
                Tags = app.Keywords?.ToList() ?? new List<string>(),
                IsPinned = isPinned,
                ComponentType = app.ComponentTypeName,
                Version = app.Version ?? "1.0.0"
            };
        }
    }
}
