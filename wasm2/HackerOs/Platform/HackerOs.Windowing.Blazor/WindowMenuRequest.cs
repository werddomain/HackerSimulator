using HackerOs.Windowing.Abstractions;

namespace HackerOs.Windowing.Blazor;

/// <summary>
/// A request to show the window menu of one window (ADR 0095 §3), raised by the chrome.
/// </summary>
/// <param name="WindowId">Window whose menu is requested.</param>
/// <param name="ClientX">
/// Pointer position in viewport CSS pixels for a right click on the title bar, or
/// <see langword="null"/> when the menu opens from its button or the keyboard: the host then places it
/// under the window's top-left corner.
/// </param>
/// <param name="ClientY">Pointer position in viewport CSS pixels, or <see langword="null"/>.</param>
public sealed record WindowMenuRequest(WindowId WindowId, double? ClientX, double? ClientY);
