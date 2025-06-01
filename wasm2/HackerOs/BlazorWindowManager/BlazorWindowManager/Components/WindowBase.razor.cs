using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BlazorWindowManager.Components;

/// <summary>
/// Base window component that provides core windowing functionality including
/// dragging, resizing, state management, and integration with WindowManagerService.
/// This is the main partial class file containing parameters and core properties.
/// </summary>
public partial class WindowBase : ComponentBase, IWindowMessageReceiver, IAsyncDisposable
{
    #region Parameters

    [CascadingParameter]
    internal WindowContext Context { get; set; }

    /// <summary>
    /// Unique identifier for this window instance
    /// </summary>
    [Parameter] public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Optional user-defined name for the window
    /// </summary>
    [Parameter] public string? Name { get; set; }

    private string title = "Window";
    /// <summary>
    /// Title displayed in the window's title bar
    /// </summary>
    public string Title { get => title; set => SetTitle(value); }
    
    /// <summary>
    /// Optional icon content for the title bar
    /// </summary>
    [Parameter] public RenderFragment? Icon { get; set; }
    
    /// <summary>
    /// Content to be displayed in the window
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// Whether the window can be resized
    /// </summary>
    [Parameter] public bool Resizable { get; set; } = true;
    
    /// <summary>
    /// Minimum width constraint in pixels
    /// </summary>
    [Parameter] public double? MinWidth { get; set; } = 200;
    
    /// <summary>
    /// Minimum height constraint in pixels
    /// </summary>
    [Parameter] public double? MinHeight { get; set; } = 150;
    
    /// <summary>
    /// Maximum width constraint in pixels
    /// </summary>
    [Parameter] public double? MaxWidth { get; set; }
    
    /// <summary>
    /// Maximum height constraint in pixels
    /// </summary>
    [Parameter] public double? MaxHeight { get; set; }
    
    /// <summary>
    /// Initial X position when the window is first created
    /// </summary>
    [Parameter] public double? InitialX { get; set; }
    
    /// <summary>
    /// Initial Y position when the window is first created
    /// </summary>
    [Parameter] public double? InitialY { get; set; }
    
    /// <summary>
    /// Initial width when the window is first created
    /// </summary>
    [Parameter] public double? InitialWidth { get; set; } = 600;
    
    /// <summary>
    /// Initial height when the window is first created
    /// </summary>
    [Parameter] public double? InitialHeight { get; set; } = 400;
    
    /// <summary>
    /// Additional CSS class to apply to the window container
    /// </summary>
    [Parameter] public string? CssClass { get; set; }
    
    /// <summary>
    /// Parent window for modal dialogs
    /// </summary>
    [Parameter] public WindowBase? ParentWindow { get; set; }
      /// <summary>
    /// Whether this window is modal (blocks interaction with other windows)
    /// </summary>
    [Parameter] public bool IsModal { get; set; } = false;
    
    /// <summary>
    /// Whether the close button should be shown in the title bar
    /// </summary>
    [Parameter] public bool ShowCloseButton { get; set; } = true;
    
    #endregion
}
