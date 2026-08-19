using HackerOs.Tests.Support;
using HackerOs.Theming.Abstractions;
using Xunit;

namespace HackerOs.Apps.Settings.Tests;

public sealed class AppearancePersistenceServiceTests
{
    private const string DocumentPath = "/etc/hackeros/appearance.json";

    [Fact]
    public async Task ReadAsync_ReturnsDecodedDocument()
    {
        FakeAppSettingsGateway gateway = new FakeAppSettingsGateway().WithDocument(
            DocumentPath, """{"schemaVersion":1,"accent":"cyan","animationsEnabled":false}""");
        AppearancePersistenceService service = new(gateway);

        ThemePreferences result = await service.ReadAsync();

        Assert.Equal(WellKnownThemeIds.HackerOs, result.DesktopThemeId);
        Assert.Equal(WellKnownThemeIds.Android, result.MobileThemeId);
        Assert.Equal(WellKnownAccentIds.Cyan, result.AccentId);
        Assert.False(result.AnimationsEnabled);
    }

    [Fact]
    public async Task ReadAsync_MissingDocument_FallsBackToDefault()
    {
        FakeAppSettingsGateway gateway = new();
        AppearancePersistenceService service = new(gateway);

        ThemePreferences result = await service.ReadAsync();

        Assert.Equal(ThemePreferences.Default, result);
    }

    [Fact]
    public async Task WriteAsync_PersistsNewValues_UsingTheCurrentRevision()
    {
        FakeAppSettingsGateway gateway = new FakeAppSettingsGateway().WithDocument(
            DocumentPath, """{"schemaVersion":1,"accent":"green","animationsEnabled":true}""", revision: 3);
        AppearancePersistenceService service = new(gateway);

        ThemePreferences expected = new(
            WellKnownThemeIds.Windows7,
            WellKnownThemeIds.Ios,
            WellKnownAccentIds.Purple,
            AnimationsEnabled: false);
        bool succeeded = await service.WriteAsync(expected);

        Assert.True(succeeded);
        ThemePreferences reread = await service.ReadAsync();
        Assert.Equal(expected, reread);
    }

    [Fact]
    public async Task WriteAsync_WithoutAnExistingDocument_Fails()
    {
        FakeAppSettingsGateway gateway = new();
        AppearancePersistenceService service = new(gateway);

        bool succeeded = await service.WriteAsync(ThemePreferences.Default with
        {
            AccentId = WellKnownAccentIds.Cyan
        });

        Assert.False(succeeded);
    }
}
