using HackerOs.App.Abstractions;
using Xunit;

namespace HackerOs.Apps.Settings.Tests;

public sealed class SettingsWindowTests
{
    [Fact]
    public void Manifest_HasWindowKind_AndRequiredCapabilities()
    {
        AppManifest manifest = AppDefinitionMapper.ToManifest(new SettingsApp());
        Assert.Equal(AppKind.Window, manifest.Kind);
        Assert.True(manifest.SingleInstancePerUser);
        Assert.Contains(AppCapabilities.FileSystemSystemRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileSystemSystemWrite, manifest.Capabilities);
        Assert.DoesNotContain(manifest.Capabilities, capability => capability.StartsWith("settings.", StringComparison.Ordinal));
        Assert.Contains(AppCapabilities.FileAssociationsRead, manifest.Capabilities);
        Assert.Contains(AppCapabilities.FileAssociationsWrite, manifest.Capabilities);
    }
}

/// <summary>Settings' <c>--app=&lt;id&gt;</c> argument opens an app's page (ADR 0096, section 4).</summary>
public sealed class SettingsAppArgumentTests
{
    [Fact]
    public void The_app_argument_names_the_page_to_show()
    {
        Assert.Equal("org.hackeros.notepad", SettingsWindow.ReadAppArgument(["--app=org.hackeros.notepad"]));
    }

    [Fact]
    public void The_last_app_argument_wins_and_an_empty_one_is_ignored()
    {
        Assert.Equal("org.b", SettingsWindow.ReadAppArgument(["--app=org.a", "--app=org.b", "--app= "]));
    }

    [Fact]
    public void Without_an_app_argument_there_is_no_page()
    {
        Assert.Null(SettingsWindow.ReadAppArgument(["--show-settings"]));
        Assert.Null(SettingsWindow.ReadAppArgument([]));
    }

    [Fact]
    public void A_page_argument_opens_appearance_or_desktop_and_ignores_unknown_pages()
    {
        Assert.Equal(0, SettingsWindow.ReadPageArgument(["--page=appearance"]));
        Assert.Equal(1, SettingsWindow.ReadPageArgument(["--page=Desktop"]));
        Assert.Equal(1, SettingsWindow.ReadPageArgument(["--page=desktop", "--page=nowhere"]));
        Assert.Null(SettingsWindow.ReadPageArgument(["--page=nowhere"]));
        Assert.Null(SettingsWindow.ReadPageArgument([]));
    }

    [Fact]
    public void Settings_can_start_and_stop_services_from_an_app_page()
    {
        AppManifest manifest = AppDefinitionMapper.ToManifest(new SettingsApp());

        Assert.Contains(AppCapabilities.ServicesManage, manifest.Capabilities);
    }
}
