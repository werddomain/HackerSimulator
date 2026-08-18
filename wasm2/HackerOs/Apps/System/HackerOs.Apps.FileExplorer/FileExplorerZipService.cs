using System.IO.Compression;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// The one implementation of zip compress/extract against a virtual filesystem — shared between
/// <c>FileExplorerWindow</c>'s toolbar (Compress/Extract) and <see cref="ZipFileContextMenuProvider"/>'s
/// "UnZip Here…" context-menu item (<c>INT-003</c>), so there is exactly one unzip implementation, not two.
/// </summary>
internal static class FileExplorerZipService
{
    public static async Task AddFileToZipAsync(
        IAppFileSystemGateway fileSystem, ZipArchive archive, VirtualPath filePath, string entryName, CancellationToken cancellationToken)
    {
        FileSystemResult<FileSystemContentReadHandle> result = await fileSystem.ReadAsync(
            new FileSystemReadRequest(filePath), cancellationToken);

        if (result.Succeeded && result.Value is not null)
        {
            await using FileSystemContentReadHandle handle = result.Value;
            ZipArchiveEntry zipEntry = archive.CreateEntry(entryName);
            using Stream entryStream = zipEntry.Open();
            await handle.Content.CopyToAsync(entryStream, cancellationToken);
        }
    }

    public static async Task AddDirectoryToZipAsync(
        IAppFileSystemGateway fileSystem, ZipArchive archive, VirtualPath dirPath, string currentZipPath, CancellationToken cancellationToken)
    {
        FileSystemResult<FileSystemDirectorySnapshot> result = await fileSystem.EnumerateAsync(
            new FileSystemEnumerateRequest(dirPath), cancellationToken);

        if (result.Succeeded && result.Value is not null)
        {
            foreach (FileSystemDirectoryItem item in result.Value.Entries)
            {
                VirtualPath childPath = VirtualPath.Parse(dirPath.Value.TrimEnd('/') + "/" + item.Name.Value);
                string zipEntryPath = currentZipPath + "/" + item.Name.Value;

                if (item.Metadata.Kind == FileSystemEntryKind.File)
                {
                    await AddFileToZipAsync(fileSystem, archive, childPath, zipEntryPath, cancellationToken);
                }
                else if (item.Metadata.Kind == FileSystemEntryKind.Directory)
                {
                    await AddDirectoryToZipAsync(fileSystem, archive, childPath, zipEntryPath, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Extracts <paramref name="zipPath"/> into <paramref name="destinationDirectory"/> (created fresh —
    /// fails if it already exists). Returns a human-readable error message, or <see langword="null"/> on success.
    /// </summary>
    public static async Task<string?> ExtractAsync(
        IAppFileSystemGateway fileSystem, VirtualPath zipPath, VirtualPath destinationDirectory, CancellationToken cancellationToken)
    {
        FileSystemResult<FileSystemContentReadHandle> readResult = await fileSystem.ReadAsync(
            new FileSystemReadRequest(zipPath), cancellationToken);
        if (!readResult.Succeeded || readResult.Value is null)
        {
            return "Could not read the archive.";
        }

        using MemoryStream ms = new();
        await using (FileSystemContentReadHandle handle = readResult.Value)
        {
            await handle.Content.CopyToAsync(ms, cancellationToken);
        }
        ms.Position = 0;

        FileSystemResult<FileSystemEntrySnapshot> destStat = await fileSystem.StatAsync(
            new FileSystemStatRequest(destinationDirectory), cancellationToken);
        if (destStat.Succeeded)
        {
            string folderName = destinationDirectory.Value[(destinationDirectory.Value.LastIndexOf('/') + 1)..];
            return $"'{folderName}' already exists.";
        }

        FileSystemMutationResult dirCreate = await fileSystem.CreateAsync(
            new FileSystemCreateRequest(destinationDirectory, FileSystemEntryKind.Directory, FileSystemPermissions.FromMode(0b111_101_101)),
            cancellationToken);
        if (!dirCreate.Succeeded)
        {
            return "Could not create destination folder.";
        }

        HashSet<string> ensuredDirs = new(StringComparer.Ordinal) { destinationDirectory.Value };

        try
        {
            using ZipArchive archive = new(ms, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (string.IsNullOrEmpty(entryName))
                {
                    continue;
                }

                string entryFullPath = destinationDirectory.Value + "/" + entryName.TrimEnd('/');

                if (entryName.EndsWith('/'))
                {
                    await EnsureDirectoryAsync(fileSystem, entryFullPath, ensuredDirs, cancellationToken);
                    continue;
                }

                int lastSlash = entryFullPath.LastIndexOf('/');
                string parentPath = lastSlash > 0 ? entryFullPath[..lastSlash] : destinationDirectory.Value;
                await EnsureDirectoryAsync(fileSystem, parentPath, ensuredDirs, cancellationToken);

                VirtualPath entryPath = VirtualPath.Parse(entryFullPath);
                FileSystemMutationResult fileCreate = await fileSystem.CreateAsync(
                    new FileSystemCreateRequest(entryPath, FileSystemEntryKind.File, FileSystemPermissions.FromMode(0b110_100_100)),
                    cancellationToken);
                if (!fileCreate.Succeeded)
                {
                    continue;
                }

                using MemoryStream entryBuffer = new();
                await using (Stream entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(entryBuffer, cancellationToken);
                }
                entryBuffer.Position = 0;

                MemoryStreamContentSource entrySource = new(FileSystemContentDescriptor.Binary(), entryBuffer);
                await fileSystem.WriteAsync(new FileSystemWriteRequest(entryPath), entrySource, cancellationToken);
            }
        }
        catch (InvalidDataException)
        {
            return "The archive is not a valid zip file.";
        }

        return null;
    }

    private static async Task EnsureDirectoryAsync(
        IAppFileSystemGateway fileSystem, string dirPath, HashSet<string> ensuredDirs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(dirPath) || ensuredDirs.Contains(dirPath))
        {
            return;
        }

        int lastSlash = dirPath.LastIndexOf('/');
        string parent = lastSlash > 0 ? dirPath[..lastSlash] : string.Empty;
        if (!string.IsNullOrEmpty(parent))
        {
            await EnsureDirectoryAsync(fileSystem, parent, ensuredDirs, cancellationToken);
        }

        VirtualPath path = VirtualPath.Parse(dirPath);
        FileSystemResult<FileSystemEntrySnapshot> stat = await fileSystem.StatAsync(new FileSystemStatRequest(path), cancellationToken);
        if (!stat.Succeeded)
        {
            await fileSystem.CreateAsync(
                new FileSystemCreateRequest(path, FileSystemEntryKind.Directory, FileSystemPermissions.FromMode(0b111_101_101)), cancellationToken);
        }

        ensuredDirs.Add(dirPath);
    }
}

/// <summary>Wraps an in-memory buffer as an <see cref="IFileSystemContentSource"/> for a single write.</summary>
internal sealed class MemoryStreamContentSource : IFileSystemContentSource
{
    private readonly MemoryStream _stream;

    public MemoryStreamContentSource(FileSystemContentDescriptor descriptor, MemoryStream stream)
    {
        Descriptor = descriptor;
        _stream = stream;
        Length = stream.Length;
    }

    public FileSystemContentDescriptor Descriptor { get; }
    public long? Length { get; }

    public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        _stream.Position = 0;
        return ValueTask.FromResult<Stream>(_stream);
    }
}
