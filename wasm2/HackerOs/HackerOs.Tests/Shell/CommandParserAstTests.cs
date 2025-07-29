using Xunit;
using HackerOs.OS.Shell;

namespace HackerOs.Tests.Shell;

/// <summary>
/// Unit tests for CommandParser AST functionality
/// </summary>
public class CommandParserAstTests
{
    [Fact]
    public void ParseCommandLineToAST_SingleCommand_ReturnsCorrectAST()
    {
        // Arrange
        var commandLine = "ls -la";

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine);

        // Assert
        Assert.False(ast.IsEmpty);
        Assert.Single(ast.Commands);
        Assert.Equal("ls", ast.Commands[0].Command);
        Assert.Equal(new[] { "-la" }, ast.Commands[0].Arguments);
        Assert.Empty(ast.Operators);
    }

    [Fact]
    public void ParseCommandLineToAST_SimplePipeline_ReturnsCorrectAST()
    {
        // Arrange
        var commandLine = "cat file.txt | grep pattern";

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine);

        // Assert
        Assert.False(ast.IsEmpty);
        Assert.Equal(2, ast.Commands.Count);
        Assert.Single(ast.Operators);
        
        // First command
        Assert.Equal("cat", ast.Commands[0].Command);
        Assert.Equal(new[] { "file.txt" }, ast.Commands[0].Arguments);
        
        // Second command
        Assert.Equal("grep", ast.Commands[1].Command);
        Assert.Equal(new[] { "pattern" }, ast.Commands[1].Arguments);
        
        // Operator
        Assert.Equal(PipelineOperator.Pipe, ast.Operators[0]);
    }

    [Fact]
    public void ParseCommandLineToAST_MultiPipeline_ReturnsCorrectAST()
    {
        // Arrange
        var commandLine = "cat file.txt | grep pattern | sort";

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine);

        // Assert
        Assert.False(ast.IsEmpty);
        Assert.Equal(3, ast.Commands.Count);
        Assert.Equal(2, ast.Operators.Count);
        
        // Commands
        Assert.Equal("cat", ast.Commands[0].Command);
        Assert.Equal("grep", ast.Commands[1].Command);
        Assert.Equal("sort", ast.Commands[2].Command);
        
        // Operators
        Assert.Equal(PipelineOperator.Pipe, ast.Operators[0]);
        Assert.Equal(PipelineOperator.Pipe, ast.Operators[1]);
    }

    [Fact]
    public void ParseCommandLineToAST_WithRedirection_ReturnsCorrectAST()
    {
        // Arrange
        var commandLine = "cat file.txt > output.txt";

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine);

        // Assert
        Assert.False(ast.IsEmpty);
        Assert.Single(ast.Commands);
        
        var command = ast.Commands[0];
        Assert.Equal("cat", command.Command);
        Assert.Equal(new[] { "file.txt" }, command.Arguments);
        Assert.Single(command.Redirections);
        
        var redirection = command.Redirections[0];
        Assert.Equal(RedirectionType.Output, redirection.Type);
        Assert.Equal("output.txt", redirection.Target);
        Assert.Equal(1, redirection.FileDescriptor);
    }

    [Fact]
    public void ParseCommandLineToAST_EmptyCommand_ReturnsEmptyAST()
    {
        // Arrange
        var commandLine = "";

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine);

        // Assert
        Assert.True(ast.IsEmpty);
        Assert.Empty(ast.Commands);
        Assert.Empty(ast.Operators);
    }

    [Fact]
    public void ParseCommandLineToAST_WithEnvironmentVariables_ExpandsVariables()
    {
        // Arrange
        var commandLine = "echo $HOME";
        var envVars = new Dictionary<string, string> { { "HOME", "/home/user" } };

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine, envVars);

        // Assert
        Assert.False(ast.IsEmpty);
        Assert.Single(ast.Commands);
        
        var command = ast.Commands[0];
        Assert.Equal("echo", command.Command);
        Assert.Equal(new[] { "/home/user" }, command.Arguments);
    }

    [Fact]
    public void ParseCommandLineToAST_ComplexPipelineWithRedirection_ReturnsCorrectAST()
    {
        // Arrange
        var commandLine = "cat file.txt | grep pattern > output.txt";

        // Act
        var ast = CommandParser.ParseCommandLineToAST(commandLine);

        // Assert
        Assert.False(ast.IsEmpty);
        Assert.Equal(2, ast.Commands.Count);
        Assert.Single(ast.Operators);
        
        // First command (cat)
        var catCommand = ast.Commands[0];
        Assert.Equal("cat", catCommand.Command);
        Assert.Equal(new[] { "file.txt" }, catCommand.Arguments);
        Assert.Empty(catCommand.Redirections);
        
        // Second command (grep with redirection)
        var grepCommand = ast.Commands[1];
        Assert.Equal("grep", grepCommand.Command);
        Assert.Equal(new[] { "pattern" }, grepCommand.Arguments);
        Assert.Single(grepCommand.Redirections);
        
        var redirection = grepCommand.Redirections[0];
        Assert.Equal(RedirectionType.Output, redirection.Type);
        Assert.Equal("output.txt", redirection.Target);
        
        // Operator
        Assert.Equal(PipelineOperator.Pipe, ast.Operators[0]);
    }
}
