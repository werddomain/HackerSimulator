using Xunit;

namespace HackerOs.Apps.TextEditor.Tests;

public sealed class TextEditorFindEngineTests
{
    [Fact]
    public void FindMatches_ReturnsEveryCaseInsensitiveOccurrence()
    {
        List<int> matches = TextEditorFindEngine.FindMatches("Cat cat CATalog", "cat");

        Assert.Equal([0, 4, 8], matches);
    }

    [Fact]
    public void FindMatches_EmptyQueryOrContent_ReturnsNoMatches()
    {
        Assert.Empty(TextEditorFindEngine.FindMatches("some content", ""));
        Assert.Empty(TextEditorFindEngine.FindMatches("", "query"));
    }

    [Fact]
    public void Next_WrapsAroundToTheFirstMatch()
    {
        Assert.Equal(1, TextEditorFindEngine.Next(0, matchCount: 3));
        Assert.Equal(2, TextEditorFindEngine.Next(1, matchCount: 3));
        Assert.Equal(0, TextEditorFindEngine.Next(2, matchCount: 3));
    }

    [Fact]
    public void Previous_WrapsAroundToTheLastMatch()
    {
        Assert.Equal(1, TextEditorFindEngine.Previous(2, matchCount: 3));
        Assert.Equal(0, TextEditorFindEngine.Previous(1, matchCount: 3));
        Assert.Equal(2, TextEditorFindEngine.Previous(0, matchCount: 3));
    }

    [Fact]
    public void NextAndPrevious_NoMatches_ReturnNegativeOne()
    {
        Assert.Equal(-1, TextEditorFindEngine.Next(-1, matchCount: 0));
        Assert.Equal(-1, TextEditorFindEngine.Previous(-1, matchCount: 0));
    }

    [Fact]
    public void Next_FromNoSelection_StartsAtFirstMatch()
    {
        // Simulates cycling forward before any match has been selected yet (_findIndex == -1).
        Assert.Equal(0, TextEditorFindEngine.Next(-1, matchCount: 3));
    }
}
