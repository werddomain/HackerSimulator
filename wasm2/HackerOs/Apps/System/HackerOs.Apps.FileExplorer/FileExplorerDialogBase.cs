using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Globalization;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

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

    /// <summary>The explorer's texts in the language of the explorer's process (ADR 0104).</summary>
    private protected IStringLocalizer<Strings> L => AppContext.GetLocalizer<Strings>();

    /// <summary>The culture of the dates and numbers shown, from the explorer's locale (<c>LC_TIME</c>, <c>LANG</c>).</summary>
    private protected CultureInfo FormatCulture => AppContext?.FormatCulture ?? HackLocale.DefaultFormatCulture;

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
