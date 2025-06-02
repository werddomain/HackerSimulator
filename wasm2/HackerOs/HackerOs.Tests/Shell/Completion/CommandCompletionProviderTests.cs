using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Shell.Completion;
using Moq;
using Xunit;

namespace HackerOs.Tests.Shell.Completion;

public class CommandCompletionProviderTests
{
    private readonly Mock<ICommandRegistry> _mockCommandRegistry;
    private readonly CommandCompletionProvider _provider;

    public CommandCompletionProviderTests()
    {
        _mockCommandRegistry = new Mock<ICommandRegistry>();
        _provider = new CommandCompletionProvider(_mockCommandRegistry.Object);
    }

    [Fact]
    public void Priority_ShouldReturn100()
    {
        // Act & Assert
        Assert.Equal(100, _provider.Priority);
    }

    [Fact]
    public void CanProvideCompletions_AtCommandPosition_ShouldReturnTrue()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "ls",
            CursorPosition = 2,
            Tokens = new[] { "ls" },
            CurrentTokenIndex = 0,
            CurrentToken = "ls"
        };

        // Act
        var result = _provider.CanProvideCompletions(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanProvideCompletions_AtArgumentPosition_ShouldReturnFalse()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "ls /home",
            CursorPosition = 8,
            Tokens = new[] { "ls", "/home" },
            CurrentTokenIndex = 1,
            CurrentToken = "/home"
        };

        // Act
        var result = _provider.CanProvideCompletions(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCompletionsAsync_ShouldReturnMatchingCommands()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "l",
            CursorPosition = 1,
            Tokens = new[] { "l" },
            CurrentTokenIndex = 0,
            CurrentToken = "l"
        };

        _mockCommandRegistry.Setup(r => r.GetCommandNamesStartingWith("l"))
            .Returns(new[] { "ls", "ln", "less" });

        // Act
        var result = await _provider.GetCompletionsAsync(context);

        // Assert
        var completions = result.ToList();
        Assert.Equal(3, completions.Count);
        Assert.All(completions, c => Assert.Equal(CompletionType.Command, c.Type));
        Assert.All(completions, c => Assert.Equal(100, c.Priority));
        Assert.Contains(completions, c => c.Text == "ls");
        Assert.Contains(completions, c => c.Text == "ln");
        Assert.Contains(completions, c => c.Text == "less");
    }

    [Fact]
    public async Task GetCompletionsAsync_WithInvalidContext_ShouldReturnEmpty()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "ls /home",
            CursorPosition = 8,
            Tokens = new[] { "ls", "/home" },
            CurrentTokenIndex = 1,
            CurrentToken = "/home"
        };

        // Act
        var result = await _provider.GetCompletionsAsync(context);

        // Assert
        Assert.Empty(result);
    }
}
