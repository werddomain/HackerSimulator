using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Sample window component demonstrating window functionality
/// </summary>
public partial class SampleWindow : WindowBase
{
    private List<string> ContentItems = new();
    private int titleCounter = 1;
    
    protected override void OnInitialized()
    {
        ContentItems.Add($"Initial content item created at {DateTime.Now:HH:mm:ss}");
        base.OnInitialized();
    }
    
    private async Task ChangeTitle()
    {
        await SetTitle($"Sample Window {titleCounter++}");
    }
    
    private void SendMessage()
    {
        // Example of inter-window communication
        var message = $"Hello from {Title} at {DateTime.Now:HH:mm:ss}";
        // This would typically target a specific window
        // WindowManager.SendMessage(Id, targetWindowId, message);
        ContentItems.Add($"Message sent: {message}");
        StateHasChanged();
    }
    
    private void AddContent()
    {
        ContentItems.Add($"Content added at {DateTime.Now:HH:mm:ss}");
        StateHasChanged();
    }
    
    public override void OnMessageReceived(WindowMessageEventArgs args)
    {
        ContentItems.Add($"Message received from {args.SourceWindowId}: {args.Message}");
        StateHasChanged();
    }
}
