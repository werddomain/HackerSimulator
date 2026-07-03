using Microsoft.AspNetCore.Components;

namespace HackerOs.AppFramework.Components;

/// <summary>
/// The top-level desktop shell of the ecosystem. It composes the window desktop
/// area, the taskbar (populated automatically as apps open) and the start-menu
/// launcher into a single drop-in component.
/// </summary>
public partial class Desktop : ComponentBase
{
    /// <summary>Title shown on the desktop watermark.</summary>
    [Parameter] public string Title { get; set; } = "HackerOS";

    /// <summary>CSS background applied to the desktop area.</summary>
    [Parameter] public string Background { get; set; } =
        "radial-gradient(circle at 20% 20%, #0b2018 0%, #05080a 60%, #02040a 100%)";
}
