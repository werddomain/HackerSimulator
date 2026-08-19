using HackerOs.Platform.Core.Shell;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.Shell;

public sealed class StartMenuPreferencesServiceTests
{
    private static readonly LocalUserId FirstUser =
        LocalUserId.FromGuid(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    private static readonly LocalUserId SecondUser =
        LocalUserId.FromGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    [Fact]
    public async Task InitializeAsync_starts_each_user_with_an_empty_pin_list()
    {
        using StartMenuPreferencesService service = CreateService(out _);

        await service.InitializeAsync();

        Assert.Empty(service.GetPinnedAppIds(FirstUser));
        Assert.Empty(service.GetPinnedAppIds(SecondUser));
    }

    [Fact]
    public async Task PinAsync_persists_deduplicates_and_reloads_unknown_ids()
    {
        using StartMenuPreferencesService service = CreateService(out InMemorySettingsDocumentService settings);
        await service.InitializeAsync();

        Assert.True(await service.PinAsync(FirstUser, "org.example.unmounted"));
        Assert.False(await service.PinAsync(FirstUser, "org.example.unmounted"));
        Assert.Equal(["org.example.unmounted"], service.GetPinnedAppIds(FirstUser));

        using StartMenuPreferencesService reloaded = new(settings);
        await reloaded.InitializeAsync();
        Assert.Equal(["org.example.unmounted"], reloaded.GetPinnedAppIds(FirstUser));
    }

    [Fact]
    public async Task MoveAsync_reorders_one_profile_without_crossing_user_boundaries()
    {
        using StartMenuPreferencesService service = CreateService(out InMemorySettingsDocumentService settings);
        await service.InitializeAsync();
        await service.PinAsync(FirstUser, "org.hackeros.browser");
        await service.PinAsync(FirstUser, "org.hackeros.files");
        await service.PinAsync(FirstUser, "org.hackeros.settings");
        await service.PinAsync(SecondUser, "org.example.private");

        Assert.True(await service.MoveAsync(FirstUser, "org.hackeros.settings", targetIndex: 0));

        Assert.Equal(
            ["org.hackeros.settings", "org.hackeros.browser", "org.hackeros.files"],
            service.GetPinnedAppIds(FirstUser));
        Assert.Equal(["org.example.private"], service.GetPinnedAppIds(SecondUser));

        using StartMenuPreferencesService reloaded = new(settings);
        await reloaded.InitializeAsync();
        Assert.Equal(service.GetPinnedAppIds(FirstUser), reloaded.GetPinnedAppIds(FirstUser));
        Assert.Equal(service.GetPinnedAppIds(SecondUser), reloaded.GetPinnedAppIds(SecondUser));
    }

    private static StartMenuPreferencesService CreateService(out InMemorySettingsDocumentService settings)
    {
        settings = new InMemorySettingsDocumentService([StartMenuSettingsDocuments.CreateDefinition()]);
        return new StartMenuPreferencesService(settings);
    }
}
