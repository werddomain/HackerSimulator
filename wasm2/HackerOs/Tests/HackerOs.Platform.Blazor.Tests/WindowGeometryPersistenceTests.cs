using HackerOs.Platform.Blazor.Windows;
using HackerOs.Platform.Core.Settings;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Tests;

public sealed class WindowGeometryPersistenceTests
{
    private const string AppId = "org.hackeros.files";

    [Fact]
    public void Geometry_lives_in_device_local_state_not_in_config()
    {
        OsSettingsFile file = WindowGeometrySettings.GetFile(AppId);

        Assert.Equal("/home/alice/.local/state/hackeros/windows/org.hackeros.files.config", file.Resolve("/home/alice").Value);
        Assert.Throws<ArgumentException>(() => WindowGeometrySettings.GetFile("../escape"));
    }

    [Fact]
    public async Task Clean_profile_restores_nothing_then_saved_geometry_round_trips()
    {
        FakeOsSettingsService settings = new();
        WindowGeometryPersistence persistence = new(settings);

        Assert.Null(await persistence.RestoreAsync(AppId));
        Assert.True(await persistence.SaveAsync(AppId, new WindowBounds(10.5, 20, 640, 480), isEligible: true));

        Assert.Equal(new WindowBounds(10.5, 20, 640, 480), await persistence.RestoreAsync(AppId));
        Assert.Equal("x=10.5\ny=20\nwidth=640\nheight=480\n", settings.Files["windows/org.hackeros.files.config"]);
    }

    [Fact]
    public async Task Ineligible_window_is_never_written()
    {
        FakeOsSettingsService settings = new();
        WindowGeometryPersistence persistence = new(settings);

        Assert.False(await persistence.SaveAsync(AppId, new WindowBounds(0, 0, 100, 100), isEligible: false));
        Assert.Empty(settings.Files);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x=1\ny=2\nwidth=3\n")]
    [InlineData("x=1\ny=2\nwidth=-3\nheight=4\n")]
    [InlineData("x=one\ny=2\nwidth=3\nheight=4\n")]
    public void Partial_or_invalid_file_means_default_placement(string content)
    {
        Assert.Null(WindowGeometrySettings.Parse(content));
    }

    [Fact]
    public async Task Save_without_a_session_is_best_effort()
    {
        FakeOsSettingsService settings = new() { NoSession = true };
        WindowGeometryPersistence persistence = new(settings);

        Assert.False(await persistence.SaveAsync(AppId, new WindowBounds(0, 0, 100, 100), isEligible: true));
    }

    /// <summary>Whole-file part of <see cref="IOsSettingsService"/> over a dictionary keyed by relative path.</summary>
    private sealed class FakeOsSettingsService : IOsSettingsService
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

        public bool NoSession { get; init; }

        public event EventHandler<OsSettingsChangedEventArgs>? DocumentChanged { add { } remove { } }

        public string? CurrentHomeDirectory => NoSession ? null : "/home/alice";

        public ValueTask<string?> ReadFileAsync(OsSettingsFile file, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Files.GetValueOrDefault(file.RelativePath));

        public ValueTask WriteFileAsync(OsSettingsFile file, string content, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default)
        {
            if (NoSession)
            {
                throw new InvalidOperationException("A user settings file needs an active session.");
            }

            Files[file.RelativePath] = content;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LayeredConfigSnapshot> ReadAsync(OsSettingsDocument document, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask SetAsync(OsSettingsDocument document, string qualifiedKey, string value, SettingLayer target = SettingLayer.User, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask SetManyAsync(OsSettingsDocument document, IReadOnlyList<KeyValuePair<string, string>> values, SettingLayer target = SettingLayer.User, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask ResetAsync(OsSettingsDocument document, string qualifiedKey, SettingLayer target = SettingLayer.User, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<string> ReadValidatedFileAsync(OsSettingsFile file, ISettingsDocumentValidator validator, string defaultContent, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask WatchAsync(OsSettingsDocument document, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
