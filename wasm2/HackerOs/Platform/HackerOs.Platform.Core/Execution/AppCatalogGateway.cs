using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Apps;
using HackerOs.Platform.Core.Apps;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Lifecycle;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted <see cref="IAppCatalogGateway"/> (ADR 0056, section 2): reads <see cref="AppCatalog"/>
/// through the launch checkpoint <see cref="IAppEnablementRegistry"/>, so an app uninstalled by
/// <c>apps.config</c> (ADR 0048) is hidden here as everywhere else. No capability is checked: the
/// list of installed apps is public.
/// </summary>
/// <remarks>
/// <see cref="Changed"/> forwards <see cref="IAppInstallationService.Changed"/>. The installation
/// service depends (through the lifecycle orchestrator) on the context factory that builds this
/// gateway, so it is resolved lazily, on the first subscription.
/// </remarks>
internal sealed class AppCatalogGateway : IAppCatalogGateway
{
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry _enablement;
    private readonly Func<FileAssociationResolver>? _resolver;
    private readonly Func<IAppInstallationService>? _installation;
    private readonly AppAmbientContextBinding _ambient = new();
    private readonly Lock _gate = new();
    private EventHandler? _changed;
    private IAppInstallationService? _subscribedTo;

    /// <summary>Creates the gateway for one app instance.</summary>
    public AppCatalogGateway(
        AppCatalog catalog,
        IAppEnablementRegistry enablement,
        Func<FileAssociationResolver>? resolver,
        Func<IAppInstallationService>? installation)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _resolver = resolver;
        _installation = installation;
    }

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add
        {
            lock (_gate)
            {
                _changed += value;
                if (_subscribedTo is null && _installation is not null)
                {
                    _subscribedTo = _installation();
                    _subscribedTo.Changed += OnInstallationChanged;
                }
            }
        }
        remove
        {
            lock (_gate)
            {
                _changed -= value;
                if (_changed is null && _subscribedTo is not null)
                {
                    _subscribedTo.Changed -= OnInstallationChanged;
                    _subscribedTo = null;
                }
            }
        }
    }

    /// <summary>Binds the app context <see cref="Changed"/> handlers run under; called once by the factory.</summary>
    internal void BindAmbientContext(HackerOs.AppSdk.IAppExecutionContext context) => _ambient.Bind(context);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<HackAppInfo> installed = [.. _catalog.Manifests.Values
            .Where(manifest => _enablement.IsEnabled(manifest.Id))
            .OrderBy(manifest => manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manifest => manifest.Id, StringComparer.Ordinal)
            .Select(Describe)];
        return ValueTask.FromResult(installed);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<HackAppInfo>> GetHandlersAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        if (_resolver is null)
        {
            throw new NotSupportedException("This execution context factory was not configured with a file association resolver.");
        }

        FileHandlerCandidates candidates = await _resolver().GetHandlerAppIdsAsync(path, mediaType, cancellationToken).ConfigureAwait(false);

        // The default (if any) stays first; the others are sorted by name, as a user reads them.
        IEnumerable<HackAppInfo> others = candidates.AppIds
            .Where(id => !string.Equals(id, candidates.DefaultAppId, StringComparison.Ordinal))
            .Select(id => Describe(_catalog.Manifests[id]))
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase);
        return candidates.DefaultAppId is { } defaultAppId
            ? [Describe(_catalog.Manifests[defaultAppId]), .. others]
            : [.. others];
    }

    private HackAppInfo Describe(AppManifest manifest) => HackAppInfo.FromManifest(manifest, _catalog.IsSystemApp(manifest.Id));

    private void OnInstallationChanged(object? sender, EventArgs e)
    {
        EventHandler? handlers;
        lock (_gate)
        {
            handlers = _changed;
        }

        if (handlers is not null)
        {
            _ambient.Wrap(() => handlers(this, EventArgs.Empty))();
        }
    }
}
