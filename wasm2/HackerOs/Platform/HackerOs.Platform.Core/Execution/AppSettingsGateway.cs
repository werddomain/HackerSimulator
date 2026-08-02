using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Execution;

/// <summary>Provides one app instance's authorized canonical settings access.</summary>
public sealed class AppSettingsGateway : IAppSettingsGateway
{
    private readonly ISettingsDocumentService _settings;
    private readonly AppOperationContext _operationContext;

    /// <summary>Initializes a settings gateway bound to one app operation context.</summary>
    public AppSettingsGateway(ISettingsDocumentService settings, AppOperationContext operationContext)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
    }

    /// <inheritdoc />
    public ValueTask<SettingsReadResult> ReadAsync(VirtualPath path, CancellationToken cancellationToken = default) =>
        _settings.ReadAsync(path, _operationContext, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SettingsWriteResult> WriteAsync(SettingsWriteRequest request, CancellationToken cancellationToken = default) =>
        _settings.WriteAsync(request, _operationContext, cancellationToken);
}
