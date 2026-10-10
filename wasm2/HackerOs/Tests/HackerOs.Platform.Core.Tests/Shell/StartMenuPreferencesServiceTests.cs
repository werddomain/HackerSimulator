using HackerOs.Platform.Core.Shell;
using HackerOs.Platform.Core.Tests.Settings;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.Shell;

public sealed class StartMenuPreferencesServiceTests
{
    [Fact]
    public async Task InitializeAsync_starts_each_user_with_an_empty_pin_list()
    {
        SettingsFilesHarness harness = new();
        AuthenticatedPrincipal alice = await harness.LoginAsync(harness.Alice);
        using StartMenuPreferencesService service = new(harness.Settings, harness.Session);

        await service.InitializeAsync();

        Assert.Empty(service.GetPinnedAppIds(alice.UserId));
        Assert.Empty(service.GetPinnedAppIds(harness.Bob.Id));
    }

    [Fact]
    public async Task PinAsync_persists_deduplicates_and_reloads_unknown_ids()
    {
        SettingsFilesHarness harness = new();
        AuthenticatedPrincipal alice = await harness.LoginAsync(harness.Alice);
        using StartMenuPreferencesService service = new(harness.Settings, harness.Session);
        await service.InitializeAsync();

        Assert.True(await service.PinAsync(alice.UserId, "org.example.unmounted"));
        Assert.False(await service.PinAsync(alice.UserId, "org.example.unmounted"));
        Assert.Equal(["org.example.unmounted"], service.GetPinnedAppIds(alice.UserId));

        // Pins are the user's own file (ADR 0044 section 8), not a shared machine document.
        Assert.Contains("org.example.unmounted", await harness.ReadAsync("/home/alice/.config/hackeros/start-menu.json"));

        using StartMenuPreferencesService reloaded = new(harness.Settings, harness.Session);
        await reloaded.InitializeAsync();
        Assert.Equal(["org.example.unmounted"], reloaded.GetPinnedAppIds(alice.UserId));
    }

    [Fact]
    public async Task MoveAsync_reorders_the_session_profile_and_never_touches_another_user()
    {
        SettingsFilesHarness harness = new();
        AuthenticatedPrincipal alice = await harness.LoginAsync(harness.Alice);
        using StartMenuPreferencesService service = new(harness.Settings, harness.Session);
        await service.InitializeAsync();
        await service.PinAsync(alice.UserId, "org.hackeros.browser");
        await service.PinAsync(alice.UserId, "org.hackeros.files");
        await service.PinAsync(alice.UserId, "org.hackeros.settings");

        Assert.False(await service.PinAsync(harness.Bob.Id, "org.example.private"));
        Assert.True(await service.MoveAsync(alice.UserId, "org.hackeros.settings", targetIndex: 0));

        Assert.Equal(
            ["org.hackeros.settings", "org.hackeros.browser", "org.hackeros.files"],
            service.GetPinnedAppIds(alice.UserId));
        Assert.Empty(service.GetPinnedAppIds(harness.Bob.Id));
        Assert.Null(await harness.ReadAsync("/home/bob/.config/hackeros/start-menu.json"));

        using StartMenuPreferencesService reloaded = new(harness.Settings, harness.Session);
        await reloaded.InitializeAsync();
        Assert.Equal(service.GetPinnedAppIds(alice.UserId), reloaded.GetPinnedAppIds(alice.UserId));
    }
}
