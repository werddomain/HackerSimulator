using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Lifecycle;

/// <summary>
/// Reads and writes the per-user, per-service start-mode config file at
/// <c>/home/{userName}/.config/apps/{appId}/service.conf</c> -- the live, Linux-rc-style
/// enabled/disabled state that lives alongside, but independently of, the manifest's
/// <see cref="AppManifest.AutoStart"/> preset. Every call here is trusted, system-authority IO:
/// callers are expected to have already authorized the request one layer up (same-assembly or
/// <see cref="AppCapabilities.ServicesManage"/>), the same way
/// <c>AppLifecycleOrchestrator.InvalidateAssociationDefaultsAsync</c> hand-builds a system context
/// to touch settings it doesn't own.
/// </summary>
internal static class ServiceStartModeStore
{
    private const string StartModeKey = "StartMode=";

    /// <summary>Reads the effective start mode, or <paramref name="fallback"/> when no config file exists yet.</summary>
    public static async ValueTask<ServiceStartMode> ReadAsync(
        IFileSystemService fileSystem,
        string userName,
        string appId,
        ServiceStartMode fallback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        FileSystemAuthorizationContext context = BuildContext(userName, appId);
        FileSystemResult<FileSystemContentReadHandle> result = await fileSystem.ReadAsync(
            new FileSystemReadRequest(GetConfigPath(userName, appId)), context, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            return fallback;
        }

        await using FileSystemContentReadHandle handle = result.Value;
        using StreamReader reader = new(handle.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Parse(content, fallback);
    }

    /// <summary>Writes the effective start mode, creating the app's config directory/file if needed.</summary>
    public static async ValueTask WriteAsync(
        IFileSystemService fileSystem,
        string userName,
        string appId,
        ServiceStartMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        FileSystemAuthorizationContext context = BuildContext(userName, appId);

        FileSystemMutationResult createDirectory = await fileSystem.CreateAsync(
            new FileSystemCreateRequest(
                GetDirectoryPath(userName, appId), FileSystemEntryKind.Directory, FileSystemPermissions.FromMode(0b111_000_000)),
            context,
            cancellationToken).ConfigureAwait(false);
        if (!createDirectory.Succeeded && createDirectory.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
        {
            throw new InvalidOperationException(
                $"Could not create service config directory for '{appId}': {createDirectory.Transaction.Error?.Code}.");
        }

        VirtualPath filePath = GetConfigPath(userName, appId);
        FileSystemMutationResult createFile = await fileSystem.CreateAsync(
            new FileSystemCreateRequest(filePath, FileSystemEntryKind.File, FileSystemPermissions.FromMode(0b110_000_000)),
            context,
            cancellationToken).ConfigureAwait(false);
        if (!createFile.Succeeded && createFile.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
        {
            throw new InvalidOperationException(
                $"Could not create service config file for '{appId}': {createFile.Transaction.Error?.Code}.");
        }

        FileSystemMutationResult write = await fileSystem.WriteAsync(
            new FileSystemWriteRequest(filePath), new TextContentSource($"{StartModeKey}{mode}\n"), context, cancellationToken)
            .ConfigureAwait(false);
        if (!write.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not write service config file for '{appId}': {write.Transaction.Error?.Code}.");
        }
    }

    private static VirtualPath GetDirectoryPath(string userName, string appId) =>
        VirtualPath.Parse($"/home/{userName}/.config/apps/{appId}");

    private static VirtualPath GetConfigPath(string userName, string appId) =>
        VirtualPath.Parse($"/home/{userName}/.config/apps/{appId}/service.conf");

    private static FileSystemAuthorizationContext BuildContext(string userName, string appId)
    {
        // A path under /home/{user}/.config/apps/{appId} resolves to the app-private capability
        // pair (FileSystemService.ResolveCapability), not the broader user-home one -- this is the
        // service's own private config, not general home access. UserName must be the real login
        // name (not a literal "system") so that path-prefix match succeeds.
        AppOperationContext operationContext = new()
        {
            AppId = appId,
            UserId = userName,
            UserName = userName,
            UserAuthority = AppAuthority.System,
            GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal)
            {
                AppCapabilities.FileSystemPrivateRead,
                AppCapabilities.FileSystemPrivateWrite
            },
            IsSystemOperation = true
        };

        return new FileSystemAuthorizationContext(operationContext, groupIds: [], DateTimeOffset.UtcNow);
    }

    private static ServiceStartMode Parse(string content, ServiceStartMode fallback)
    {
        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith(StartModeKey, StringComparison.Ordinal))
            {
                continue;
            }

            string value = line[StartModeKey.Length..].Trim();
            if (Enum.TryParse(value, ignoreCase: false, out ServiceStartMode mode) && Enum.IsDefined(mode))
            {
                return mode;
            }
        }

        return fallback;
    }

    private sealed class TextContentSource(string content) : IFileSystemContentSource
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(content);

        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Text();

        public long? Length => _bytes.LongLength;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(_bytes, writable: false));
        }
    }
}
