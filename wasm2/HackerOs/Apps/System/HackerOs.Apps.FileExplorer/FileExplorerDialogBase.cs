using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// Base of the explorer's MudBlazor dialog bodies. MudBlazor renders them in its dialog provider,
/// outside the window's cascading <see cref="IAppExecutionContext"/>, so the window passes its context
/// as <see cref="OwnerAppContext"/>; the dialog then runs its lifecycle and event handlers inside the
/// explorer's ambient frame, like any <see cref="HackComponentBase"/> (ADR 0067: code outside a frame
/// would be charged to the compositor).
/// </summary>
public abstract class FileExplorerDialogBase : HackComponentBase
{
    /// <summary>Gets or sets the context of the explorer window that opened the dialog.</summary>
    [Parameter]
    public IAppExecutionContext? OwnerAppContext { get; set; }

    /// <summary>Adopts <see cref="OwnerAppContext"/> before the base class opens the ambient scope.</summary>
    /// <param name="parameters">The dialog parameters.</param>
    /// <returns>The base lifecycle task.</returns>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (parameters.TryGetValue(nameof(OwnerAppContext), out IAppExecutionContext? owner) && owner is not null)
        {
            AppContext = owner;
        }

        return base.SetParametersAsync(parameters);
    }
}
