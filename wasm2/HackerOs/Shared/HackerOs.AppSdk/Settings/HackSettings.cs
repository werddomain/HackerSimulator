using System.Text.RegularExpressions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.AppSdk.Settings;

/// <summary>
/// Typed access to an app's layered settings files (ADR 0044): defaults declared by the app, then
/// <c>/etc/&lt;appId&gt;/&lt;document&gt;.config</c> (system), then <c>~/.config/&lt;appId&gt;/&lt;document&gt;.config</c>
/// (user), with keys an administrator can lock in the system file's <c>[locked]</c> section.
/// </summary>
/// <remarks>
/// <para>
/// The schema (keys, types, defaults) comes from <see cref="HackerOs.App.Abstractions.AppManifest.Settings"/>.
/// The app needs <c>filesystem.private.read</c> to read its two layers and
/// <c>filesystem.private.write</c> to write its user layer.
/// </para>
/// <code>
/// await using HackSettingsDocument settings = await HackSettings.OpenAsync(AppContext);
/// long tabSize = settings.Get&lt;long&gt;("Editor.tab-size");
/// await settings.SetAsync("theme", "light");
/// </code>
/// <para>
/// Phase 1 takes the app's <see cref="IAppExecutionContext"/> explicitly. When the ambient context of
/// ADR 0043 (<c>HackExecutionContext</c>) is available, context-free overloads with the same names
/// are added on top of these; this API does not change.
/// </para>
/// </remarks>
public static partial class HackSettings
{
    /// <summary>Default document identifier, giving <c>settings.config</c>.</summary>
    public const string DefaultDocumentId = "settings";

    /// <summary>Opens one settings document of the calling app and starts watching its files.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="documentId">Document name, i.e. the file name without <c>.config</c>.</param>
    /// <param name="cancellationToken">Cancels the initial reads.</param>
    /// <returns>An open document; dispose it to stop watching.</returns>
    /// <exception cref="InvalidOperationException">The app's manifest declares no settings schema.</exception>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> is not a plain file name.</exception>
    /// <exception cref="UnauthorizedAccessException">The app may not read one of its two layers.</exception>
    public static ValueTask<HackSettingsDocument> OpenAsync(
        IAppExecutionContext context,
        string documentId = DefaultDocumentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateDocumentId(documentId);
        SettingsSchema schema = context.Manifest.Settings is { } declared
            ? SettingsSchema.FromManifest(declared)
            : throw new InvalidOperationException(
                $"App '{context.Manifest.Id}' declares no settings schema in its manifest.");
        return HackSettingsDocument.OpenAsync(context, schema, documentId, cancellationToken);
    }

    /// <summary>Reads one value of the default document (opens, reads and closes it).</summary>
    /// <typeparam name="T">See <see cref="HackSettingsDocument.Get{T}"/>.</typeparam>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public static async ValueTask<T> GetAsync<T>(
        IAppExecutionContext context,
        string qualifiedKey,
        CancellationToken cancellationToken = default)
    {
        await using HackSettingsDocument document = await OpenAsync(context, DefaultDocumentId, cancellationToken).ConfigureAwait(false);
        return document.Get<T>(qualifiedKey);
    }

    /// <summary>Writes one value to the user layer of the default document.</summary>
    /// <typeparam name="T">See <see cref="HackSettingsDocument.Get{T}"/>.</typeparam>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="qualifiedKey"><c>key</c> or <c>Group.key</c>, as declared.</param>
    /// <param name="value">New value.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public static async ValueTask SetAsync<T>(
        IAppExecutionContext context,
        string qualifiedKey,
        T value,
        CancellationToken cancellationToken = default)
    {
        await using HackSettingsDocument document = await OpenAsync(context, DefaultDocumentId, cancellationToken).ConfigureAwait(false);
        await document.SetAsync(qualifiedKey, value, SettingLayer.User, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateDocumentId(string documentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        if (!DocumentIdPattern().IsMatch(documentId))
        {
            throw new ArgumentException(
                $"Settings document id '{documentId}' must be a plain name (letters, digits, '.', '-', '_').",
                nameof(documentId));
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9._-]*$")]
    private static partial Regex DocumentIdPattern();
}
