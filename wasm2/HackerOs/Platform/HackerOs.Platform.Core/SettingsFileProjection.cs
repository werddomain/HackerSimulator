using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core;

/// <summary>
/// Projects canonical settings documents into the virtual filesystem namespace.
/// </summary>
public sealed class SettingsFileProjection(ISettingsDocumentService settings) : ISettingsFileProjection
{
    private readonly ISettingsDocumentService _settings =
        settings ?? throw new ArgumentNullException(nameof(settings));

    /// <inheritdoc />
    public ValueTask<SettingsReadResult> ReadFileAsync(
        VirtualPath path,
        AppOperationContext context,
        CancellationToken cancellationToken = default) =>
        _settings.ReadAsync(path, context, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SettingsWriteResult> WriteFileAsync(
        SettingsWriteRequest request,
        AppOperationContext context,
        CancellationToken cancellationToken = default) =>
        _settings.WriteAsync(request, context, cancellationToken);
}