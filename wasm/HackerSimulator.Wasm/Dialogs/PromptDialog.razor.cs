using Microsoft.AspNetCore.Components;

namespace HackerSimulator.Wasm.Dialogs
{
    public partial class PromptDialog : Dialog<string?>
    {
        [Parameter] public string Message { get; set; } = string.Empty;
        [Parameter] public string? DefaultText { get; set; }

        protected string _value = string.Empty;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _value = DefaultText ?? string.Empty;
        }

        protected void Ok() => CloseDialog(_value);
        protected void Cancel() => CloseDialog(null);
    }
}
