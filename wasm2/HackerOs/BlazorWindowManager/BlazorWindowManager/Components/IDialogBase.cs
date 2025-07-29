using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components
{
    public interface IDialogBase
    {
        RenderFragment? ChildContent { get; set; }
        bool CloseOnOverlayClick { get; set; }
        bool IsModal { get; set; }
        bool IsDialogVisible { get; }
        WindowBase? OwnerWindow { get; set; }

        Task CancelDialogAsync(string closeReason = "Cancel");
       
        Task CloseWithErrorAsync(string errorMessage, string closeReason = "Error");
        
    }
}