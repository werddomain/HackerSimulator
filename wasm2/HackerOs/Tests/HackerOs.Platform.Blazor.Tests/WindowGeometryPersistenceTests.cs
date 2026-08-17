using HackerOs.App.Abstractions;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Platform.Core;
using HackerOs.Simulation.Abstractions;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Tests;

public sealed class WindowGeometryPersistenceTests
{
    [Fact]
    public async Task Eligible_geometry_round_trips_without_volatile_window_state()
    {
        SettingsDocumentDefinition definition = WindowGeometrySettings.CreateDefinition(
            "org.hackeros.notes", "alice", "installation-1");
        InMemorySettingsDocumentService settings = new([definition]);
        WindowGeometryPersistence persistence = new(settings);
        AppOperationContext context = CreateContext();

        Assert.Null(await persistence.RestoreAsync(definition, context));

        WindowBounds bounds = new(25, 40, 720, 540);
        SettingsWriteStatus status = await persistence.SaveAsync(
            definition, bounds, isEligible: true, context);

        Assert.Equal(SettingsWriteStatus.Success, status);
        Assert.Equal(bounds, await persistence.RestoreAsync(definition, context));
        SettingsReadResult stored = await settings.ReadAsync(definition.Path, context);
        Assert.DoesNotContain("visualState", stored.Document!.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("process", stored.Document.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ineligible_window_does_not_write_geometry()
    {
        SettingsDocumentDefinition definition = WindowGeometrySettings.CreateDefinition(
            "org.hackeros.notes", "alice", "installation-1");
        WindowGeometryPersistence persistence = new(new InMemorySettingsDocumentService([definition]));

        SettingsWriteStatus status = await persistence.SaveAsync(
            definition,
            new WindowBounds(25, 40, 720, 540),
            isEligible: false,
            CreateContext());

        Assert.Equal(SettingsWriteStatus.Denied, status);
    }

    private static AppOperationContext CreateContext() => new()
    {
        AppId = "org.hackeros.shell",
        UserId = "alice",
        UserAuthority = AppAuthority.User,
        GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal)
        {
            AppCapabilities.SettingsRead,
            AppCapabilities.SettingsWrite,
        },
    };
}