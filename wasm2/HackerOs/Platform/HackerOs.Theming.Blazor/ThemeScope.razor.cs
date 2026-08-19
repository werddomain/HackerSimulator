using HackerOs.Theming.Abstractions;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Theming.Blazor;

/// <summary>
/// Applies one validated theme to an arbitrary subtree and contributes the RCL's static theme
/// stylesheet to the document head. A host should render one scope around its shell root.
/// </summary>
public partial class ThemeScope : ComponentBase
{
    private ThemeRenderState _renderState = ThemeRenderState.Create(
        WellKnownThemeIds.HackerOs,
        ThemePlatform.Desktop,
        WellKnownAccentIds.Green,
        animationsEnabled: true);

    /// <summary>Gets or sets the exact built-in theme identifier to apply.</summary>
    [Parameter]
    public string ThemeId { get; set; } = WellKnownThemeIds.HackerOs;

    /// <summary>Gets or sets the shell form factor the selected theme must support.</summary>
    [Parameter]
    public ThemePlatform Platform { get; set; } = ThemePlatform.Desktop;

    /// <summary>Gets or sets the validated accent identifier; only HackerOS consumes its color.</summary>
    [Parameter]
    public string AccentId { get; set; } = WellKnownAccentIds.Green;

    /// <summary>Gets or sets whether non-essential theme motion remains enabled.</summary>
    [Parameter]
    public bool AnimationsEnabled { get; set; } = true;

    /// <summary>Gets or sets the shell or reusable chrome subtree receiving the theme tokens.</summary>
    [Parameter, EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets the validated, attribute-safe render state for the current parameters.</summary>
    private ThemeRenderState RenderState => _renderState;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Enum.IsDefined(Platform))
        {
            throw new ArgumentOutOfRangeException(nameof(Platform));
        }

        _renderState = ThemeRenderState.Create(ThemeId, Platform, AccentId, AnimationsEnabled);
    }
}
