using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Applications;
using HackerOs.OS.Core.Settings;
using HackerOs.OS.UI.Models;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.UI.Services
{
    /// <summary>
    /// Service for managing desktop icons and shortcuts
    /// </summary>
    public class DesktopIconService
    {
        private readonly DesktopSettingsService _desktopSettings;
        private readonly IApplicationManager _applicationManager;
        private readonly ILogger<DesktopIconService> _logger;

        public DesktopIconService(
            DesktopSettingsService desktopSettings,
            IApplicationManager applicationManager,
            ILogger<DesktopIconService> logger)
        {
            _desktopSettings = desktopSettings ?? throw new ArgumentNullException(nameof(desktopSettings));
            _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all desktop icons
        /// </summary>
        public async Task<List<DesktopIcon>> GetAllIconsAsync()
        {
            return await _desktopSettings.GetDesktopIconsAsync();
        }

        /// <summary>
        /// Add a new icon to the desktop
        /// </summary>
        public async Task AddIconAsync(DesktopIcon icon)
        {
            var icons = await _desktopSettings.GetDesktopIconsAsync();
            
            // Find an empty position for the icon if not specified
            if (icon.GridX == 0 && icon.GridY == 0)
            {
                FindEmptyPosition(icons, out int gridX, out int gridY);
                icon.GridX = gridX;
                icon.GridY = gridY;
            }
            
            icons.Add(icon);
            await _desktopSettings.SaveDesktopIconsAsync(icons);
        }

        /// <summary>
        /// Remove an icon from the desktop
        /// </summary>
        public async Task RemoveIconAsync(Guid iconId)
        {
            var icons = await _desktopSettings.GetDesktopIconsAsync();
            var icon = icons.FirstOrDefault(i => i.Id == iconId);
            
            if (icon != null)
            {
                icons.Remove(icon);
                await _desktopSettings.SaveDesktopIconsAsync(icons);
            }
        }

        /// <summary>
        /// Update an icon's position
        /// </summary>
        public async Task UpdateIconPositionAsync(Guid iconId, int gridX, int gridY)
        {
            var icons = await _desktopSettings.GetDesktopIconsAsync();
            var icon = icons.FirstOrDefault(i => i.Id == iconId);
            
            if (icon != null)
            {
                icon.GridX = gridX;
                icon.GridY = gridY;
                await _desktopSettings.SaveDesktopIconsAsync(icons);
            }
        }        /// <summary>
        /// Create an application shortcut on the desktop
        /// </summary>
        public async Task CreateApplicationShortcutAsync(string applicationId, string? displayName = null)
        {
            try
            {
                var app = _applicationManager.GetApplication(applicationId);
                if (app == null)
                {
                    _logger.LogWarning($"Cannot create shortcut for unknown application: {applicationId}");
                    return;
                }
                
                var iconPath = app.IconPath ?? "/images/icons/default-app.png";
                
                var icon = DesktopIcon.CreateApplicationIcon(
                    applicationId,
                    displayName ?? app.Name,
                    iconPath);
                
                await AddIconAsync(icon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating application shortcut for {applicationId}");
            }
        }

        /// <summary>
        /// Create a file shortcut on the desktop
        /// </summary>
        public async Task CreateFileShortcutAsync(string filePath, string displayName, string iconPath)
        {
            try
            {
                var icon = DesktopIcon.CreateFileIcon(
                    filePath,
                    displayName,
                    iconPath);
                
                await AddIconAsync(icon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating file shortcut for {filePath}");
            }
        }

        /// <summary>
        /// Arrange desktop icons in a grid pattern
        /// </summary>
        public async Task ArrangeIconsAsync(bool sortByName = true)
        {
            var icons = await _desktopSettings.GetDesktopIconsAsync();
            
            // Sort icons if requested
            if (sortByName)
            {
                icons = icons.OrderBy(i => i.DisplayName).ToList();
            }
            
            // Get grid size
            var gridSize = await _desktopSettings.GetIconGridCellSizeAsync();
            
            // Calculate max icons per row based on screen width (assuming 1920px width)
            int screenWidth = 1920;
            int maxIconsPerRow = screenWidth / gridSize.Width;
            
            // Arrange icons in grid
            for (int i = 0; i < icons.Count; i++)
            {
                icons[i].GridX = i % maxIconsPerRow;
                icons[i].GridY = i / maxIconsPerRow;
            }
            
            await _desktopSettings.SaveDesktopIconsAsync(icons);
        }

        /// <summary>
        /// Find an empty position for a new icon
        /// </summary>
        private void FindEmptyPosition(List<DesktopIcon> icons, out int gridX, out int gridY)
        {
            // Default to position 0,0
            gridX = 0;
            gridY = 0;
            
            // If there are no icons, return the default position
            if (!icons.Any())
            {
                return;
            }
            
            // Get the maximum grid positions
            int maxGridX = icons.Max(i => i.GridX);
            int maxGridY = icons.Max(i => i.GridY);
            
            // Create a grid of occupied positions
            var occupiedPositions = new HashSet<(int, int)>();
            foreach (var icon in icons)
            {
                occupiedPositions.Add((icon.GridX, icon.GridY));
            }
            
            // Find an empty position by scanning the grid
            for (int y = 0; y <= maxGridY + 1; y++)
            {
                for (int x = 0; x <= maxGridX + 1; x++)
                {
                    if (!occupiedPositions.Contains((x, y)))
                    {
                        gridX = x;
                        gridY = y;
                        return;
                    }
                }
            }
        }
    }
}
