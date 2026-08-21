using HackerOs.Theming.Abstractions;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Theming.Blazor;

/// <summary>Renders a compact, non-interactive sample using the exact tokens used by shell chrome.</summary>
public partial class ThemePreview : ComponentBase
{
    private ThemeRenderState _renderState = ThemeRenderState.Create(
        WellKnownThemeIds.HackerOs,
        ThemePlatform.Desktop,
        WellKnownAccentIds.Green,
        animationsEnabled: true);

    /// <summary>Gets or sets the exact built-in theme identifier to preview.</summary>
    [Parameter]
    public string ThemeId { get; set; } = WellKnownThemeIds.HackerOs;

    /// <summary>Gets or sets the validated accent identifier; only HackerOS consumes its color.</summary>
    [Parameter]
    public string AccentId { get; set; } = WellKnownAccentIds.Green;

    /// <summary>Gets or sets whether the preview may use non-essential theme motion.</summary>
    [Parameter]
    public bool AnimationsEnabled { get; set; } = true;

    /// <summary>Gets the validated, attribute-safe render state for the current parameters.</summary>
    private ThemeRenderState RenderState => _renderState;

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        _renderState = ThemeRenderState.Create(ThemeId, requiredPlatform: null, AccentId, AnimationsEnabled);
}
