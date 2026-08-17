using System.Collections.Immutable;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Apps.Calculator;
using HackerOs.Apps.HackPaint;
using HackerOs.Commands.Alias;
using HackerOs.Commands.Clear;
using HackerOs.Commands.Diff;
using HackerOs.Commands.Grep;
using HackerOs.Commands.Head;
using HackerOs.Commands.Help;
using HackerOs.Commands.Launch;
using HackerOs.Commands.Ps;
using HackerOs.Commands.Sort;
using HackerOs.Commands.Tail;
using HackerOs.Commands.Wc;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using Xunit;

namespace HackerOs.Commands.Wave5.Tests;

/// <summary>
/// Unit test suite for Phase 4 Wave 5 (Utility Apps & Terminal Commands).
/// Validates CalculatorEngine, PaintCanvasState, grep, head/tail, sort, wc,
/// diff, ps, launch, clear, help/man, and alias commands.
/// </summary>
public sealed class Wave5Tests
{
    // ── Calculator Engine Unit Tests ──────────────────────────────────────

    [Fact]
    public void CalculatorEngine_PerformsBasicArithmetic()
    {
        var engine = new CalculatorEngine();
        engine.InputDigit("5");
        engine.HandleOperator("+");
        engine.InputDigit("3");
        engine.HandleEquals();

        Assert.Equal("8", engine.CurrentValue);
    }

    [Fact]
    public void CalculatorEngine_HandlesSquareRoot_And_Memory()
    {
        var engine = new CalculatorEngine();
        engine.InputDigit("9");
        engine.SquareRoot();
        Assert.Equal("3", engine.CurrentValue);

        engine.MemoryAdd();
        engine.AllClear();
        Assert.Equal("0", engine.CurrentValue);

        engine.MemoryRecall();
        Assert.Equal("3", engine.CurrentValue);
    }

    // ── Paint Canvas State Unit Tests ──────────────────────────────────────

    [Fact]
    public void PaintCanvasState_NewDocument_And_Rotate90()
    {
        var state = new PaintCanvasState();
        state.NewDocument(100, 50);

        Assert.Equal(100, state.Width);
        Assert.Equal(50, state.Height);

        state.Rotate90();
        Assert.Equal(50, state.Width);
        Assert.Equal(100, state.Height);
        Assert.True(state.CanUndo);

        state.Undo();
        Assert.Equal(100, state.Width);
        Assert.Equal(50, state.Height);
    }

    [Fact]
    public void PaintCanvasState_DrawUndoRedo_RestoresActualPixels()
    {
        var state = new PaintCanvasState();
        state.NewDocument(16, 16);
        var before = state.GetCurrentBuffer();

        state.DrawLine(2, 2, 2, 2, "#ff0000", 1);
        var painted = state.GetCurrentBuffer();
        Assert.NotEqual(before, painted);
        Assert.Equal((byte)255, painted[(2 * 16 + 2) * 4]);

        state.Undo();
        Assert.Equal(before, state.GetCurrentBuffer());
        state.Redo();
        Assert.Equal(painted, state.GetCurrentBuffer());
    }

    [Fact]
    public void PaintCanvasState_CropAndRotate_PreservePixelContent()
    {
        var state = new PaintCanvasState();
        state.NewDocument(16, 16);
        state.DrawLine(3, 4, 3, 4, "#0000ff", 1);
        state.Crop(2, 3, 4, 5);

        Assert.Equal(4, state.Width);
        Assert.Equal(5, state.Height);
        Assert.Equal((byte)255, state.GetCurrentBuffer()[(1 * 4 + 1) * 4 + 2]);

        state.Rotate90();
        Assert.Equal(5, state.Width);
        Assert.Equal(4, state.Height);
        Assert.Contains((byte)255, state.GetCurrentBuffer());
    }

    [Fact]
    public void PaintCanvasState_Pan_DoesNotMutatePixelsOrHistory()
    {
        var state = new PaintCanvasState();
        state.NewDocument(16, 16);
        var pixels = state.GetCurrentBuffer();
        state.PanBy(12, -4);

        Assert.Equal(12, state.PanX);
        Assert.Equal(-4, state.PanY);
        Assert.False(state.CanUndo);
        Assert.Equal(pixels, state.GetCurrentBuffer());
    }

