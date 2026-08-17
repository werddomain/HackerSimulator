using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;
using System.Text;

namespace HackerOs.Commands.Nano;

/// <summary>
/// Nano-style interactive terminal text editor backed by the capability-checked VFS.
/// </summary>
public sealed class NanoCommand : TerminalAppBase
{
    private const int MaxDocumentBytes = 1024 * 1024;

    /// <summary>Static manifest used for test validation without a DI container.</summary>
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.nano",
        Name = "nano",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Terminal interactive text editor command",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Nano.dll", "HackerOs.Commands.Nano.NanoCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("system", AppLaunchVisibility.Hidden, []),
        Terminal = new TerminalCommandManifest("nano", [], "nano [file]"),
        Capabilities = ["filesystem.user-home.read", "filesystem.user-home.write"],
        Resources = AppResourceProfileManifest.None,
        SingleInstancePerUser = false
    };

    /// <summary>Initializes the nano command with its validated manifest.</summary>
    public NanoCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc/>
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.FullScreen is null)
        {
            context.StandardError.WriteLine("nano: this terminal does not provide full-screen editing");
            return 2;
        }

        string targetFile = context.Arguments.Count > 0 ? context.Arguments[0] : "new_file.txt";
        VirtualPath path = VirtualPath.Parse(ResolvePath(context.WorkingDirectory, targetFile));
        LoadResult load = await LoadAsync(context, path, cancellationToken);
        if (!load.Succeeded)
        {
            context.StandardError.WriteLine($"nano: {targetFile}: {load.Error}");
            return 1;
        }

        EditorState state = new(path, load.Content!, load.Revision);
        await context.FullScreen.EnterAlternateScreenAsync(cancellationToken);
        try
        {
            while (true)
            {
                await RenderAsync(context.FullScreen, state, cancellationToken);
                TerminalKeyEvent key = await context.FullScreen.ReadKeyAsync(cancellationToken);

                if (key.Control && IsCharacter(key, "o"))
                {
                    if (key.Shift)
                    {
                        string? saveAs = await PromptAsync(
                            context.FullScreen, state, "Save as: ", cancellationToken);
                        if (!string.IsNullOrWhiteSpace(saveAs))
                        {
                            state.Path = VirtualPath.Parse(ResolvePath(context.WorkingDirectory, saveAs));
                            state.Revision = null;
                        }
                        else
                        {
                            state.Status = "Save As cancelled";
                            continue;
                        }
                    }
                    state.Status = await SaveAsync(context, state, cancellationToken)
                        ? $"Wrote {state.Path.Value}"
                        : "Write failed";
                    continue;
                }

                if (key.Control && IsCharacter(key, "x"))
                {
                    if (!state.Dirty || await ConfirmExitAsync(context, context.FullScreen, state, cancellationToken))
                        return 0;
                    continue;
                }

                ApplyKey(state, key);
            }
        }
        finally
        {
            // Cleanup must not be skipped merely because command cancellation fired.
            await context.FullScreen.LeaveAlternateScreenAsync(CancellationToken.None);
        }
    }

    private static async ValueTask<LoadResult> LoadAsync(
        TerminalExecutionContext context, VirtualPath path, CancellationToken cancellationToken)
    {
        FileSystemResult<FileSystemContentReadHandle> result = await context.App.FileSystem.ReadAsync(
            new FileSystemReadRequest(path), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return result.Error?.Code == FileSystemErrorCode.NotFound
                ? new LoadResult(true, string.Empty, null, null)
                : new LoadResult(false, null, null, result.Error?.Code.ToString() ?? "read failed");
        }

        await using FileSystemContentReadHandle handle = result.Value;
        if (handle.Entry.Metadata is FileMetadata fileMetadata && fileMetadata.Length > MaxDocumentBytes)
            return new LoadResult(false, null, null, $"file exceeds {MaxDocumentBytes} bytes");

        using StreamReader reader = new(handle.Content, Encoding.UTF8, true, leaveOpen: true);
        string content = await reader.ReadToEndAsync(cancellationToken);
        return Encoding.UTF8.GetByteCount(content) > MaxDocumentBytes
            ? new LoadResult(false, null, null, $"file exceeds {MaxDocumentBytes} bytes")
            : new LoadResult(true, content, handle.Entry.Metadata.Revision, null);
    }

    private static async ValueTask<bool> SaveAsync(
        TerminalExecutionContext context, EditorState state, CancellationToken cancellationToken)
    {
        string content = state.GetContent();
        if (Encoding.UTF8.GetByteCount(content) > MaxDocumentBytes)
        {
            state.Status = $"Buffer exceeds {MaxDocumentBytes} bytes";
            return false;
        }

        if (state.Revision is null)
        {
            VirtualPath parent = ParentOf(state.Path);
            FileSystemResult<FileSystemEntrySnapshot> parentStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(parent), cancellationToken);
            if (!parentStat.Succeeded || parentStat.Value is null)
                return false;

            FileSystemMutationResult created = await context.App.FileSystem.CreateAsync(
                new FileSystemCreateRequest(state.Path, FileSystemEntryKind.File,
                    FileSystemPermissions.FromMode(0b110_100_100), parentStat.Value.Metadata.Revision),
                cancellationToken);
            if (!created.Succeeded || created.Entry is null)
                return false;
            state.Revision = created.Entry.Metadata.Revision;
        }

        FileSystemMutationResult written = await context.App.FileSystem.WriteAsync(
            new FileSystemWriteRequest(state.Path, state.Revision.Value),
            new Utf8TextSource(content), cancellationToken);
        if (!written.Succeeded || written.Entry is null)
            return false;

        state.Revision = written.Entry.Metadata.Revision;
        state.Dirty = false;
        return true;
    }

    private static async ValueTask<bool> ConfirmExitAsync(
        TerminalExecutionContext context,
        IFullScreenTerminalSession terminal,
        EditorState state,
        CancellationToken cancellationToken)
    {
        state.Status = "Save modified buffer? Y Yes  N No  C Cancel";
        await RenderAsync(terminal, state, cancellationToken);
        while (true)
        {
            TerminalKeyEvent key = await terminal.ReadKeyAsync(cancellationToken);
            if (IsCharacter(key, "n"))
                return true;
            if (IsCharacter(key, "c") || key.Key is TerminalKey.Escape)
            {
                state.Status = "Exit cancelled";
                return false;
            }
            if (IsCharacter(key, "y"))
            {
                bool saved = await SaveAsync(context, state, cancellationToken);
                state.Status = saved ? $"Wrote {state.Path.Value}" : "Write failed; exit cancelled";
                return saved;
            }
        }
    }

    private static async ValueTask<string?> PromptAsync(
        IFullScreenTerminalSession terminal,
        EditorState state,
        string prompt,
        CancellationToken cancellationToken)
    {
        StringBuilder input = new();
        while (true)
        {
            state.Status = prompt + input;
            await RenderAsync(terminal, state, cancellationToken);
            TerminalKeyEvent key = await terminal.ReadKeyAsync(cancellationToken);
            if (key.Key is TerminalKey.Enter)
                return input.ToString();
            if (key.Key is TerminalKey.Escape)
                return null;
            if (key.Key is TerminalKey.Backspace && input.Length > 0)
                input.Length--;
            else if (key.Key is TerminalKey.Character && !key.Control && !key.Alt && key.Text is not null)
                input.Append(key.Text);
        }
    }

    private static void ApplyKey(EditorState state, TerminalKeyEvent key)
    {
        StringBuilder line = state.Lines[state.Row];
        switch (key.Key)
        {
            case TerminalKey.Character when !key.Control && !key.Alt && !string.IsNullOrEmpty(key.Text):
                line.Insert(state.Column, key.Text);
                state.Column += key.Text.Length;
                state.Dirty = true;
                break;
            case TerminalKey.Enter:
                string remainder = line.ToString(state.Column, line.Length - state.Column);
                line.Remove(state.Column, line.Length - state.Column);
                state.Lines.Insert(++state.Row, new StringBuilder(remainder));
                state.Column = 0;
                state.Dirty = true;
                break;
            case TerminalKey.Backspace when state.Column > 0:
                line.Remove(--state.Column, 1);
                state.Dirty = true;
                break;
            case TerminalKey.Backspace when state.Row > 0:
                int previousLength = state.Lines[state.Row - 1].Length;
                state.Lines[state.Row - 1].Append(line);
                state.Lines.RemoveAt(state.Row--);
                state.Column = previousLength;
                state.Dirty = true;
                break;
            case TerminalKey.Delete when state.Column < line.Length:
                line.Remove(state.Column, 1);
                state.Dirty = true;
                break;
            case TerminalKey.Delete when state.Row + 1 < state.Lines.Count:
                line.Append(state.Lines[state.Row + 1]);
                state.Lines.RemoveAt(state.Row + 1);
                state.Dirty = true;
                break;
            case TerminalKey.ArrowLeft when state.Column > 0:
                state.Column--;
                break;
            case TerminalKey.ArrowRight when state.Column < line.Length:
                state.Column++;
                break;
            case TerminalKey.ArrowUp when state.Row > 0:
                state.Row--;
                state.Column = Math.Min(state.Column, state.Lines[state.Row].Length);
                break;
            case TerminalKey.ArrowDown when state.Row + 1 < state.Lines.Count:
                state.Row++;
                state.Column = Math.Min(state.Column, state.Lines[state.Row].Length);
                break;
            case TerminalKey.Home:
                state.Column = 0;
                break;
            case TerminalKey.End:
                state.Column = line.Length;
                break;
        }
    }

    private static ValueTask RenderAsync(
        IFullScreenTerminalSession terminal, EditorState state, CancellationToken cancellationToken)
    {
        int width = Math.Max(1, terminal.Viewport.Columns);
        int height = Math.Max(3, terminal.Viewport.Rows);
        int bodyRows = Math.Max(1, height - 2);
        if (state.Row < state.TopRow)
            state.TopRow = state.Row;
        if (state.Row >= state.TopRow + bodyRows)
            state.TopRow = state.Row - bodyRows + 1;

        List<string> lines = [Fit($"  GNU nano  HackerOS  {state.Path.Value}{(state.Dirty ? " *" : string.Empty)}", width)];
        for (int index = 0; index < bodyRows; index++)
        {
            int sourceRow = state.TopRow + index;
            lines.Add(sourceRow < state.Lines.Count ? Fit(state.Lines[sourceRow].ToString(), width) : string.Empty);
        }
        lines.Add(Fit(state.Status ?? "^O Save   ^X Exit", width));
        state.Status = null;
        return terminal.RenderAsync(new TerminalScreenFrame(
            lines,
            new TerminalCursor(Math.Min(state.Column, width - 1), state.Row - state.TopRow + 1),
            $"Nano editor, {state.Path.Value}"), cancellationToken);
    }

    private static bool IsCharacter(TerminalKeyEvent key, string value) =>
        key.Key is TerminalKey.Character
        && string.Equals(key.Text, value, StringComparison.OrdinalIgnoreCase);

    private static string Fit(string value, int width) =>
        value.Length <= width ? value : value[..width];

    private static string ResolvePath(string cwd, string target) =>
        target.StartsWith('/') ? target : $"{cwd.TrimEnd('/')}/{target}";

    private static VirtualPath ParentOf(VirtualPath path)
    {
        int separator = path.Value.LastIndexOf('/');
        return VirtualPath.Parse(separator <= 0 ? "/" : path.Value[..separator]);
    }

    private sealed record LoadResult(bool Succeeded, string? Content, long? Revision, string? Error);

    private sealed class EditorState
    {
        public EditorState(VirtualPath path, string content, long? revision)
        {
            Path = path;
            Revision = revision;
            Lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
                .Select(static line => new StringBuilder(line)).ToList();
            if (Lines.Count == 0)
                Lines.Add(new StringBuilder());
        }

        public VirtualPath Path { get; set; }
        public List<StringBuilder> Lines { get; }
        public long? Revision { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int TopRow { get; set; }
        public bool Dirty { get; set; }
        public string? Status { get; set; }
        public string GetContent() => string.Join('\n', Lines.Select(static line => line.ToString()));
    }

    private sealed class Utf8TextSource(string content) : IFileSystemContentSource
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
