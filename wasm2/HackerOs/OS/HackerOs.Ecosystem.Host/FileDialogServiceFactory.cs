using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Blazor.Dialogs;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Ecosystem;

/// <summary>Creates a file-dialog queue after authentication binds it to a real session.</summary>
public sealed class FileDialogServiceFactory
{
    private readonly IFileSystemSelectedResourceHandleRegistry _handleRegistry;

    /// <summary>Initializes the factory over the process-wide selected-resource registry.</summary>
    public FileDialogServiceFactory(IFileSystemSelectedResourceHandleRegistry handleRegistry)
    {
        _handleRegistry = handleRegistry ?? throw new ArgumentNullException(nameof(handleRegistry));
    }

    /// <summary>Creates one disposable FIFO dialog coordinator for the supplied session.</summary>
    public IFileDialogService Create(SessionId sessionId) =>
        new FileDialogCoordinator(sessionId, _handleRegistry);
}