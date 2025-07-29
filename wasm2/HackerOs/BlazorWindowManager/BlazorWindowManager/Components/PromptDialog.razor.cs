using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWindowManager.Components;

/// <summary>
/// Input prompt dialog component for collecting user text input
/// </summary>
public partial class PromptDialog : DialogBase<string>
{
    /// <summary>
    /// The prompt message to display
    /// </summary>
    [Parameter] public string Message { get; set; } = "Please enter a value:";

    /// <summary>
    /// The default value for the input
    /// </summary>
    [Parameter] public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// The label for the input field
    /// </summary>
    [Parameter] public string InputLabel { get; set; } = string.Empty;

    /// <summary>
    /// The placeholder text for the input
    /// </summary>
    [Parameter] public string Placeholder { get; set; } = string.Empty;

    /// <summary>
    /// The type of input (text, password, email, etc.)
    /// </summary>
    [Parameter] public string InputType { get; set; } = "text";

    /// <summary>
    /// Maximum length of input
    /// </summary>
    [Parameter] public int MaxLength { get; set; } = 0;

    /// <summary>
    /// Whether the input is required
    /// </summary>
    [Parameter] public bool Required { get; set; } = false;

    /// <summary>
    /// Whether to show character count
    /// </summary>
    [Parameter] public bool ShowCharacterCount { get; set; } = false;

    /// <summary>
    /// Custom validation function
    /// </summary>
    [Parameter] public Func<string, bool>? ValidationFunction { get; set; }

    /// <summary>
    /// Validation error message
    /// </summary>
    [Parameter] public string ValidationErrorMessage { get; set; } = "Invalid input";

    private string InputValue = string.Empty;
    private ElementReference inputElement;    protected override void OnInitialized()
    {
        base.OnInitialized();
        
        // Set dialog properties
        Resizable = false;
        InitialWidth = 450;
        InitialHeight = 250;
        
        // Set default title if not specified
        if (string.IsNullOrEmpty(Title))
        {
            Title = "Input Required";
        }

        // Set initial input value
        InputValue = DefaultValue;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Focus the input field when dialog appears
            await inputElement.FocusAsync();
        }
    }

    private bool IsInputValid()
    {
        if (Required && string.IsNullOrWhiteSpace(InputValue))
        {
            return false;
        }

        if (MaxLength > 0 && (InputValue?.Length ?? 0) > MaxLength)
        {
            return false;
        }

        if (ValidationFunction != null && !ValidationFunction(InputValue ?? string.Empty))
        {
            return false;
        }

        return true;
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && IsInputValid())
        {
            await OnOkClick();
        }
        else if (e.Key == "Escape")
        {
            await OnCancelClick();
        }
    }

    private async Task OnOkClick()
    {
        if (IsInputValid())
        {
            await CloseWithResultAsync(InputValue ?? string.Empty, "OK");
        }
    }

    private async Task OnCancelClick()
    {
        await CancelDialogAsync("Cancel");
    }

    protected override async Task<bool> OnBeforeCloseAsync()
    {
        // If closed via X button, treat as cancel
        if (_result == null)
        {
            _result = DialogResult<string>.Cancel();
        }

        return await base.OnBeforeCloseAsync();
    }
}
