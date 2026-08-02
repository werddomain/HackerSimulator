using System.Text.Json;
using System.Text.Json.Serialization;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Creates canonical app/user/device settings definitions for eligible window geometry.</summary>
public static class WindowGeometrySettings
{
    /// <summary>Stable document ID used for window geometry only.</summary>
    public const string DocumentId = "window-geometry";

    /// <summary>Creates one definition to register during user-session composition.</summary>
    public static SettingsDocumentDefinition CreateDefinition(
        string appId,
        string userId,
        string installationId)
    {
        SettingsDocumentKey key = SettingsDocumentKey.ForAppUserDevice(appId, userId, installationId, DocumentId);
        return new SettingsDocumentDefinition(
            SettingsDocumentPathFactory.GetProjectionPath(key),
            key,
            "{\"hasValue\":false,\"x\":0,\"y\":0,\"width\":1,\"height\":1}",
            "application/json",
            AppCapabilities.SettingsRead,
            AppCapabilities.SettingsWrite,
            AppAuthority.User,
            AppAuthority.User,
            WindowGeometryValidator.Instance);
    }

    private sealed class WindowGeometryValidator : ISettingsDocumentValidator
    {
        public static WindowGeometryValidator Instance { get; } = new();

        public IReadOnlyList<string> Validate(string content)
        {
            try
            {
                WindowGeometryDocument? document = JsonSerializer.Deserialize(
                    content,
                    WindowGeometryJsonContext.Default.WindowGeometryDocument);
                if (document is null || document.Width <= 0 || document.Height <= 0
                    || !double.IsFinite(document.X) || !double.IsFinite(document.Y)
                    || !double.IsFinite(document.Width) || !double.IsFinite(document.Height))
                {
                    return ["window.geometry.invalid"];
                }

                return [];
            }
            catch (JsonException)
            {
                return ["window.geometry.invalid-json"];
            }
        }
    }

}

internal sealed record WindowGeometryDocument(bool HasValue, double X, double Y, double Width, double Height);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WindowGeometryDocument))]
internal sealed partial class WindowGeometryJsonContext : JsonSerializerContext;

/// <summary>Persists and restores eligible window geometry without volatile app state.</summary>
public sealed class WindowGeometryPersistence(ISettingsDocumentService settings)
{
    private readonly ISettingsDocumentService _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Reads saved geometry, returning <see langword="null"/> for a clean profile.</summary>
    public async ValueTask<WindowBounds?> RestoreAsync(
        SettingsDocumentDefinition definition,
        AppOperationContext context,
        CancellationToken cancellationToken = default)
    {
        SettingsReadResult result = await _settings.ReadAsync(definition.Path, context, cancellationToken);
        if (result.Status != SettingsReadStatus.Success || result.Document is null)
        {
            return null;
        }

        WindowGeometryDocument document =
            JsonSerializer.Deserialize(result.Document.Content, WindowGeometryJsonContext.Default.WindowGeometryDocument)
            ?? throw new InvalidDataException("Window geometry document is empty.");
        return document.HasValue
            ? new WindowBounds(document.X, document.Y, document.Width, document.Height)
            : null;
    }

    /// <summary>Saves geometry when persistence is explicitly eligible.</summary>
    public async ValueTask<SettingsWriteStatus> SaveAsync(
        SettingsDocumentDefinition definition,
        WindowBounds bounds,
        bool isEligible,
        AppOperationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!isEligible)
        {
            return SettingsWriteStatus.Denied;
        }

        SettingsReadResult current = await _settings.ReadAsync(definition.Path, context, cancellationToken);
        if (current.Status != SettingsReadStatus.Success || current.Document is null)
        {
            return current.Status == SettingsReadStatus.Denied
                ? SettingsWriteStatus.Denied
                : SettingsWriteStatus.NotFound;
        }

        string content = JsonSerializer.Serialize(
            new WindowGeometryDocument(true, bounds.X, bounds.Y, bounds.Width, bounds.Height),
            WindowGeometryJsonContext.Default.WindowGeometryDocument);
        SettingsWriteResult result = await _settings.WriteAsync(
            new SettingsWriteRequest(definition.Path, content, current.Document.Revision),
            context,
            cancellationToken);
        return result.Status;
    }
}