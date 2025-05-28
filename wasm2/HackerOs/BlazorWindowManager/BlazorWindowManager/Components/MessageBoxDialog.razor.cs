using BlazorWindowManager.Services;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Message box dialog component for displaying messages with various button configurations
/// </summary>
public partial class MessageBoxDialog : DialogBase<MessageBoxResult>
{
    /// <summary>
    /// The message to display in the dialog
    /// </summary>
    [Parameter] public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The buttons to show in the dialog
    /// </summary>
    [Parameter] public MessageBoxButtons Buttons { get; set; } = MessageBoxButtons.OK;

    /// <summary>
    /// The icon to display in the dialog
    /// </summary>
    [Parameter] public MessageBoxIcon Icon { get; set; } = MessageBoxIcon.Information;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        
        // Set dialog properties
        Resizable = false;
        Width = 400;
        Height = 200;
        
        // Set title if not already set
        if (string.IsNullOrEmpty(Title))
        {
            Title = GetDefaultTitle();
        }
    }

    private string GetDefaultTitle()
    {
        return Icon switch
        {
            MessageBoxIcon.Error => "Error",
            MessageBoxIcon.Warning => "Warning",
            MessageBoxIcon.Question => "Question",
            MessageBoxIcon.Information => "Information",
            _ => "Message"
        };
    }

    private string GetIconClass()
    {
        return Icon.ToString().ToLower();
    }

    private string GetIconSymbol()
    {
        return Icon switch
        {
            MessageBoxIcon.Error => "❌",
            MessageBoxIcon.Warning => "⚠️",
            MessageBoxIcon.Question => "❓",
            MessageBoxIcon.Information => "ℹ️",
            _ => ""
        };
    }

    private List<MessageBoxButton> GetButtons()
    {
        return Buttons switch
        {
            MessageBoxButtons.OK => new List<MessageBoxButton> { MessageBoxButton.OK },
            MessageBoxButtons.OKCancel => new List<MessageBoxButton> { MessageBoxButton.OK, MessageBoxButton.Cancel },
            MessageBoxButtons.YesNo => new List<MessageBoxButton> { MessageBoxButton.Yes, MessageBoxButton.No },
            MessageBoxButtons.YesNoCancel => new List<MessageBoxButton> { MessageBoxButton.Yes, MessageBoxButton.No, MessageBoxButton.Cancel },
            _ => new List<MessageBoxButton> { MessageBoxButton.OK }
        };
    }

    private string GetButtonText(MessageBoxButton button)
    {
        return button switch
        {
            MessageBoxButton.OK => "OK",
            MessageBoxButton.Cancel => "Cancel",
            MessageBoxButton.Yes => "Yes",
            MessageBoxButton.No => "No",
            _ => button.ToString()
        };
    }

    private string GetButtonClass(MessageBoxButton button)
    {
        return button switch
        {
            MessageBoxButton.OK or MessageBoxButton.Yes => "primary",
            MessageBoxButton.No when Buttons == MessageBoxButtons.YesNo => "danger",
            _ => ""
        };
    }

    private async Task OnButtonClick(MessageBoxButton button)
    {
        var result = new MessageBoxResult(button, true);
        var r = new DialogResult<MessageBoxResult>() { Data = result, Success = result.Success};
        await CloseDialogAsync(r);
    }

    /// <inheritdoc/>
    protected override async Task<bool> OnBeforeCloseAsync()
    {
        // If closed via X button or other means, treat as Cancel/No
        if (_result == null)
        {
            var cancelButton = Buttons switch
            {
                MessageBoxButtons.OKCancel or MessageBoxButtons.YesNoCancel => MessageBoxButton.Cancel,
                MessageBoxButtons.YesNo => MessageBoxButton.No,
                _ => MessageBoxButton.OK
            };
            
            _result = new DialogResult<MessageBoxResult>() { Data = new MessageBoxResult(cancelButton, false), Success = false };
        }

        return await base.OnBeforeCloseAsync();
    }
}
