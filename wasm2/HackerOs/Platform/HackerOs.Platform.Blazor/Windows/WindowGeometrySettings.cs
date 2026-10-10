using System.Globalization;
using HackerOs.Platform.Core.Settings;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>
/// Locates the window geometry state file of one app:
/// <c>~/.local/state/hackeros/windows/&lt;appId&gt;.config</c>. Geometry is device-local state, not a
/// preference, so it lives outside <c>~/.config</c> and is never synchronized (ADR 0044 section 10).
/// </summary>
public static class WindowGeometrySettings
{
    /// <summary>Gets the state file of <paramref name="appId"/>.</summary>
    /// <param name="appId">Reverse-DNS app identifier.</param>
    public static OsSettingsFile GetFile(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (appId.Contains('/', StringComparison.Ordinal) || appId.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("An app id cannot contain path separators.", nameof(appId));
        }

        return new OsSettingsFile(OsSettingsLocation.UserState, $"windows/{appId}.config");
    }

    /// <summary>
    /// Parses a geometry file (<c>x=</c>, <c>y=</c>, <c>width=</c>, <c>height=</c>, invariant culture).
    /// Returns <see langword="null"/> for an empty, partial or invalid file: a bad state file only
    /// means the window opens at its default place.
    /// </summary>
    /// <param name="content">File content, or <see langword="null"/> when absent.</param>
    public static WindowBounds? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        Dictionary<string, double> values = new(StringComparer.Ordinal);
        foreach (ConfigLine line in ConfigDocumentLines.Parse(content))
        {
            if (line.Kind == ConfigLineKind.KeyValue && line.Group is null && line.Key is { } key
                && double.TryParse(line.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && double.IsFinite(number))
            {
                values[key] = number;
            }
        }

        return values.TryGetValue("x", out double x)
            && values.TryGetValue("y", out double y)
            && values.TryGetValue("width", out double width) && width > 0
            && values.TryGetValue("height", out double height) && height > 0
            ? new WindowBounds(x, y, width, height)
            : null;
    }

    /// <summary>Serializes geometry in the format read by <see cref="Parse"/>.</summary>
    /// <param name="bounds">Window bounds to save.</param>
    public static string Serialize(WindowBounds bounds) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"x={bounds.X}\ny={bounds.Y}\nwidth={bounds.Width}\nheight={bounds.Height}\n");
}

/// <summary>Persists and restores eligible window geometry without volatile app state.</summary>
public sealed class WindowGeometryPersistence(IOsSettingsService settings)
{
    private readonly IOsSettingsService _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Reads saved geometry, returning <see langword="null"/> for a clean profile or an unreadable file.</summary>
    /// <param name="appId">App whose window is restored.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async ValueTask<WindowBounds?> RestoreAsync(string appId, CancellationToken cancellationToken = default)
    {
        string? content = await _settings
            .ReadFileAsync(WindowGeometrySettings.GetFile(appId), cancellationToken)
            .ConfigureAwait(false);
        return WindowGeometrySettings.Parse(content);
    }

    /// <summary>Saves geometry when persistence is explicitly eligible.</summary>
    /// <param name="appId">App whose window is saved.</param>
    /// <param name="bounds">Current window bounds.</param>
    /// <param name="isEligible">Whether the window opted into geometry persistence.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public async ValueTask<bool> SaveAsync(
        string appId,
        WindowBounds bounds,
        bool isEligible,
        CancellationToken cancellationToken = default)
    {
        if (!isEligible || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        try
        {
            await _settings
                .WriteFileAsync(WindowGeometrySettings.GetFile(appId), WindowGeometrySettings.Serialize(bounds), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            // No session, or a home the user cannot write: geometry is best effort.
            return false;
        }
    }
}
