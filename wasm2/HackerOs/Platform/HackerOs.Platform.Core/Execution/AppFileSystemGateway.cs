using System.Collections.Frozen;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's authorized filesystem access by delegating to the trusted
/// <see cref="IFileSystemService"/>, constructing a fresh <see cref="FileSystemAuthorizationContext"/>
/// for every call so path/capability/handle policy is re-evaluated at the current simulated time.
/// </summary>
public sealed class AppFileSystemGateway : IAppFileSystemGateway
{
    private static readonly FrozenSet<FileSystemErrorCode> PermissionErrorCodes = new[]
    {
        FileSystemErrorCode.PermissionDenied,
        FileSystemErrorCode.CapabilityDenied,
        FileSystemErrorCode.AuthorityDenied
    }.ToFrozenSet();

    private readonly IFileSystemService _fileSystem;
    private readonly ISimulationClock _clock;
    private readonly AppOperationContext _operationContext;
    private readonly IReadOnlyList<string> _groupIds;
    private readonly FileSystemSelectedResourceHandle? _selectedHandle;
    private readonly Action<FileSystemError>? _notifyPermissionError;
    private readonly IKernelMemoryLedger? _kernelMemory;

    /// <summary>Initializes a filesystem gateway bound to one app operation context.</summary>
    /// <param name="fileSystem">The trusted filesystem service.</param>
    /// <param name="clock">Simulation clock stamping each authorization context.</param>
    /// <param name="operationContext">Who the app is: id, user, process, capabilities.</param>
    /// <param name="groupIds">The user's group ids, for permission checks.</param>
    /// <param name="selectedHandle">Optional file-picker handle granting access to one resource.</param>
    /// <param name="notifyPermissionError">Optional callback raised for every permission refusal.</param>
    /// <param name="kernelMemory">
    /// Optional ledger charged with the buffers of the app's write streams (ADR 0068); without it, or
    /// when it is not tracking, <see cref="ChargeStreamBuffer"/> returns <see cref="KernelMemoryCharge.None"/>.
    /// </param>
    public AppFileSystemGateway(
        IFileSystemService fileSystem,
        ISimulationClock clock,
        AppOperationContext operationContext,
        IReadOnlyList<string> groupIds,
        FileSystemSelectedResourceHandle? selectedHandle = null,
        Action<FileSystemError>? notifyPermissionError = null,
        IKernelMemoryLedger? kernelMemory = null)
    {
        _kernelMemory = kernelMemory;
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
        _groupIds = groupIds ?? throw new ArgumentNullException(nameof(groupIds));
        _selectedHandle = selectedHandle;
        _notifyPermissionError = notifyPermissionError;
    }

    /// <inheritdoc />
    public KernelMemoryCharge ChargeStreamBuffer() =>
        _kernelMemory is { IsTracking: true } ledger && _operationContext.ProcessId is > 0 and long pid
            ? ledger.Charge(ProcessId.FromInt64(pid), KernelMemoryKind.StreamBuffer)
            : KernelMemoryCharge.None;

    private FileSystemAuthorizationContext BuildContext() =>
        new(_operationContext, _groupIds, _clock.UtcNow, _selectedHandle);

    private void NotifyIfPermissionError(FileSystemError? error)
    {
        if (error is { } denied && PermissionErrorCodes.Contains(denied.Code))
        {
            _notifyPermissionError?.Invoke(denied);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
        FileSystemReadRequest request, CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemContentReadHandle> result =
            await _fileSystem.ReadAsync(request, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Error);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(
        FileSystemEnumerateRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.EnumerateAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> CreateAsync(
        FileSystemCreateRequest request, CancellationToken cancellationToken = default)
    {
        FileSystemMutationResult result = await _fileSystem.CreateAsync(request, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Transaction.Error);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> WriteAsync(
        FileSystemWriteRequest request, IFileSystemContentSource content, CancellationToken cancellationToken = default)
    {
        FileSystemMutationResult result = await _fileSystem.WriteAsync(request, content, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Transaction.Error);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> MoveAsync(
        FileSystemMoveRequest request, CancellationToken cancellationToken = default)
    {
        FileSystemMutationResult result = await _fileSystem.MoveAsync(request, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Transaction.Error);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> CopyAsync(
        FileSystemCopyRequest request, CancellationToken cancellationToken = default)
    {
        FileSystemMutationResult result = await _fileSystem.CopyAsync(request, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Transaction.Error);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> DeleteAsync(
        FileSystemDeleteRequest request, CancellationToken cancellationToken = default)
    {
        FileSystemMutationResult result = await _fileSystem.DeleteAsync(request, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Transaction.Error);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
        FileSystemStatRequest request, CancellationToken cancellationToken = default) =>
        _fileSystem.StatAsync(request, BuildContext(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask<FileSystemMutationResult> SetPermissionsAsync(
        FileSystemSetPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        FileSystemMutationResult result = await _fileSystem.SetPermissionsAsync(request, BuildContext(), cancellationToken);
        NotifyIfPermissionError(result.Transaction.Error);
        return result;
    }

    /// <inheritdoc />
    public IAppFileSystemGateway WithSelectedHandle(FileSystemSelectedResourceHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return new AppFileSystemGateway(_fileSystem, _clock, _operationContext, _groupIds, handle, _notifyPermissionError);
    }
}
