using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Shell;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Tests.Shell;

public sealed class UiPlatformPreferenceServiceTests
{
    [Fact]
    public async Task InitializeAsync_defaults_to_auto_and_the_probes_suggestion()
    {
        FakeProbe probe = new(WellKnownAppPlatforms.Desktop);
        UiPlatformPreferenceService service = CreateService(probe, out _);

        await service.InitializeAsync();

        Assert.Equal(UiPlatformSelectionSource.Auto, service.Current.SelectionSource);
        Assert.Null(service.Current.ExplicitPlatformId);
        Assert.Equal(WellKnownAppPlatforms.Desktop, service.Current.ActivePlatform);
    }

    [Fact]
    public async Task SetExplicitAsync_persists_and_updates_active_platform()
    {
        FakeProbe probe = new(WellKnownAppPlatforms.Desktop);
        UiPlatformPreferenceService service = CreateService(probe, out InMemorySettingsDocumentService settings);
        await service.InitializeAsync();

        bool raised = false;
        service.Changed += () => raised = true;

        await service.SetExplicitAsync(WellKnownAppPlatforms.Mobile);

        Assert.True(raised);
        Assert.Equal(UiPlatformSelectionSource.Explicit, service.Current.SelectionSource);
        Assert.Equal(WellKnownAppPlatforms.Mobile, service.Current.ExplicitPlatformId);
        Assert.Equal(WellKnownAppPlatforms.Mobile, service.Current.ActivePlatform);

        // Persisted: a freshly initialized service reads the same choice back.
        UiPlatformPreferenceService reloaded = new(settings, probe);
        await reloaded.InitializeAsync();
        Assert.Equal(UiPlatformSelectionSource.Explicit, reloaded.Current.SelectionSource);
        Assert.Equal(WellKnownAppPlatforms.Mobile, reloaded.Current.ExplicitPlatformId);
    }

    [Fact]
    public async Task ClearToAutoAsync_reverts_to_the_probes_suggestion()
    {
        FakeProbe probe = new(WellKnownAppPlatforms.Mobile);
        UiPlatformPreferenceService service = CreateService(probe, out _);
        await service.InitializeAsync();
        await service.SetExplicitAsync(WellKnownAppPlatforms.Desktop);

        await service.ClearToAutoAsync();

        Assert.Equal(UiPlatformSelectionSource.Auto, service.Current.SelectionSource);
        Assert.Null(service.Current.ExplicitPlatformId);
        Assert.Equal(WellKnownAppPlatforms.Mobile, service.Current.ActivePlatform);
    }

    [Fact]
    public async Task Probe_change_while_auto_updates_active_platform_and_raises_changed()
    {
        FakeProbe probe = new(WellKnownAppPlatforms.Desktop);
        UiPlatformPreferenceService service = CreateService(probe, out _);
        await service.InitializeAsync();

        bool raised = false;
        service.Changed += () => raised = true;

        probe.SetSuggestion(WellKnownAppPlatforms.Mobile);

        Assert.True(raised);
        Assert.Equal(WellKnownAppPlatforms.Mobile, service.Current.ActivePlatform);
    }

    [Fact]
    public async Task Probe_change_while_explicit_does_not_change_active_platform()
    {
        FakeProbe probe = new(WellKnownAppPlatforms.Desktop);
        UiPlatformPreferenceService service = CreateService(probe, out _);
        await service.InitializeAsync();
        await service.SetExplicitAsync(WellKnownAppPlatforms.Desktop);

        probe.SetSuggestion(WellKnownAppPlatforms.Mobile);

        Assert.Equal(WellKnownAppPlatforms.Desktop, service.Current.ActivePlatform);
    }

    private static UiPlatformPreferenceService CreateService(
        FakeProbe probe, out InMemorySettingsDocumentService settings)
    {
        settings = new InMemorySettingsDocumentService([UiPlatformPreferenceSettingsDocuments.CreateDefinition()]);
        return new UiPlatformPreferenceService(settings, probe);
    }

    private sealed class FakeProbe(AppPlatformId initialSuggestion) : IPlatformEnvironmentProbe
    {
        public PlatformEnvironmentSnapshot Current { get; private set; } = new(
            new PlatformEnvironmentSignals(1280, 800, false, true, 0, false),
            initialSuggestion,
            "test");

        public event Action? Changed;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void SetSuggestion(AppPlatformId suggestion)
        {
            Current = Current with { SuggestedPlatform = suggestion };
            Changed?.Invoke();
        }
    }
}
