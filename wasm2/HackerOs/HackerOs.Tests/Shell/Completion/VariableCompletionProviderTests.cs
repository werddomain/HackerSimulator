using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Shell.Completion;
using Xunit;

namespace HackerOs.Tests.Shell.Completion;

public class VariableCompletionProviderTests
{
    private readonly VariableCompletionProvider _provider;

    public VariableCompletionProviderTests()
    {
        _provider = new VariableCompletionProvider();
    }

    [Fact]
    public void Priority_ShouldReturn60()
    {
        // Act & Assert
        Assert.Equal(60, _provider.Priority);
    }

    [Fact]
    public void CanProvideCompletions_WithDollarPrefix_ShouldReturnTrue()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "echo $HO",
            CursorPosition = 8,
            Tokens = new[] { "echo", "$HO" },
            CurrentTokenIndex = 1,
            CurrentToken = "$HO"
        };

        // Act
        var result = _provider.CanProvideCompletions(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanProvideCompletions_WithoutDollarPrefix_ShouldReturnFalse()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "echo HOME",
            CursorPosition = 9,
            Tokens = new[] { "echo", "HOME" },
            CurrentTokenIndex = 1,
            CurrentToken = "HOME"
        };

        // Act
        var result = _provider.CanProvideCompletions(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCompletionsAsync_ShouldReturnMatchingVariables()
    {
        // Arrange
        var environmentVariables = new Dictionary<string, string>
        {
            ["HOME"] = "/home/user",
            ["PATH"] = "/bin:/usr/bin",
            ["USER"] = "testuser",
            ["HOSTNAME"] = "localhost"
        };

        var context = new CompletionContext
        {
            CommandLine = "echo $HO",
            CursorPosition = 8,
            Tokens = new[] { "echo", "$HO" },
            CurrentTokenIndex = 1,
            CurrentToken = "$HO",
            EnvironmentVariables = environmentVariables
        };

        // Act
        var result = await _provider.GetCompletionsAsync(context);

        // Assert
        var completions = result.ToList();
        Assert.Equal(2, completions.Count);
        Assert.All(completions, c => Assert.Equal(CompletionType.Variable, c.Type));
        Assert.All(completions, c => Assert.Equal(60, c.Priority));
        Assert.Contains(completions, c => c.Text == "$HOME");
        Assert.Contains(completions, c => c.Text == "$HOSTNAME");
    }

    [Fact]
    public async Task GetCompletionsAsync_WithEmptyPrefix_ShouldReturnAllVariables()
    {
        // Arrange
        var environmentVariables = new Dictionary<string, string>
        {
            ["HOME"] = "/home/user",
            ["PATH"] = "/bin:/usr/bin"
        };

        var context = new CompletionContext
        {
            CommandLine = "echo $",
            CursorPosition = 6,
            Tokens = new[] { "echo", "$" },
            CurrentTokenIndex = 1,
            CurrentToken = "$",
            EnvironmentVariables = environmentVariables
        };

        // Act
        var result = await _provider.GetCompletionsAsync(context);

        // Assert
        var completions = result.ToList();
        Assert.Equal(2, completions.Count);
        Assert.Contains(completions, c => c.Text == "$HOME");
        Assert.Contains(completions, c => c.Text == "$PATH");
    }

    [Fact]
    public async Task GetCompletionsAsync_WithInvalidContext_ShouldReturnEmpty()
    {
        // Arrange
        var context = new CompletionContext
        {
            CommandLine = "echo HOME",
            CursorPosition = 9,
            Tokens = new[] { "echo", "HOME" },
            CurrentTokenIndex = 1,
            CurrentToken = "HOME",
            EnvironmentVariables = new Dictionary<string, string>()
        };

        // Act
        var result = await _provider.GetCompletionsAsync(context);

        // Assert
        Assert.Empty(result);
    }
}
