using HackerOs.Tests.Support;
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

        AppearanceSettings result = await service.ReadAsync();

        Assert.Equal("cyan", result.Accent);
        Assert.False(result.AnimationsEnabled);
    }

    [Fact]
    public async Task ReadAsync_MissingDocument_FallsBackToDefault()
    {
        FakeAppSettingsGateway gateway = new();
        AppearancePersistenceService service = new(gateway);

        AppearanceSettings result = await service.ReadAsync();

        Assert.Equal(AppearanceSettings.Default, result);
    }

    [Fact]
    public async Task WriteAsync_PersistsNewValues_UsingTheCurrentRevision()
    {
        FakeAppSettingsGateway gateway = new FakeAppSettingsGateway().WithDocument(
            DocumentPath, """{"schemaVersion":1,"accent":"green","animationsEnabled":true}""", revision: 3);
        AppearancePersistenceService service = new(gateway);

        bool succeeded = await service.WriteAsync("purple", false);

        Assert.True(succeeded);
        AppearanceSettings reread = await service.ReadAsync();
        Assert.Equal("purple", reread.Accent);
        Assert.False(reread.AnimationsEnabled);
    }

    [Fact]
    public async Task WriteAsync_WithoutAnExistingDocument_Fails()
    {
        FakeAppSettingsGateway gateway = new();
        AppearancePersistenceService service = new(gateway);

        bool succeeded = await service.WriteAsync("cyan", true);

        Assert.False(succeeded);
    }
}
