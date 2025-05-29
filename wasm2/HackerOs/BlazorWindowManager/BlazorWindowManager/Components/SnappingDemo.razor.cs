using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorWindowManager.Services;
using BlazorWindowManager.Models;

namespace BlazorWindowManager.Components;

/// <summary>
/// Demo page for testing and validating window snapping functionality
/// </summary>
public partial class SnappingDemo : ComponentBase, IDisposable
{    [Inject] private SnappingService SnappingService { get; set; } = default!;
    [Inject] private WindowManagerService WindowManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
      private DesktopArea? desktopArea;
    private List<TestWindowInfo> testWindows = new();
    private int windowCounter = 1;
    private string lastSnapEvent = "None";
    
    // Configuration properties bound to UI
    private int edgeSensitivity = 15;
    private int zoneSensitivity = 50;

    /// <summary>
    /// Test window information for demo purposes
    /// </summary>
    private class TestWindowInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 200;
        public WindowState State { get; set; } = WindowState.Normal;    }

    /// <summary>
    /// Initializes the snapping demo component
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // Subscribe to snapping events for testing
        SnappingService.OnSnapPreviewChanged += OnSnapPreviewChanged;
        SnappingService.OnSnapApplied += OnSnapApplied;
        
        // Initialize with current service values
        edgeSensitivity = SnappingService.EdgeSensitivity;
        zoneSensitivity = SnappingService.ZoneSensitivity;
        
        await base.OnInitializedAsync();
    }

    private void OnSnapPreviewChanged(SnapPreviewInfo? previewInfo)
    {
        lastSnapEvent = previewInfo != null 
            ? $"Preview: {previewInfo.SnapType} at {DateTime.Now:HH:mm:ss}"
            : "Preview cleared";
        
        InvokeAsync(StateHasChanged);
    }

    private void OnSnapApplied(SnapResult snapResult)
    {
        lastSnapEvent = $"Applied: {snapResult.SnapType} to ({snapResult.NewBounds.X}, {snapResult.NewBounds.Y}) at {DateTime.Now:HH:mm:ss}";        InvokeAsync(StateHasChanged);
    }

    #region Configuration Event Handlers

    private async Task OnSnappingEnabledChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnEdgeSensitivityChanged()
    {
        SnappingService.EdgeSensitivity = edgeSensitivity;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnZoneSensitivityChanged()
    {
        SnappingService.ZoneSensitivity = zoneSensitivity;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnShowPreviewChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    // Legacy methods kept for compatibility
    private async Task OnSnappingEnabledChanged(ChangeEventArgs e)
    {
        SnappingService.IsEnabled = (bool)(e.Value ?? false);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnEdgeSensitivityChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int value))
        {
            edgeSensitivity = value;
            SnappingService.EdgeSensitivity = value;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnZoneSensitivityChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int value))
        {
            zoneSensitivity = value;
            SnappingService.ZoneSensitivity = value;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnShowPreviewChanged(ChangeEventArgs e)
    {
        SnappingService.ShowSnapPreview = (bool)(e.Value ?? false);
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Test Window Management

    private void CreateTestWindow()
    {
        var random = new Random();
        var window = new TestWindowInfo
        {
            Title = $"Test Window {windowCounter++}",
            Left = random.Next(50, 400),
            Top = random.Next(50, 300),
            Width = 300,
            Height = 200
        };
        
        testWindows.Add(window);
        StateHasChanged();
    }

    private void CreateMultipleWindows()
    {
        for (int i = 0; i < 3; i++)
        {
            CreateTestWindow();
        }
    }

    private void PositionWindowsForTesting()
    {
        if (testWindows.Count == 0)
        {
            CreateMultipleWindows();
        }

        // Position windows for optimal edge snapping testing
        if (testWindows.Count >= 1)
        {
            testWindows[0].Left = 10;
            testWindows[0].Top = 50;
        }
        
        if (testWindows.Count >= 2)
        {
            testWindows[1].Left = 400;
            testWindows[1].Top = 50;
        }
        
        if (testWindows.Count >= 3)
        {
            testWindows[2].Left = 200;
            testWindows[2].Top = 250;
        }
        
        StateHasChanged();
    }

    private void RemoveWindow(Guid windowId)
    {
        testWindows.RemoveAll(w => w.Id == windowId);
        StateHasChanged();
    }

    private void ClearAllWindows()
    {
        testWindows.Clear();
        StateHasChanged();
    }

    private void MoveWindowToCorner(TestWindowInfo window, string corner)
    {
        // Move window to specified corner for testing edge snapping
        switch (corner)
        {
            case "top-left":
                window.Left = 5;
                window.Top = 5;
                break;
            case "top-right":
                window.Left = 500;
                window.Top = 5;
                break;
            case "bottom-left":
                window.Left = 5;
                window.Top = 400;
                break;
            case "bottom-right":
                window.Left = 500;
                window.Top = 400;
                break;
        }
          StateHasChanged();
    }

    #endregion

    /// <summary>
    /// Disposes the component and unsubscribes from events
    /// </summary>
    public void Dispose()
    {
        // Unsubscribe from events
        SnappingService.OnSnapPreviewChanged -= OnSnapPreviewChanged;
        SnappingService.OnSnapApplied -= OnSnapApplied;
    }
}
