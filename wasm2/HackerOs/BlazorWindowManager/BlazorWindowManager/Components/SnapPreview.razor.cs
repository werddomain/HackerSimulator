using BlazorWindowManager.Services;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Component for displaying snap preview overlays during window dragging
/// </summary>
public partial class SnapPreview : ComponentBase, IDisposable
{
    [Inject] public SnappingService SnappingService { get; set; } = default!;

    private SnapTarget? _currentSnapTarget;
    private bool _isVisible = false;

    protected override void OnInitialized()
    {
        SnappingService.SnapPreviewChanged += OnSnapPreviewChanged;
    }

    private void OnSnapPreviewChanged(SnapTarget? snapTarget)
    {
        _currentSnapTarget = snapTarget;
        _isVisible = snapTarget != null;
        StateHasChanged();
    }

    private RenderFragment GetSnapIcon(SnapType snapType) => __builder =>
    {
        string icon = snapType switch
        {
            SnapType.LeftHalf => "⬅️",
            SnapType.RightHalf => "➡️",
            SnapType.Maximize => "⬆️",
            SnapType.LeftEdge => "📌",
            SnapType.RightEdge => "📌",
            SnapType.TopEdge => "📌",
            SnapType.BottomEdge => "📌",
            SnapType.WindowLeft => "🔗",
            SnapType.WindowRight => "🔗",
            SnapType.WindowTop => "🔗",
            SnapType.WindowBottom => "🔗",
            _ => "📌"
        };

        

        __builder.AddContent(0, icon);  
    };

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            SnappingService.SnapPreviewChanged -= OnSnapPreviewChanged;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
