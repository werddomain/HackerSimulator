namespace HackerOs.App.Abstractions.Tests;

public sealed class AppCapabilitiesTests
{
    [Theory]
    [InlineData(AppCapabilities.ProcessList)]
    [InlineData(AppCapabilities.ProcessManage)]
    [InlineData(AppCapabilities.NotificationsPost)]
    [InlineData(AppCapabilities.WindowsManage)]
    [InlineData(AppCapabilities.ClipboardRead)]
    [InlineData(AppCapabilities.ClipboardWrite)]
    [InlineData(AppCapabilities.ServicesManage)]
    public void Process_notification_window_clipboard_and_service_capabilities_are_known(string capability)
    {
        Assert.True(AppCapabilities.IsKnown(capability));
        Assert.Contains(capability, AppCapabilities.All);
    }

    [Theory]
    [InlineData(AppCapabilities.DialogFileOpen)]
    [InlineData(AppCapabilities.DialogFileSave)]
    [InlineData(AppCapabilities.DialogFolderSelect)]
    public void Dialog_capabilities_require_window_kind(string capability)
    {
        Assert.True(AppCapabilities.RequiresWindowKind(capability));
    }

    [Theory]
    [InlineData(AppCapabilities.FileSystemPrivateRead)]
    [InlineData(AppCapabilities.ProcessManage)]
    [InlineData(AppCapabilities.NotificationsPost)]
    public void Non_dialog_capabilities_do_not_require_window_kind(string capability)
    {
        Assert.False(AppCapabilities.RequiresWindowKind(capability));
    }
}
