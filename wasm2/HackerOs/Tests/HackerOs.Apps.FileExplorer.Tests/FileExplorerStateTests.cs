using HackerOs.Apps.FileExplorer;
using Xunit;

namespace HackerOs.Apps.FileExplorer.Tests;

public sealed class FileExplorerStateTests
{
    [Fact]
    public void FileExplorerState_manages_navigation_history_stacks()
    {
        FileExplorerState state = new("/home/user");
        Assert.Equal("/home/user", state.CurrentPath);
        Assert.False(state.CanNavigateBack);
        Assert.False(state.CanNavigateForward);
        Assert.True(state.CanNavigateUp);

        state.NavigateTo("/home/user/docs");
        Assert.Equal("/home/user/docs", state.CurrentPath);
        Assert.True(state.CanNavigateBack);

        state.NavigateUp();
        Assert.Equal("/home/user", state.CurrentPath);

        state.NavigateBack();
        Assert.Equal("/home/user/docs", state.CurrentPath);

        state.NavigateTo("/var/log");
        Assert.Equal("/var/log", state.CurrentPath);

        state.NavigateBack();
        Assert.Equal("/home/user/docs", state.CurrentPath);
        Assert.True(state.CanNavigateForward);

        state.NavigateForward();
        Assert.Equal("/var/log", state.CurrentPath);
    }

    [Fact]
    public void FileExplorerState_toggles_sorting_and_selection()
    {
        FileExplorerState state = new("/home/user");
        Assert.Equal(FileExplorerSortColumn.Name, state.SortColumn);
        Assert.True(state.SortAscending);

        state.SetSortColumn(FileExplorerSortColumn.Name);
        Assert.False(state.SortAscending);

        state.SetSortColumn(FileExplorerSortColumn.Size);
        Assert.Equal(FileExplorerSortColumn.Size, state.SortColumn);
        Assert.True(state.SortAscending);

        state.SelectSingle("file1.txt");
        Assert.Single(state.SelectedItemNames);
        Assert.Contains("file1.txt", state.SelectedItemNames);

        state.ToggleSelection("file2.txt");
        Assert.Equal(2, state.SelectedItemNames.Count);

        state.ClearSelection();
        Assert.Empty(state.SelectedItemNames);
    }
}
