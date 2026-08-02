using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's authorized filesystem access by delegating to the trusted
/// <see cref="IFileSystemService"/>, constructing a fresh <see cref="FileSystemAuthorizationContext"/>
/// for every call so path/capability/handle policy is re-evaluated at the current simulated time.
/// </summary>
public sealed class AppFileSystemGateway : IAppFileSystemGateway
{
    private readonly IFileSystemService _fileSystem;
    private readonly ISimulationClock _clock;
    private readonly AppOperationContext _operationContext;
    private readonly IReadOnlyList<string> _groupIds;
    private readonly FileSystemSelectedResourceHandle? _selectedHandle;

    /// <summary>Initializes a filesystem gateway bound to one app operation context.</summary>
    public AppFileSystemGateway(
        IFileSystemService fileSystem,
        ISimulationClock clock,
        AppOperationContext operationContext,
        IReadOnlyList<string> groupIds,
        FileSystemSelectedResourceHandle? selectedHandle = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
        _groupIds = groupIds ?? throw new ArgumentNullException(nameof(groupIds));
        _selectedHandle = selectedHandle;
    }

    private FileSystemAuthorizationContext BuildContext() =>
        new(_operationContext, _groupIds, _clock.UtcNow, _selectedHandle);

    /// <inheritdoc />
    public ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.ReadAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.EnumerateAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> CreateAsync(
        FileSystemCreateRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.CreateAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> WriteAsync(
        FileSystemWriteRequest request, IFileSystemContentSource content, CancellationToken cancellationToken = default) =>
        _fileSystem.WriteAsync(request, content, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> MoveAsync(
        FileSystemMoveRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.MoveAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> CopyAsync(
        FileSystemCopyRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.CopyAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> DeleteAsync(
        FileSystemDeleteRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.DeleteAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.StatAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<FileSystemMutationResult> SetPermissionsAsync(
        FileSystemSetPermissionsRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.SetPermissionsAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public IAppFileSystemGateway WithSelectedHandle(FileSystemSelectedResourceHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return new AppFileSystemGateway(_fileSystem, _clock, _operationContext, _groupIds, handle);
    }
}
