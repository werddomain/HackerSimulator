using HackerOs.OS.Theme;
using HackerOs.OS.UI;
using HackerOs.OS.Applications;
using HackerOs.OS.Core.Settings;
using HackerOs.OS.User;
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
    /// Main desktop component for HackerOS
    /// </summary>
    public partial class Desktop
    {
        [Inject] protected IThemeManager ThemeManager { get; set; } = null!;
        [Inject] protected IApplicationManager ApplicationManager { get; set; } = null!;
        [Inject] protected ApplicationWindowManager WindowManager { get; set; } = null!;
        [Inject] protected ISettingsService SettingsService { get; set; } = null!;
        [Inject] protected IUserManager UserManager { get; set; } = null!;
        [Inject] protected ILogger<Desktop> Logger { get; set; } = null!;
        [Inject] protected DesktopSettingsService DesktopSettings { get; set; } = null!;
        [Inject] protected NotificationService NotificationService { get; set; } = null!;

        /// <summary>
        /// Reference to the application launcher component
        /// </summary>
        private ApplicationLauncher? _applicationLauncher;

        /// <summary>
        /// Reference to the notification center component
        /// </summary>
        private NotificationCenter? _notificationCenter;

        /// <summary>
        /// The current user session
        /// </summary>
        [Parameter] public UserSession? UserSession { get; set; }

        /// <summary>
        /// Desktop background image path
        /// </summary>
        protected string BackgroundImagePath { get; set; } = "/images/desktop/default-background.jpg";

        /// <summary>
        /// Desktop icons
        /// </summary>
        protected List<DesktopIcon> Icons { get; set; } = new();

        /// <summary>
        /// Whether the context menu is visible
        /// </summary>
        protected bool IsContextMenuVisible { get; set; }

        /// <summary>
        /// Context menu position
        /// </summary>
        protected int ContextMenuX { get; set; }
        protected int ContextMenuY { get; set; }

        /// <summary>
        /// Context menu items
        /// </summary>
        protected List<ContextMenuItem> ContextMenuItems { get; set; } = new();

        /// <summary>
        /// The icon that was right-clicked
        /// </summary>
        protected DesktopIcon? ContextMenuIcon { get; set; }

        /// <summary>
        /// Selection rectangle
        /// </summary>
        protected Rectangle SelectionRect { get; set; } = new Rectangle();
        protected bool IsSelectionRectVisible { get; set; }

        /// <summary>
        /// Mouse tracking for selection rectangle and dragging
        /// </summary>
        protected int MouseStartX { get; set; }
        protected int MouseStartY { get; set; }
        protected int MouseCurrentX { get; set; }
        protected int MouseCurrentY { get; set; }
        protected bool IsMouseDown { get; set; }
        protected bool IsDragging { get; set; }
        protected bool IsSelecting { get; set; }

        /// <summary>
        /// Grid cell size for desktop icons
        /// </summary>
        protected int GridCellWidth { get; set; } = 80;
        protected int GridCellHeight { get; set; } = 90;

        /// <summary>
        /// Component initialization
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            
            // Load desktop settings
            await LoadDesktopSettingsAsync();
            
            // Load desktop icons
            await LoadDesktopIconsAsync();
            
            // Subscribe to theme changes
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        /// <summary>
        /// Clean up when component is disposed
        /// </summary>
        public void Dispose()
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        /// <summary>
        /// Load desktop settings
        /// </summary>
        private async Task LoadDesktopSettingsAsync()
        {
            try
            {
                // Load background image
                BackgroundImagePath = await DesktopSettings.GetBackgroundImagePathAsync();
                
                // Load grid cell size
                var gridSize = await DesktopSettings.GetIconGridCellSizeAsync();
                GridCellWidth = gridSize.Width;
                GridCellHeight = gridSize.Height;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading desktop settings");
            }
        }

        /// <summary>
        /// Load desktop icons
        /// </summary>
        private async Task LoadDesktopIconsAsync()
        {
            try
            {
                Icons = await DesktopSettings.GetDesktopIconsAsync();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading desktop icons");
            }
        }

        /// <summary>
        /// Save desktop icons
        /// </summary>
        private async Task SaveDesktopIconsAsync()
        {
            try
            {
                await DesktopSettings.SaveDesktopIconsAsync(Icons);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving desktop icons");
            }
        }

        /// <summary>
        /// Handle theme changes
        /// </summary>
        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            StateHasChanged();
        }

        #region Desktop Event Handlers

        /// <summary>
        /// Handle desktop click
        /// </summary>
        protected void OnDesktopClick(MouseEventArgs e)
        {
            // Clear icon selection when clicking on the desktop (unless selecting with rectangle)
            if (!IsSelecting && !IsDragging)
            {
                ClearIconSelection();
                IsContextMenuVisible = false;
            }
        }

        /// <summary>
        /// Handle desktop mouse down
        /// </summary>
        protected void OnDesktopMouseDown(MouseEventArgs e)
        {
            if (e.Button == 0) // Left mouse button
            {
                IsMouseDown = true;
                IsSelecting = true;
                MouseStartX = (int)e.ClientX;
                MouseStartY = (int)e.ClientY;
                
                // Initialize selection rectangle
                SelectionRect = new Rectangle(MouseStartX, MouseStartY, 0, 0);
                IsSelectionRectVisible = false;
            }
        }

        /// <summary>
        /// Handle desktop mouse move
        /// </summary>
        protected void OnDesktopMouseMove(MouseEventArgs e)
        {
            MouseCurrentX = (int)e.ClientX;
            MouseCurrentY = (int)e.ClientY;
            
            if (IsMouseDown)
            {
                if (IsSelecting)
                {
                    // Update selection rectangle
                    UpdateSelectionRectangle();
                    IsSelectionRectVisible = true;
                    
                    // Select icons inside the rectangle
                    SelectIconsInRectangle();
                }
                else if (IsDragging)
                {
                    // Handle icon dragging
                    DragSelectedIcons();
                }
            }
        }

        /// <summary>
        /// Handle desktop mouse up
        /// </summary>
        protected void OnDesktopMouseUp(MouseEventArgs e)
        {
            if (e.Button == 0) // Left mouse button
            {
                if (IsDragging)
                {
                    // Complete drag operation
                    FinalizeDragOperation();
                }
                
                IsMouseDown = false;
                IsSelecting = false;
                IsDragging = false;
                IsSelectionRectVisible = false;
                
                // Save desktop icons if positions changed
                _ = SaveDesktopIconsAsync();
            }
        }

        /// <summary>
        /// Handle desktop context menu
        /// </summary>
        protected void OnDesktopContextMenu(MouseEventArgs e)
        {
            ContextMenuX = (int)e.ClientX;
            ContextMenuY = (int)e.ClientY;
            
            // Clear current icon context
            ContextMenuIcon = null;
            
            // Create desktop context menu
            CreateDesktopContextMenu();
            
            IsContextMenuVisible = true;
            StateHasChanged();
        }

        #endregion

        #region Icon Event Handlers

        /// <summary>
        /// Handle icon click
        /// </summary>
        protected void OnIconClick(MouseEventArgs e, DesktopIcon icon)
        {
            if (e.CtrlKey || e.ShiftKey)
            {
                // Toggle selection with Ctrl key
                icon.IsSelected = !icon.IsSelected;
            }
            else
            {
                // Select only this icon
                ClearIconSelection();
                icon.IsSelected = true;
            }
            
            // Hide context menu
            IsContextMenuVisible = false;
            
            // Stop event propagation
            e.StopPropagation();
            
            StateHasChanged();
        }

        /// <summary>
        /// Handle icon double click
        /// </summary>
        protected async Task OnIconDoubleClick(MouseEventArgs e, DesktopIcon icon)
        {
            // Launch the application or open the file
            await LaunchIconTarget(icon);
            
            // Stop event propagation
            e.StopPropagation();
        }

        /// <summary>
        /// Handle icon mouse down
        /// </summary>
        protected void OnIconMouseDown(MouseEventArgs e, DesktopIcon icon)
        {
            if (e.Button == 0) // Left mouse button
            {
                IsMouseDown = true;
                
                // Start position for potential drag
                MouseStartX = (int)e.ClientX;
                MouseStartY = (int)e.ClientY;
                
                // If icon isn't already selected, select it
                if (!icon.IsSelected)
                {
                    if (!e.CtrlKey && !e.ShiftKey)
                    {
                        ClearIconSelection();
                    }
                    
                    icon.IsSelected = true;
                }
                
                // Prepare for dragging instead of selection
                IsSelecting = false;
                IsDragging = true;
            }
            
            // Stop event propagation
            e.StopPropagation();
        }

        /// <summary>
        /// Handle icon context menu
        /// </summary>
        protected void OnIconContextMenu(MouseEventArgs e, DesktopIcon icon)
        {
            ContextMenuX = (int)e.ClientX;
            ContextMenuY = (int)e.ClientY;
            
            // Set current context menu icon
            ContextMenuIcon = icon;
            
            // If icon isn't selected, select only it
            if (!icon.IsSelected)
            {
                ClearIconSelection();
                icon.IsSelected = true;
            }
            
            // Create icon context menu
            CreateIconContextMenu();
            
            IsContextMenuVisible = true;
            StateHasChanged();
            
            // Stop event propagation
            e.StopPropagation();
        }

        #endregion

        #region Context Menu

        /// <summary>
        /// Create desktop context menu items
        /// </summary>
        private void CreateDesktopContextMenu()
        {
            ContextMenuItems = new List<ContextMenuItem>
            {
                new ContextMenuItem { Id = "view", Label = "View", IconClass = "icon-view" },
                new ContextMenuItem { Id = "sort", Label = "Sort By", IconClass = "icon-sort" },
                new ContextMenuItem { Id = "refresh", Label = "Refresh", IconClass = "icon-refresh" },
                new ContextMenuItem { IsSeparator = true },
                new ContextMenuItem { Id = "newFolder", Label = "New Folder", IconClass = "icon-folder" },
                new ContextMenuItem { Id = "newFile", Label = "New File", IconClass = "icon-file" },
                new ContextMenuItem { IsSeparator = true },
                new ContextMenuItem { Id = "properties", Label = "Properties", IconClass = "icon-properties" }
            };
        }

        /// <summary>
        /// Create icon context menu items
        /// </summary>
        private void CreateIconContextMenu()
        {
            ContextMenuItems = new List<ContextMenuItem>
            {
                new ContextMenuItem { Id = "open", Label = "Open", IconClass = "icon-open" },
                new ContextMenuItem { Id = "runAs", Label = "Run as Administrator", IconClass = "icon-shield" },
                new ContextMenuItem { IsSeparator = true },
                new ContextMenuItem { Id = "cut", Label = "Cut", IconClass = "icon-cut" },
                new ContextMenuItem { Id = "copy", Label = "Copy", IconClass = "icon-copy" },
                new ContextMenuItem { IsSeparator = true },
                new ContextMenuItem { Id = "delete", Label = "Delete", IconClass = "icon-delete" },
                new ContextMenuItem { Id = "rename", Label = "Rename", IconClass = "icon-rename" },
                new ContextMenuItem { IsSeparator = true },
                new ContextMenuItem { Id = "properties", Label = "Properties", IconClass = "icon-properties" }
            };
        }

        /// <summary>
        /// Handle context menu item click
        /// </summary>
        protected async Task OnContextMenuItemClick(ContextMenuItem item)
        {
            IsContextMenuVisible = false;
            
            switch (item.Id)
            {
                case "view":
                    // Handle view menu item
                    break;
                    
                case "sort":
                    // Handle sort menu item
                    await ArrangeIconsAsync();
                    break;
                    
                case "refresh":
                    // Handle refresh menu item
                    await LoadDesktopIconsAsync();
                    break;
                    
                case "newFolder":
                    // Handle new folder menu item
                    await CreateNewFolderAsync();
                    break;
                    
                case "newFile":
                    // Handle new file menu item
                    await CreateNewFileAsync();
                    break;
                    
                case "open":
                    // Handle open menu item
                    if (ContextMenuIcon != null)
                    {
                        await LaunchIconTarget(ContextMenuIcon);
                    }
                    break;
                    
                case "delete":
                    // Handle delete menu item
                    await DeleteSelectedIconsAsync();
                    break;
                    
                case "properties":
                    // Handle properties menu item
                    if (ContextMenuIcon != null)
                    {
                        await ShowIconPropertiesAsync(ContextMenuIcon);
                    }
                    else
                    {
                        await ShowDesktopPropertiesAsync();
                    }
                    break;
            }
            
            StateHasChanged();
        }

        #endregion

        #region Desktop Operations

        /// <summary>
        /// Update selection rectangle based on mouse position
        /// </summary>
        private void UpdateSelectionRectangle()
        {
            int left = Math.Min(MouseStartX, MouseCurrentX);
            int top = Math.Min(MouseStartY, MouseCurrentY);
            int width = Math.Abs(MouseCurrentX - MouseStartX);
            int height = Math.Abs(MouseCurrentY - MouseStartY);
            
            SelectionRect = new Rectangle(left, top, width, height);
        }

        /// <summary>
        /// Select icons that are inside the selection rectangle
        /// </summary>
        private void SelectIconsInRectangle()
        {
            // Clear selection if not using Ctrl or Shift
            if (!IsKeyModifierPressed)
            {
                ClearIconSelection();
            }
            
            // Find icons that intersect with the selection rectangle
            foreach (var icon in Icons)
            {
                // Calculate icon's position and bounds
                var iconRect = GetIconBounds(icon);
                
                // Check if icon intersects with selection rectangle
                if (SelectionRect.IntersectsWith(iconRect))
                {
                    icon.IsSelected = true;
                }
            }
            
            StateHasChanged();
        }

        /// <summary>
        /// Get the rectangle bounds of an icon on the desktop
        /// </summary>
        private Rectangle GetIconBounds(DesktopIcon icon)
        {
            // Calculate position based on grid coordinates
            int left = icon.GridX * GridCellWidth;
            int top = icon.GridY * GridCellHeight;
            
            return new Rectangle(left, top, GridCellWidth, GridCellHeight);
        }

        /// <summary>
        /// Clear selection from all icons
        /// </summary>
        private void ClearIconSelection()
        {
            foreach (var icon in Icons)
            {
                icon.IsSelected = false;
            }
        }

        /// <summary>
        /// Get selected icons
        /// </summary>
        private IEnumerable<DesktopIcon> GetSelectedIcons()
        {
            return Icons.Where(i => i.IsSelected);
        }

        /// <summary>
        /// Drag selected icons
        /// </summary>
        private void DragSelectedIcons()
        {
            // Calculate drag delta
            int deltaX = MouseCurrentX - MouseStartX;
            int deltaY = MouseCurrentY - MouseStartY;
            
            // Determine if we've moved enough to start dragging
            if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
            {
                IsDragging = true;
            }
            
            // Update UI for dragging state
            StateHasChanged();
        }

        /// <summary>
        /// Finalize drag operation by updating icon positions
        /// </summary>
        private void FinalizeDragOperation()
        {
            if (!IsDragging) return;
            
            // Calculate drag delta in grid cells
            int deltaGridX = (MouseCurrentX - MouseStartX) / GridCellWidth;
            int deltaGridY = (MouseCurrentY - MouseStartY) / GridCellHeight;
            
            // Skip if no movement in grid
            if (deltaGridX == 0 && deltaGridY == 0) return;
            
            // Update positions of selected icons
            foreach (var icon in GetSelectedIcons())
            {
                int newGridX = icon.GridX + deltaGridX;
                int newGridY = icon.GridY + deltaGridY;
                
                // Ensure we don't go out of bounds (minimum 0)
                icon.GridX = Math.Max(0, newGridX);
                icon.GridY = Math.Max(0, newGridY);
            }
        }

        /// <summary>
        /// Launch the application or open the file associated with the icon
        /// </summary>
        private async Task LaunchIconTarget(DesktopIcon icon)
        {
            try
            {
                if (icon.IsApplication)
                {
                    // Launch application
                    var app = ApplicationManager.GetApplication(icon.Target);
                    if (app != null)
                    {
                        var session = UserSession ?? new UserSession(UserManager.SystemUser, "system");
                        var context = ApplicationLaunchContext.Create(session);
                        await ApplicationManager.LaunchApplicationAsync(app.Id, context);
                    }
                    else
                    {
                        Logger.LogWarning($"Application not found: {icon.Target}");
                    }
                }
                else
                {
                    // Open file
                    Logger.LogInformation($"Opening file: {icon.Target}");
                    // TODO: Implement file opening logic
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error launching icon target: {icon.Target}");
            }
        }

        /// <summary>
        /// Arrange icons in a grid
        /// </summary>
        protected async Task ArrangeIconsAsync()
        {
            // Sort icons by name
            var sortedIcons = Icons.OrderBy(i => i.DisplayName).ToList();
            
            // Arrange in grid pattern
            int maxIconsPerRow = 8; // Adjust based on screen width
            
            for (int i = 0; i < sortedIcons.Count; i++)
            {
                sortedIcons[i].GridX = i % maxIconsPerRow;
                sortedIcons[i].GridY = i / maxIconsPerRow;
            }
            
            // Save arrangement
            Icons = sortedIcons;
            await SaveDesktopIconsAsync();
            
            StateHasChanged();
        }

        /// <summary>
        /// Create a new folder on the desktop
        /// </summary>
        private async Task CreateNewFolderAsync()
        {
            // TODO: Implement folder creation
            Logger.LogInformation("Creating new folder");
        }

        /// <summary>
        /// Create a new file on the desktop
        /// </summary>
        private async Task CreateNewFileAsync()
        {
            // TODO: Implement file creation
            Logger.LogInformation("Creating new file");
        }

        /// <summary>
        /// Delete selected desktop icons
        /// </summary>
        protected async Task DeleteSelectedIconsAsync()
        {
            var selectedIcons = GetSelectedIcons().ToList();
            if (selectedIcons.Any())
            {
                // Remove selected icons
                foreach (var icon in selectedIcons)
                {
                    Icons.Remove(icon);
                }
                
                // Save changes
                await SaveDesktopIconsAsync();
                
                StateHasChanged();
            }
        }

        /// <summary>
        /// Show properties for a desktop icon
        /// </summary>
        private async Task ShowIconPropertiesAsync(DesktopIcon icon)
        {
            // TODO: Implement icon properties dialog
            Logger.LogInformation($"Showing properties for icon: {icon.DisplayName}");
        }

        /// <summary>
        /// Show desktop properties
        /// </summary>
        private async Task ShowDesktopPropertiesAsync()
        {
            // TODO: Implement desktop properties dialog
            Logger.LogInformation("Showing desktop properties");
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Represents a rectangle
        /// </summary>
        protected class Rectangle
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            
            public int Right => Left + Width;
            public int Bottom => Top + Height;
            
            public Rectangle()
            {
            }
            
            public Rectangle(int left, int top, int width, int height)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }
            
            public bool IntersectsWith(Rectangle other)
            {
                return Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
            }
        }

        /// <summary>
        /// Context menu item
        /// </summary>
        protected class ContextMenuItem
        {
            public string Id { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string IconClass { get; set; } = string.Empty;
            public bool IsSeparator { get; set; }
        }

        /// <summary>        /// <summary>        /// <summary>
        /// Check if any modifier key is pressed
        /// </summary>
        private bool IsKeyModifierPressed { get; set; }

        #endregion
    }
}


