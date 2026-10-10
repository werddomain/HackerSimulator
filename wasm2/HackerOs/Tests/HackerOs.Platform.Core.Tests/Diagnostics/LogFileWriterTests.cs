using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Events;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.Diagnostics;

/// <summary>Contract tests for <see cref="LogFileWriter"/> and rotation (ADR 0045, sections 1, 3 and 4).</summary>
public sealed class LogFileWriterTests
{
    [Fact]
    public async Task Lines_queued_before_attach_are_written_on_attach_with_system_ownership()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "early boot line");
        Assert.Equal(1, writer.PendingCount);
        Assert.Null(await fs.StatAsync("/var/log/syslog"));

        await writer.AttachAsync(fs.Service);

        Assert.Equal(0, writer.PendingCount);
        string[] lines = await fs.ReadLinesAsync("/var/log/syslog");
        Assert.Equal("2026-10-10T03:11:42.123Z INFO  kernel: early boot line", Assert.Single(lines));
        FileSystemEntrySnapshot syslog = (await fs.StatAsync("/var/log/syslog"))!;
        Assert.Equal("system", syslog.Metadata.OwnerId);
        Assert.Equal("system", syslog.Metadata.GroupId);
        Assert.Equal(FileSystemPermissions.FromMode(0b110_100_100), syslog.Metadata.Permissions);
    }

    [Fact]
    public async Task Auth_log_is_created_0600_and_app_logs_0644_under_var_log_apps()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.AuthLog, DiagnosticSeverity.Information, "session.login", "actor=alice");
        writer.Write(LogFileTarget.ForApp("org.hackeros.text-editor"), DiagnosticSeverity.Warning, "org.hackeros.text-editor", "w", 42);

        await writer.AttachAsync(fs.Service);

        Assert.Equal(FileSystemPermissions.FromMode(0b110_000_000), (await fs.StatAsync("/var/log/auth.log"))!.Metadata.Permissions);
        Assert.Equal(FileSystemPermissions.FromMode(0b110_100_100), (await fs.StatAsync("/var/log/apps/org.hackeros.text-editor.log"))!.Metadata.Permissions);
        Assert.Equal(
            "2026-10-10T03:11:42.123Z WARN  org.hackeros.text-editor[42]: w",
            Assert.Single(await fs.ReadLinesAsync("/var/log/apps/org.hackeros.text-editor.log")));
    }

    [Fact]
    public async Task Missing_log_directories_are_created_by_the_writer()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        FileSystemEntrySnapshot apps = (await fs.StatAsync("/var/log/apps"))!;
        await fs.Service.DeleteAsync(
            new FileSystemDeleteRequest(apps.Path, apps.Metadata.Revision, (await fs.StatAsync("/var/log"))!.Metadata.Revision),
            LogTestFileSystem.SystemContext(fs.Clock.UtcNow));
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.ForApp("org.example.app"), DiagnosticSeverity.Information, "org.example.app", "x");

        await writer.AttachAsync(fs.Service);

        Assert.Equal(FileSystemPermissions.FromMode(0b111_101_101), (await fs.StatAsync("/var/log/apps"))!.Metadata.Permissions);
        Assert.Single(await fs.ReadLinesAsync("/var/log/apps/org.example.app.log"));
    }

    [Fact]
    public async Task Periodic_flush_runs_every_simulated_second()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        await writer.AttachAsync(fs.Service);

        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "tick");
        fs.Clock.Advance(9);
        Assert.Equal(1, writer.PendingCount);

        fs.Clock.Advance(1);
        await writer.FlushAsync(); // waits for the flush the clock started

        Assert.Equal(0, writer.PendingCount);
        Assert.Single(await fs.ReadLinesAsync("/var/log/syslog"));
    }

    [Fact]
    public async Task Reaching_the_threshold_flushes_without_waiting_for_the_clock()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        await writer.AttachAsync(fs.Service);

        for (int index = 0; index < LogFileWriter.FlushThreshold; index++)
        {
            writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Debug, "kernel", $"line {index}");
        }

        await writer.FlushAsync();
        Assert.Equal(LogFileWriter.FlushThreshold, (await fs.ReadLinesAsync("/var/log/syslog")).Length);
    }

    [Fact]
    public async Task One_flush_appends_each_file_with_a_single_write()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "first");
        await writer.AttachAsync(fs.Service);
        long before = (await fs.StatAsync("/var/log/syslog"))!.Metadata.Revision;

        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "second");
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "third");
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "fourth");
        await writer.FlushAsync();

        Assert.Equal(before + 1, (await fs.StatAsync("/var/log/syslog"))!.Metadata.Revision);
        Assert.Equal(
            ["first", "second", "third", "fourth"],
            (await fs.ReadLinesAsync("/var/log/syslog")).Select(line => line[(line.LastIndexOf(": ", StringComparison.Ordinal) + 2)..]));
    }

    [Fact]
    public async Task A_batch_that_would_exceed_one_mebibyte_rotates_the_file_first()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        string full = new string('x', (int)LogRotationPolicy.MaxFileBytes - 10) + "\n";
        await fs.WriteTextAsync("/var/log/syslog", full);
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "after rotation");

        await writer.AttachAsync(fs.Service);

        Assert.Equal(full, await fs.ReadTextAsync("/var/log/syslog.1"));
        Assert.Equal("2026-10-10T03:11:42.123Z INFO  kernel: after rotation", Assert.Single(await fs.ReadLinesAsync("/var/log/syslog")));
        Assert.Equal(FileSystemPermissions.FromMode(0b110_100_100), (await fs.StatAsync("/var/log/syslog"))!.Metadata.Permissions);
    }

    [Fact]
    public async Task Rotation_shifts_archives_and_drops_the_fifth()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        for (int index = 1; index <= LogRotationPolicy.MaxArchives; index++)
        {
            await fs.WriteTextAsync($"/var/log/auth.log.{index}", $"archive {index}\n", 0b110_000_000);
        }

        await fs.WriteTextAsync("/var/log/auth.log", new string('y', (int)LogRotationPolicy.MaxFileBytes) + "\n", 0b110_000_000);
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.AuthLog, DiagnosticSeverity.Information, "session.login", "actor=alice");

        await writer.AttachAsync(fs.Service);

        Assert.StartsWith("yyyy", await fs.ReadTextAsync("/var/log/auth.log.1"), StringComparison.Ordinal);
        for (int index = 2; index <= LogRotationPolicy.MaxArchives; index++)
        {
            Assert.Equal($"archive {index - 1}\n", await fs.ReadTextAsync($"/var/log/auth.log.{index}"));
        }

        Assert.Null(await fs.StatAsync("/var/log/auth.log.6"));
        Assert.Single(await fs.ReadLinesAsync("/var/log/auth.log"));
        Assert.Equal(FileSystemPermissions.FromMode(0b110_000_000), (await fs.StatAsync("/var/log/auth.log.1"))!.Metadata.Permissions);
    }

    [Fact]
    public async Task A_single_batch_larger_than_the_limit_is_written_whole_into_a_fresh_file()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        string huge = new('z', (int)LogRotationPolicy.MaxFileBytes + 1024);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", huge);

        await writer.AttachAsync(fs.Service);

        Assert.Null(await fs.StatAsync("/var/log/syslog.1"));
        Assert.EndsWith(huge, Assert.Single(await fs.ReadLinesAsync("/var/log/syslog")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rotation_policy_never_rotates_an_empty_file()
    {
        Assert.False(LogRotationPolicy.ShouldRotate(0, LogRotationPolicy.MaxFileBytes * 2));
        Assert.False(LogRotationPolicy.ShouldRotate(10, LogRotationPolicy.MaxFileBytes - 10));
        Assert.True(LogRotationPolicy.ShouldRotate(10, LogRotationPolicy.MaxFileBytes - 9));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_full_queue_drops_the_oldest_lines_and_reports_them_in_syslog()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        for (int index = 0; index < LogFileWriter.QueueCapacity + 5; index++)
        {
            writer.Enqueue(LogFileTarget.Syslog, $"2026-10-10T03:11:42.123Z DEBUG kernel: line {index}");
        }

        Assert.Equal(5, writer.TotalDropped);
        await writer.AttachAsync(fs.Service);

        string[] lines = await fs.ReadLinesAsync("/var/log/syslog");
        Assert.Equal(LogFileWriter.QueueCapacity + 1, lines.Length);
        Assert.Equal("2026-10-10T03:11:42.123Z WARN  logging: 5 entries dropped", lines[0]);
        Assert.EndsWith("line 5", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_failures_are_noted_once_in_memory_and_never_written_to_a_file()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        BoundedDiagnosticSink memory = new(100);
        LogFileWriter writer = new(fs.Clock, memory);
        // Make /var/log/apps/broken.log a directory so every append fails.
        await fs.Service.CreateAsync(
            new FileSystemCreateRequest(VirtualPath.Parse("/var/log/apps/broken.log"), FileSystemEntryKind.Directory, FileSystemPermissions.FromMode(0b111_101_101)),
            LogTestFileSystem.SystemContext(fs.Clock.UtcNow));
        await writer.AttachAsync(fs.Service);

        writer.Enqueue(LogFileTarget.ForApp("broken"), "line one");
        await writer.FlushAsync();
        writer.Enqueue(LogFileTarget.ForApp("broken"), "line two");
        await writer.FlushAsync();

        DiagnosticEntry noted = Assert.Single(memory.Entries);
        Assert.Equal(LogFileWriter.LoggingCategory, noted.Category);
        Assert.Contains("/var/log/apps/broken.log", noted.Message, StringComparison.Ordinal);
        Assert.Null(await fs.StatAsync("/var/log/syslog"));
    }

    [Fact]
    public async Task Logout_triggers_a_flush()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        InMemoryEventBus bus = new();
        LogFileWriter writer = new(fs.Clock, eventBus: bus);
        await writer.AttachAsync(fs.Service);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "bye");

        bus.Publish(new SessionLoggedOutEvent(
            SessionId.FromGuid(Guid.NewGuid()), LocalUserId.FromGuid(Guid.NewGuid()), fs.Clock.UtcNow));
        await writer.FlushAsync();

        Assert.Single(await fs.ReadLinesAsync("/var/log/syslog"));
    }

    [Fact]
    public async Task Dispose_writes_the_last_lines_and_stops_the_schedule()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        await writer.AttachAsync(fs.Service);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "shutdown");

        await writer.DisposeAsync();
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "ignored after dispose");
        fs.Clock.Advance(20);

        Assert.Equal("2026-10-10T03:11:42.123Z INFO  kernel: shutdown", Assert.Single(await fs.ReadLinesAsync("/var/log/syslog")));
    }

    [Fact]
    public async Task Ordinary_readers_go_through_file_rights()
    {
        LogTestFileSystem fs = await LogTestFileSystem.CreateAsync();
        LogFileWriter writer = new(fs.Clock);
        writer.Write(LogFileTarget.Syslog, DiagnosticSeverity.Information, "kernel", "public");
        writer.Write(LogFileTarget.AuthLog, DiagnosticSeverity.Information, "session.login", "private");
        await writer.AttachAsync(fs.Service);
        FileSystemAuthorizationContext reader = LogTestFileSystem.UserContext(fs.Clock.UtcNow, AppCapabilities.FileSystemSystemRead);
        FileSystemAuthorizationContext noCapability = LogTestFileSystem.UserContext(fs.Clock.UtcNow);

        Assert.Contains("public", await fs.ReadTextAsync("/var/log/syslog", reader), StringComparison.Ordinal);
        FileSystemResult<FileSystemContentReadHandle> auth = await fs.Service.ReadAsync(
            new FileSystemReadRequest(VirtualPath.Parse("/var/log/auth.log")), reader);
        Assert.Equal(FileSystemErrorCode.PermissionDenied, auth.Error?.Code);
        FileSystemResult<FileSystemContentReadHandle> denied = await fs.Service.ReadAsync(
            new FileSystemReadRequest(VirtualPath.Parse("/var/log/syslog")), noCapability);
        Assert.Equal(FileSystemErrorCode.CapabilityDenied, denied.Error?.Code);
        FileSystemMutationResult write = await fs.Service.WriteAsync(
            new FileSystemWriteRequest(VirtualPath.Parse("/var/log/syslog")),
            new EmptySource(),
            LogTestFileSystem.UserContext(fs.Clock.UtcNow, AppCapabilities.FileSystemSystemRead, AppCapabilities.FileSystemSystemWrite));
        Assert.False(write.Succeeded);
    }

    private sealed class EmptySource : IFileSystemContentSource
    {
        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Text();

        public long? Length => 0;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(string.Empty)));
    }
}
