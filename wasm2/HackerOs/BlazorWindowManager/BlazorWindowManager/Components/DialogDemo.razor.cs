using BlazorWindowManager.Services;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Demonstration component for various dialog types and functionality
/// </summary>
public partial class DialogDemo : WindowBase
{
    [Inject] public DialogService DialogService { get; set; } = default!;
    [Inject] public WindowManagerService WindowManager { get; set; } = default!;

    private string _lastResult = string.Empty;

    private async Task ShowInfoMessage()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "This is an information message. Everything is working correctly!",
            "Information",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
            
        _lastResult = $"Info Message - Button: {result.Button}, Success: {result.Success}";
        StateHasChanged();
    }

    private async Task ShowWarningMessage()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "This is a warning message. Please proceed with caution.",
            "Warning",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);
            
        _lastResult = $"Warning Message - Button: {result.Button}, Success: {result.Success}";
        StateHasChanged();
    }

    private async Task ShowErrorMessage()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "An error has occurred! The operation could not be completed.",
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
            
        _lastResult = $"Error Message - Button: {result.Button}, Success: {result.Success}";
        StateHasChanged();
    }

    private async Task ShowQuestionMessage()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "Do you want to save the changes before closing?",
            "Save Changes?",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
            
        _lastResult = $"Question Message - Button: {result.Button}, Success: {result.Success}";
        StateHasChanged();
    }

    private async Task ShowYesNoConfirmation()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "Are you sure you want to delete this item? This action cannot be undone.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
            
        _lastResult = $"Yes/No Confirmation - Button: {result.Button}, Success: {result.Success}";
        StateHasChanged();
    }

    private async Task ShowYesNoCancelConfirmation()
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "Would you like to save your work before exiting?",
            "Save Before Exit",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
            
        _lastResult = $"Yes/No/Cancel Confirmation - Button: {result.Button}, Success: {result.Success}";
        StateHasChanged();
    }

    private async Task ShowSimpleConfirmation()
    {
        var confirmed = await DialogService.ShowConfirmationAsync(
            "This will permanently delete all data. Continue?",
            "Confirm Action");
            
        _lastResult = $"Simple Confirmation - Confirmed: {confirmed}";
        StateHasChanged();
    }

    private async Task ShowTextPrompt()
    {
        var result = await DialogService.ShowPromptAsync(
            "Please enter your name:",
            "Name Input",
            "John Doe");
            
        _lastResult = $"Text Prompt - Success: {result.Success}, Value: '{result.Data}', Reason: {result.CloseReason}";
        StateHasChanged();
    }

    private async Task ShowPasswordPrompt()
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(PromptDialog.Message)] = "Please enter your password:",
            [nameof(PromptDialog.Title)] = "Password Required",
            [nameof(PromptDialog.InputType)] = "password",
            [nameof(PromptDialog.Required)] = true,
            [nameof(PromptDialog.Placeholder)] = "Enter password..."
        };

        var dialog = DialogService.Create<PromptDialog, string>(parameters);
        var result = await dialog.ShowDialogAsync();
        
        var maskedValue = string.IsNullOrEmpty(result.Data) ? "(empty)" : new string('*', result.Data.Length);
        _lastResult = $"Password Prompt - Success: {result.Success}, Value: '{maskedValue}', Reason: {result.CloseReason}";
        StateHasChanged();
    }

    private async Task ShowEmailPrompt()
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(PromptDialog.Message)] = "Please enter your email address:",
            [nameof(PromptDialog.Title)] = "Email Input",
            [nameof(PromptDialog.InputType)] = "email",
            [nameof(PromptDialog.InputLabel)] = "Email Address",
            [nameof(PromptDialog.Placeholder)] = "user@example.com",
            [nameof(PromptDialog.Required)] = true,
            [nameof(PromptDialog.ValidationFunction)] = new Func<string, bool>(IsValidEmail),
            [nameof(PromptDialog.ValidationErrorMessage)] = "Please enter a valid email address"
        };

        var dialog = DialogService.Create<PromptDialog, string>(parameters);
        var result = await dialog.ShowDialogAsync();
        
        _lastResult = $"Email Prompt - Success: {result.Success}, Value: '{result.Data}', Reason: {result.CloseReason}";
        StateHasChanged();
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
            
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
