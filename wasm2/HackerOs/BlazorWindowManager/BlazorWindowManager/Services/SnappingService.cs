using BlazorWindowManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorWindowManager.Services
{
    /// <summary>
    /// Configuration for window snapping behavior
    /// </summary>
    public class SnappingConfiguration
    {
        /// <summary>
        /// Whether window snapping is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Snap sensitivity in pixels (how close a window needs to be to snap)
        /// </summary>
        public int SnapSensitivity { get; set; } = 20;

        /// <summary>
        /// Whether to enable edge snapping (to screen boundaries)
        /// </summary>
        public bool EnableEdgeSnapping { get; set; } = true;

        /// <summary>
        /// Whether to enable window-to-window snapping
        /// </summary>
        public bool EnableWindowSnapping { get; set; } = true;

        /// <summary>
        /// Whether to enable snap zones (drag to edge for half-screen, etc.)
        /// </summary>
        public bool EnableSnapZones { get; set; } = true;

        /// <summary>
        /// Whether to show visual feedback during snapping operations
        /// </summary>
        public bool ShowSnapPreview { get; set; } = true;
    }    /// <summary>
    /// Represents a snap target location
    /// </summary>
    public class SnapTarget
    {
        /// <summary>
        /// Type of snapping operation
        /// </summary>
        public SnapType Type { get; set; }
        
        /// <summary>
        /// Target bounds where the window would be positioned
        /// </summary>
        public WindowBounds TargetBounds { get; set; } = new();
        
        /// <summary>
        /// Description of the snap operation
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Distance from current position to snap target
        /// </summary>
        public double Distance { get; set; }
    }

    /// <summary>
    /// Types of snapping operations
    /// </summary>
    public enum SnapType
    {
        None,
        LeftEdge,
        RightEdge,
        TopEdge,
        BottomEdge,
        LeftHalf,
        RightHalf,
        TopHalf,
        BottomHalf,
        Maximize,
        WindowLeft,
        WindowRight,
        WindowTop,
        WindowBottom
    }

    /// <summary>
    /// Service responsible for managing window snapping behavior
    /// </summary>
    public class SnappingService
    {        private readonly WindowManagerService _windowManager;
        private readonly SnappingConfiguration _configuration;
        
        /// <summary>
        /// Event raised when a snap preview should be shown
        /// </summary>
        public event Action<SnapTarget?>? SnapPreviewChanged;

        /// <summary>
        /// Event raised when snap preview changes (compatible interface for demo)
        /// </summary>
        public event Action<SnapPreviewInfo?>? OnSnapPreviewChanged;

        /// <summary>
        /// Event raised when a snap is applied to a window
        /// </summary>
        public event Action<SnapResult>? OnSnapApplied;

        /// <summary>
        /// Initializes a new instance of the SnappingService
        /// </summary>
        /// <param name="windowManager">The window manager service</param>
        public SnappingService(WindowManagerService windowManager)
        {
            _windowManager = windowManager;
            _configuration = new SnappingConfiguration();
        }        /// <summary>
        /// Gets the current snapping configuration
        /// </summary>
        public SnappingConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets or sets whether snapping is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _configuration.IsEnabled;
            set => _configuration.IsEnabled = value;
        }

        /// <summary>
        /// Gets or sets the edge sensitivity for snapping
        /// </summary>
        public int EdgeSensitivity
        {
            get => _configuration.SnapSensitivity;
            set => _configuration.SnapSensitivity = value;
        }

        /// <summary>
        /// Gets or sets the zone sensitivity for snapping
        /// </summary>
        public int ZoneSensitivity
        {
            get => _configuration.SnapSensitivity; // Using same value for now
            set => _configuration.SnapSensitivity = value;
        }

        /// <summary>
        /// Gets or sets whether to show snap preview
        /// </summary>
        public bool ShowSnapPreview
        {
            get => _configuration.ShowSnapPreview;
            set => _configuration.ShowSnapPreview = value;
        }

        /// <summary>
        /// Calculates the best snap target for a window being moved
        /// </summary>
        /// <param name="windowId">The ID of the window being moved</param>
        /// <param name="currentBounds">The current bounds of the window</param>
        /// <param name="containerBounds">The bounds of the container (desktop area)</param>
        /// <returns>The best snap target, or null if no snapping should occur</returns>
        public SnapTarget? CalculateSnapTarget(Guid windowId, WindowBounds currentBounds, WindowBounds containerBounds)
        {
            if (!_configuration.IsEnabled)
                return null;

            var snapTargets = new List<SnapTarget>();

            // Add edge snap targets
            if (_configuration.EnableEdgeSnapping)
            {
                snapTargets.AddRange(GetEdgeSnapTargets(currentBounds, containerBounds));
            }

            // Add snap zone targets
            if (_configuration.EnableSnapZones)
            {
                snapTargets.AddRange(GetSnapZoneTargets(currentBounds, containerBounds));
            }

            // Add window-to-window snap targets
            if (_configuration.EnableWindowSnapping)
            {
                snapTargets.AddRange(GetWindowSnapTargets(windowId, currentBounds));
            }

            // Find the closest valid snap target
            var bestTarget = snapTargets
                .Where(target => target.Distance <= _configuration.SnapSensitivity)
                .OrderBy(target => target.Distance)
                .FirstOrDefault();            return bestTarget;
        }

        /// <summary>
        /// Updates the snap preview display
        /// </summary>
        public void UpdateSnapPreview(SnapTarget? target)
        {
            if (_configuration.ShowSnapPreview)
            {
                SnapPreviewChanged?.Invoke(target);
                
                // Also trigger the compatible event for demo
                var previewInfo = target != null ? new SnapPreviewInfo
                {
                    SnapType = target.Type,
                    TargetBounds = target.TargetBounds,
                    Description = target.Description,
                    IsActive = true
                } : null;
                
                OnSnapPreviewChanged?.Invoke(previewInfo);
            }
        }

        /// <summary>
        /// Applies a snap result and triggers the event
        /// </summary>
        /// <param name="windowId">ID of the window being snapped</param>
        /// <param name="originalBounds">Original bounds before snap</param>
        /// <param name="target">Snap target being applied</param>
        public void ApplySnapResult(Guid windowId, WindowBounds originalBounds, SnapTarget target)
        {
            var snapResult = new SnapResult
            {
                WindowId = windowId,
                SnapType = target.Type,
                OriginalBounds = originalBounds,
                NewBounds = target.TargetBounds,
                Description = target.Description,
                Success = true
            };
            
            OnSnapApplied?.Invoke(snapResult);
        }        /// <summary>
        /// Hides the snap preview
        /// </summary>
        public void HideSnapPreview()
        {
            UpdateSnapPreview(null);
        }

        /// <summary>
        /// Applies snapping to a window position
        /// </summary>
        /// <param name="windowId">The window to snap</param>
        /// <param name="currentBounds">Current window bounds</param>
        /// <param name="containerBounds">Container bounds</param>
        /// <returns>The snapped bounds, or original bounds if no snapping</returns>
        public WindowBounds ApplySnapping(Guid windowId, WindowBounds currentBounds, WindowBounds containerBounds)
        {
            var snapTarget = CalculateSnapTarget(windowId, currentBounds, containerBounds);
            
            if (snapTarget != null)
            {
                return snapTarget.TargetBounds;
            }

            return currentBounds;
        }

        /// <summary>
        /// Gets edge snapping targets (window edges to container edges)
        /// </summary>
        private List<SnapTarget> GetEdgeSnapTargets(WindowBounds windowBounds, WindowBounds containerBounds)
        {
            var targets = new List<SnapTarget>();

            // Left edge
            var leftDistance = Math.Abs(windowBounds.Left - containerBounds.Left);
            if (leftDistance <= _configuration.SnapSensitivity)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.LeftEdge,
                    TargetBounds = new WindowBounds
                    {
                        Left = containerBounds.Left,
                        Top = windowBounds.Top,
                        Width = windowBounds.Width,
                        Height = windowBounds.Height
                    },
                    Distance = leftDistance,
                    Description = "Snap to left edge"
                });
            }

            // Right edge
            var rightDistance = Math.Abs((windowBounds.Left + windowBounds.Width) - (containerBounds.Left + containerBounds.Width));
            if (rightDistance <= _configuration.SnapSensitivity)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.RightEdge,
                    TargetBounds = new WindowBounds
                    {
                        Left = containerBounds.Left + containerBounds.Width - windowBounds.Width,
                        Top = windowBounds.Top,
                        Width = windowBounds.Width,
                        Height = windowBounds.Height
                    },
                    Distance = rightDistance,
                    Description = "Snap to right edge"
                });
            }

            // Top edge
            var topDistance = Math.Abs(windowBounds.Top - containerBounds.Top);
            if (topDistance <= _configuration.SnapSensitivity)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.TopEdge,
                    TargetBounds = new WindowBounds
                    {
                        Left = windowBounds.Left,
                        Top = containerBounds.Top,
                        Width = windowBounds.Width,
                        Height = windowBounds.Height
                    },
                    Distance = topDistance,
                    Description = "Snap to top edge"
                });
            }

            // Bottom edge
            var bottomDistance = Math.Abs((windowBounds.Top + windowBounds.Height) - (containerBounds.Top + containerBounds.Height));
            if (bottomDistance <= _configuration.SnapSensitivity)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.BottomEdge,
                    TargetBounds = new WindowBounds
                    {
                        Left = windowBounds.Left,
                        Top = containerBounds.Top + containerBounds.Height - windowBounds.Height,
                        Width = windowBounds.Width,
                        Height = windowBounds.Height
                    },
                    Distance = bottomDistance,
                    Description = "Snap to bottom edge"
                });
            }

            return targets;
        }

        /// <summary>
        /// Gets snap zone targets (drag to edge for half-screen layouts)
        /// </summary>
        private List<SnapTarget> GetSnapZoneTargets(WindowBounds windowBounds, WindowBounds containerBounds)
        {
            var targets = new List<SnapTarget>();
            var snapZoneSize = 50; // Size of snap zone at edges

            // Left half snap zone
            if (windowBounds.Left <= snapZoneSize)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.LeftHalf,
                    TargetBounds = new WindowBounds
                    {
                        Left = containerBounds.Left,
                        Top = containerBounds.Top,
                        Width = containerBounds.Width / 2,
                        Height = containerBounds.Height
                    },
                    Distance = windowBounds.Left,
                    Description = "Snap to left half"
                });
            }

            // Right half snap zone
            if (windowBounds.Left + windowBounds.Width >= containerBounds.Width - snapZoneSize)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.RightHalf,
                    TargetBounds = new WindowBounds
                    {
                        Left = containerBounds.Left + containerBounds.Width / 2,
                        Top = containerBounds.Top,
                        Width = containerBounds.Width / 2,
                        Height = containerBounds.Height
                    },
                    Distance = (containerBounds.Width - (windowBounds.Left + windowBounds.Width)),
                    Description = "Snap to right half"
                });
            }

            // Top maximize snap zone
            if (windowBounds.Top <= snapZoneSize)
            {
                targets.Add(new SnapTarget
                {
                    Type = SnapType.Maximize,
                    TargetBounds = new WindowBounds
                    {
                        Left = containerBounds.Left,
                        Top = containerBounds.Top,
                        Width = containerBounds.Width,
                        Height = containerBounds.Height
                    },
                    Distance = windowBounds.Top,
                    Description = "Maximize window"
                });
            }

            return targets;
        }

        /// <summary>
        /// Gets window-to-window snapping targets
        /// </summary>
        private List<SnapTarget> GetWindowSnapTargets(Guid movingWindowId, WindowBounds windowBounds)
        {
            var targets = new List<SnapTarget>();
            var allWindows = _windowManager.GetAllWindows();

            foreach (var otherWindow in allWindows.Where(w => w.Id != movingWindowId && w.State == WindowState.Normal))
            {
                var otherBounds = otherWindow.Bounds;

                // Snap to right edge of other window
                var rightSnapDistance = Math.Abs(windowBounds.Left - (otherBounds.Left + otherBounds.Width));
                if (rightSnapDistance <= _configuration.SnapSensitivity && 
                    DoesVerticalRangeOverlap(windowBounds, otherBounds))
                {
                    targets.Add(new SnapTarget
                    {
                        Type = SnapType.WindowRight,
                        TargetBounds = new WindowBounds
                        {
                            Left = otherBounds.Left + otherBounds.Width,
                            Top = windowBounds.Top,
                            Width = windowBounds.Width,
                            Height = windowBounds.Height
                        },
                        Distance = rightSnapDistance,
                        Description = $"Snap to right of {otherWindow.Title}"
                    });
                }

                // Snap to left edge of other window
                var leftSnapDistance = Math.Abs((windowBounds.Left + windowBounds.Width) - otherBounds.Left);
                if (leftSnapDistance <= _configuration.SnapSensitivity && 
                    DoesVerticalRangeOverlap(windowBounds, otherBounds))
                {
                    targets.Add(new SnapTarget
                    {
                        Type = SnapType.WindowLeft,
                        TargetBounds = new WindowBounds
                        {
                            Left = otherBounds.Left - windowBounds.Width,
                            Top = windowBounds.Top,
                            Width = windowBounds.Width,
                            Height = windowBounds.Height
                        },
                        Distance = leftSnapDistance,
                        Description = $"Snap to left of {otherWindow.Title}"
                    });
                }

                // Similar logic for top and bottom snapping...
                var bottomSnapDistance = Math.Abs(windowBounds.Top - (otherBounds.Top + otherBounds.Height));
                if (bottomSnapDistance <= _configuration.SnapSensitivity && 
                    DoesHorizontalRangeOverlap(windowBounds, otherBounds))
                {
                    targets.Add(new SnapTarget
                    {
                        Type = SnapType.WindowBottom,
                        TargetBounds = new WindowBounds
                        {
                            Left = windowBounds.Left,
                            Top = otherBounds.Top + otherBounds.Height,
                            Width = windowBounds.Width,
                            Height = windowBounds.Height
                        },
                        Distance = bottomSnapDistance,
                        Description = $"Snap below {otherWindow.Title}"
                    });
                }

                var topSnapDistance = Math.Abs((windowBounds.Top + windowBounds.Height) - otherBounds.Top);
                if (topSnapDistance <= _configuration.SnapSensitivity && 
                    DoesHorizontalRangeOverlap(windowBounds, otherBounds))
                {
                    targets.Add(new SnapTarget
                    {
                        Type = SnapType.WindowTop,
                        TargetBounds = new WindowBounds
                        {
                            Left = windowBounds.Left,
                            Top = otherBounds.Top - windowBounds.Height,
                            Width = windowBounds.Width,
                            Height = windowBounds.Height
                        },
                        Distance = topSnapDistance,
                        Description = $"Snap above {otherWindow.Title}"
                    });
                }
            }

            return targets;
        }

        /// <summary>
        /// Checks if two windows overlap vertically (for horizontal snapping)
        /// </summary>
        private bool DoesVerticalRangeOverlap(WindowBounds bounds1, WindowBounds bounds2)
        {
            var top1 = bounds1.Top;
            var bottom1 = bounds1.Top + bounds1.Height;
            var top2 = bounds2.Top;
            var bottom2 = bounds2.Top + bounds2.Height;

            return top1 < bottom2 && top2 < bottom1;
        }

        /// <summary>
        /// Checks if two windows overlap horizontally (for vertical snapping)
        /// </summary>
        private bool DoesHorizontalRangeOverlap(WindowBounds bounds1, WindowBounds bounds2)
        {
            var left1 = bounds1.Left;
            var right1 = bounds1.Left + bounds1.Width;
            var left2 = bounds2.Left;
            var right2 = bounds2.Left + bounds2.Width;

            return left1 < right2 && left2 < right1;
        }
    }
}
