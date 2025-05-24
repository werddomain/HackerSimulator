using Microsoft.AspNetCore.Components;

namespace HackerSimulator.Wasm.Dialogs
{
    public partial class MessageBoxDialog : Dialog<bool>
    {
        [Parameter] public string Message { get; set; } = string.Empty;
        [Parameter] public string OkText { get; set; } = "OK";
        [Parameter] public string CancelText { get; set; } = "Cancel";
        [Parameter] public bool ShowCancel { get; set; }

        protected override bool DefaultResult => false;

        protected void Ok() => CloseDialog(true);
        protected void Cancel() => CloseDialog(false);
    }
}
