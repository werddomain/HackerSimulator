using HackerOs.Platform.Blazor.Shell;

namespace HackerOs.Platform.Blazor.Tests.Shell;

public sealed class TaskbarClockPanelSourceAdapterTests
{
    [Fact]
    public void Toggle_opens_and_raises_changed()
    {
        TaskbarClockPanelSourceAdapter adapter = new();
        int changedCount = 0;
        adapter.Changed += () => changedCount++;

        adapter.Toggle();

        Assert.True(adapter.IsOpen);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void Toggle_twice_closes_and_raises_changed_twice()
    {
        TaskbarClockPanelSourceAdapter adapter = new();
        int changedCount = 0;
        adapter.Changed += () => changedCount++;

        adapter.Toggle();
        adapter.Toggle();

        Assert.False(adapter.IsOpen);
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void Close_when_already_closed_does_not_raise_changed()
    {
        TaskbarClockPanelSourceAdapter adapter = new();
        int changedCount = 0;
        adapter.Changed += () => changedCount++;

        adapter.Close();

        Assert.False(adapter.IsOpen);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void Close_when_open_closes_and_raises_changed_once()
    {
        TaskbarClockPanelSourceAdapter adapter = new();
        adapter.Toggle();
        int changedCount = 0;
        adapter.Changed += () => changedCount++;

        adapter.Close();

        Assert.False(adapter.IsOpen);
        Assert.Equal(1, changedCount);
    }
}
