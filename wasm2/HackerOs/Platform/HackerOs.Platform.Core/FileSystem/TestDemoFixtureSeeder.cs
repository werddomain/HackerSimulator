using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.FileSystem;

/// <summary>
/// Seeds a deterministic file/folder fixture into the signed-in user's home directory.
/// Only ever invoked from the "Test Demo" control, which is itself gated to E2E test
/// runs (see <c>docs/e2e-test-demo-mode.md</c>). Writes go through the trusted platform
/// <see cref="IFileSystemService"/> as a system operation, the same way
/// <see cref="FileSystemSeeder"/> provisions the default home layout at login.
/// </summary>
public sealed class TestDemoFixtureSeeder(IFileSystemService fileSystem)
{
    private readonly IFileSystemService _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>Relative fixture root created under the user's home directory.</summary>
    public const string FixtureRootName = "e2e-fixtures";

    /// <summary>Name of the plain fixture file used by download/zip content assertions.</summary>
    public const string AlphaFileName = "alpha.txt";

    /// <summary>Content written to <see cref="AlphaFileName"/>.</summary>
    public const string AlphaContent = "Alpha fixture file for HackerOS E2E tests.\n";

    /// <summary>Name of the fixture file the Text Editor E2E scenario opens and overwrites.</summary>
    public const string EditableFileName = "editable.txt";

    /// <summary>Content <see cref="EditableFileName"/> is seeded with, before the editor rewrites it.</summary>
    public const string EditableOriginalContent = "Original content before edit.\n";

    /// <summary>Name of the nested fixture directory used to assert zip folder structure.</summary>
    public const string NestedDirectoryName = "notes";

    /// <summary>Name of the fixture file inside <see cref="NestedDirectoryName"/>.</summary>
    public const string NestedFileName = "beta.md";

    /// <summary>Content written to the nested fixture file.</summary>
    public const string NestedContent = "# Beta\n\nNested fixture file.\n";

    /// <summary>Creates (or resets) the fixture tree for the given signed-in principal.</summary>
    /// <returns>The absolute virtual path of the seeded fixture root directory.</returns>
    public async Task<string> SeedAsync(AuthenticatedPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string userId = principal.LoginName.Value;
        FileSystemAuthorizationContext context = CreateContext(userId, principal.PrimaryGroupId.ToString());

        string root = $"/home/{userId}/Documents/{FixtureRootName}";
        string nested = $"{root}/{NestedDirectoryName}";

        await EnsureDirectoryAsync(root, context, cancellationToken);
        await EnsureDirectoryAsync(nested, context, cancellationToken);
        await WriteFileAsync($"{root}/{AlphaFileName}", AlphaContent, context, cancellationToken);
        await WriteFileAsync($"{root}/{EditableFileName}", EditableOriginalContent, context, cancellationToken);
        await WriteFileAsync($"{nested}/{NestedFileName}", NestedContent, context, cancellationToken);

        return root;
    }

    private async Task EnsureDirectoryAsync(
        string pathValue,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        VirtualPath path = VirtualPath.Parse(pathValue);
        FileSystemResult<FileSystemEntrySnapshot> existing = await _fileSystem.StatAsync(
            new FileSystemStatRequest(path), context, cancellationToken);
        if (existing.Succeeded)
        {
            return;
        }

        VirtualPath parent = GetParent(path);
        FileSystemResult<FileSystemEntrySnapshot> parentStat = await _fileSystem.StatAsync(
            new FileSystemStatRequest(parent), context, cancellationToken);
        if (!parentStat.Succeeded)
        {
            throw new InvalidOperationException(
                $"Cannot seed fixture directory '{path}': parent '{parent}' is not available ({parentStat.Error?.Code}).");
        }

        FileSystemMutationResult created = await _fileSystem.CreateAsync(
            new FileSystemCreateRequest(
                path,
                FileSystemEntryKind.Directory,
                FileSystemPermissions.FromMode(0x01C0),
                parentStat.Value!.Metadata.Revision),
            context,
            cancellationToken);
        if (!created.Succeeded && created.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
        {
            throw new InvalidOperationException(
                $"Cannot create fixture directory '{path}': {created.Transaction.Error?.Code}.");
        }
    }

    private async Task WriteFileAsync(
        string pathValue,
        string content,
        FileSystemAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        VirtualPath path = VirtualPath.Parse(pathValue);
        FileSystemResult<FileSystemEntrySnapshot> existing = await _fileSystem.StatAsync(
            new FileSystemStatRequest(path), context, cancellationToken);

        long revision;
        if (existing.Succeeded)
        {
            revision = existing.Value!.Metadata.Revision;
        }
        else
        {
            VirtualPath parent = GetParent(path);
            FileSystemResult<FileSystemEntrySnapshot> parentStat = await _fileSystem.StatAsync(
                new FileSystemStatRequest(parent), context, cancellationToken);
            if (!parentStat.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Cannot seed fixture file '{path}': parent '{parent}' is not available ({parentStat.Error?.Code}).");
            }

            FileSystemMutationResult created = await _fileSystem.CreateAsync(
                new FileSystemCreateRequest(
                    path,
                    FileSystemEntryKind.File,
                    FileSystemPermissions.FromMode(0x01A4),
                    parentStat.Value!.Metadata.Revision),
                context,
                cancellationToken);
            if (!created.Succeeded || created.Entry is null)
            {
                throw new InvalidOperationException(
                    $"Cannot create fixture file '{path}': {created.Transaction.Error?.Code}.");
            }

            revision = created.Entry.Metadata.Revision;
        }

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        using MemoryStream stream = new(bytes);
        FixtureContentSource source = new(stream);
        FileSystemMutationResult written = await _fileSystem.WriteAsync(
            new FileSystemWriteRequest(path, revision), source, context, cancellationToken);
        if (!written.Succeeded)
        {
            throw new InvalidOperationException(
                $"Cannot write fixture file '{path}': {written.Transaction.Error?.Code}.");
        }
    }

    private static FileSystemAuthorizationContext CreateContext(string userId, string groupId)
    {
        AppOperationContext operation = new()
        {
            AppId = "org.hackeros.kernel",
            UserId = userId,
            UserAuthority = AppAuthority.System,
            GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
            IsSystemOperation = true
        };
        return new FileSystemAuthorizationContext(operation, [groupId], DateTimeOffset.UtcNow);
    }

    private static VirtualPath GetParent(VirtualPath path)
    {
        int separator = path.Value.LastIndexOf('/');
        return separator <= 0 ? VirtualPath.Parse("/") : VirtualPath.Parse(path.Value[..separator]);
    }

    private sealed class FixtureContentSource(MemoryStream stream) : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Text("text/plain", "utf-8");
        public long? Length { get; } = stream.Length;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            stream.Position = 0;
            return ValueTask.FromResult<Stream>(stream);
        }
    }
}