    // ── Command Unit Tests ────────────────────────────────────────────────

    [Fact]
    public async Task GrepCommand_FiltersMatchingLines()
    {
        var cmd = new GrepCommand(GrepCommand.StaticManifest);
        using var stdin = new StringReader("apple\nbanana\ncherry\nAPRICOT\n");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext(["-i", "ap"], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains("apple", output);
        Assert.Contains("APRICOT", output);
        Assert.DoesNotContain("banana", output);
    }

    [Fact]
    public async Task HeadCommand_OutputsFirstNLines()
    {
        var cmd = new HeadCommand(HeadCommand.StaticManifest);
        using var stdin = new StringReader("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext(["-n", "3"], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Equal("1\n2\n3\n", output.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task TailCommand_OutputsLastNLines()
    {
        var cmd = new TailCommand(TailCommand.StaticManifest);
        using var stdin = new StringReader("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext(["-n", "2"], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Equal("9\n10\n", output.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task SortCommand_SortsLinesNumericallyAndReverses()
    {
        var cmd = new SortCommand(SortCommand.StaticManifest);
        using var stdin = new StringReader("10\n2\n1\n");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext(["-n", "-r"], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Equal("10\n2\n1\n", output.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task WcCommand_CountsLinesWordsBytes()
    {
        var cmd = new WcCommand(WcCommand.StaticManifest);
        using var stdin = new StringReader("hello world\nfoo bar\n");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext([], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains("2", output); // 2 lines
        Assert.Contains("4", output); // 4 words
    }

    [Fact]
    public async Task ClearCommand_EmitsAnsiClearSequence()
    {
        var cmd = new ClearCommand(ClearCommand.StaticManifest);
        using var stdin = new StringReader("");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext([], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("\x1b[2J\x1b[H", stdout.ToString());
    }

    [Fact]
    public async Task HelpCommand_ListsCommands_Or_ManualPage()
    {
        var cmd = new HelpCommand(HelpCommand.StaticManifest);
        using var stdin = new StringReader("");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var ctx = CreateContext(["ping"], stdin, stdout, stderr);
        int exitCode = await cmd.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("PING(1)", stdout.ToString());
    }

    // AliasCommand now persists through the real filesystem gateway (see AliasCommandTests.cs,
    // which uses HackerOs.Tests.Support's FakeAppFileSystemGateway) instead of an in-memory
    // dictionary, so it needs a FileSystem-capable context that this file's FileSystem-less
    // StubAppExecutionContext cannot provide.

    // ── Helper ─────────────────────────────────────────────────────────────

    private static TerminalExecutionContext CreateContext(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr)
    {
        var manifest = GrepCommand.StaticManifest;
        var app = new StubAppExecutionContext(manifest);

        return new TerminalExecutionContext(
            app: app,
            arguments: args.ToImmutableArray(),
            standardInput: stdin,
            standardOutput: stdout,
            standardError: stderr,
            workingDirectory: "/home/user",
            environment: ImmutableDictionary<string, string>.Empty);
    }

    private sealed class StubAppExecutionContext : IAppExecutionContext
    {
        public StubAppExecutionContext(AppManifest manifest) => Manifest = manifest;
        public AppManifest Manifest { get; }
        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId => "user";
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities => new HashSet<string>(Manifest.Capabilities);
        public SessionId SessionId { get; } = SessionId.FromGuid(Guid.NewGuid());
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(1);
        public CancellationToken CancellationToken => CancellationToken.None;
        public ICapabilityChecker Capabilities => throw new NotImplementedException();
        public IAppFileSystemGateway FileSystem => throw new NotImplementedException();
        public IAppSettingsGateway Settings => throw new NotImplementedException();
        public IAppEventGateway Events => throw new NotImplementedException();
        public IAppNotificationGateway Notifications => throw new NotImplementedException();
        public IAppLoggingGateway Logging => throw new NotImplementedException();
        public IAppDiagnosticsGateway Diagnostics => throw new NotImplementedException();
        public IAppClockGateway Clock => throw new NotImplementedException();
        public IAppProcessGateway Processes => throw new NotImplementedException();
    }
}
