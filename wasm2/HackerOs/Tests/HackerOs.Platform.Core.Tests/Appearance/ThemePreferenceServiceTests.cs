using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Appearance;
using HackerOs.Simulation.Abstractions;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Platform.Core.Tests.Appearance;

public sealed class ThemePreferenceServiceTests
{
    private static readonly AppOperationContext SettingsContext = new()
    {
        AppId = "org.hackeros.settings",
        UserId = "test-user",
        UserAuthority = AppAuthority.System,
        GrantedCapabilities = new HashSet<string>(
            [AppCapabilities.SettingsSystemRead, AppCapabilities.SettingsSystemWrite],
            StringComparer.Ordinal),
        IsSystemOperation = true
    };

    [Fact]
    public async Task RefreshAsync_reloads_an_external_settings_change_and_notifies()
    {
        InMemorySettingsDocumentService settings = new([AppearanceSettingsDocuments.CreateDefinition()]);
        ThemePreferenceService service = new(settings);
        await service.InitializeAsync();
        Assert.Equal(ThemePreferences.Default, service.Current);

        SettingsReadResult initial = await settings.ReadAsync(AppearanceSettingsDocuments.Path, SettingsContext);
        ThemePreferences expected = new(
            WellKnownThemeIds.Ubuntu,
            WellKnownThemeIds.Ios,
            WellKnownAccentIds.Purple,
            AnimationsEnabled: false);
        SettingsWriteResult write = await settings.WriteAsync(
            new SettingsWriteRequest(
                AppearanceSettingsDocuments.Path,
                AppearanceSettingsCodec.Encode(expected),
                initial.Document!.Revision),
            SettingsContext);
        Assert.Equal(SettingsWriteStatus.Success, write.Status);

        int changes = 0;
        service.Changed += () => changes++;
        await service.RefreshAsync();

        Assert.Equal(expected, service.Current);
        Assert.Equal(1, changes);
    }
}
