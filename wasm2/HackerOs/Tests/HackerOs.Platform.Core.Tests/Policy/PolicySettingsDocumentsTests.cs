using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Tests.Settings;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Tests.Policy;

/// <summary>The administrator policy is the system-only file /etc/hackeros/policy.config (ADR 0044).</summary>
public sealed class PolicySettingsDocumentsTests
{
    private const string GrantsLocked = "capabilityGrantsLocked";

    [Fact]
    public async Task Ordinary_user_cannot_write_the_policy_even_with_the_system_write_capability()
    {
        SettingsFilesHarness harness = new();
        await harness.BootAsync();
        AuthenticatedPrincipal alice = await harness.LoginAsync(harness.Alice);
        IAppFileSystemGateway settingsApp = harness.CreateAppGateway(
            alice, "org.hackeros.settings", AppCapabilities.FileSystemSystemRead, AppCapabilities.FileSystemSystemWrite);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.Settings
            .SetAsync(PolicySettingsDocuments.Document, GrantsLocked, "true", SettingLayer.System, settingsApp)
            .AsTask());

        LayeredConfigSnapshot policy = await harness.Settings.ReadAsync(PolicySettingsDocuments.Document);
        Assert.Equal("false", policy.Values[GrantsLocked]);
    }

    [Fact]
    public async Task Policy_has_no_user_layer()
    {
        SettingsFilesHarness harness = new();
        await harness.LoginAsync(harness.Alice);
        await harness.WriteAsRootAsync("/home/alice/.config/hackeros/policy.config", "capabilityGrantsLocked=true\n");

        LayeredConfigSnapshot policy = await harness.Settings.ReadAsync(PolicySettingsDocuments.Document);

        Assert.Equal("false", policy.Values[GrantsLocked]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Settings
            .SetAsync(PolicySettingsDocuments.Document, GrantsLocked, "true")
            .AsTask());
    }

    [Fact]
    public async Task Root_edit_of_etc_is_what_every_reader_sees()
    {
        SettingsFilesHarness harness = new();
        await harness.BootAsync();
        await harness.WriteAsRootAsync(
            PolicySettingsDocuments.Document.SystemPath.Value,
            "capabilityGrantsLocked=true\nallowRuntimePackageInstall=false\n");

        LayeredConfigSnapshot policy = await harness.Settings.ReadAsync(PolicySettingsDocuments.Document);

        Assert.Equal("true", policy.Values[GrantsLocked]);
        Assert.Equal(SettingLayer.System, policy.Sources[GrantsLocked]);
        Assert.Empty(policy.Warnings);
    }

    [Fact]
    public async Task Invalid_value_in_the_file_falls_back_to_the_default_with_a_warning()
    {
        SettingsFilesHarness harness = new();
        await harness.WriteAsRootAsync(PolicySettingsDocuments.Document.SystemPath.Value, "capabilityGrantsLocked=not-a-bool\n");

        LayeredConfigSnapshot policy = await harness.Settings.ReadAsync(PolicySettingsDocuments.Document);

        Assert.Equal("false", policy.Values[GrantsLocked]);
        LayeredConfigWarning warning = Assert.Single(policy.Warnings);
        Assert.Equal("settings: /etc/hackeros/policy.config:1: invalid value for 'capabilityGrantsLocked' (expected boolean), using default", warning.ToLogLine());
    }

    [Fact]
    public async Task Writing_an_invalid_value_is_rejected_before_touching_the_file()
    {
        SettingsFilesHarness harness = new();
        await harness.BootAsync();
        string? before = await harness.ReadAsync(PolicySettingsDocuments.Document.SystemPath.Value);

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Settings
            .SetAsync(PolicySettingsDocuments.Document, GrantsLocked, "maybe", SettingLayer.System, harness.CreateRootGateway())
            .AsTask());

        Assert.Equal(before, await harness.ReadAsync(PolicySettingsDocuments.Document.SystemPath.Value));
    }
}
