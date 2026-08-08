using HackerOs.App.Abstractions;
using HackerOs.Apps.Terminal;
using HackerOs.Platform.Core;
using Xunit;

namespace HackerOs.Apps.Terminal.Tests;

public sealed class TerminalSessionTests
{
    [Fact]
    public void ShellParser_parses_quoted_arguments_and_returns_diagnostics_on_syntax_error()
    {
        ShellParseResult valid = ShellParser.Parse("echo \"hello world\" 'foo bar' baz\\ qux");
        Assert.True(valid.IsSuccess);
        Assert.Equal("echo", valid.CommandName);
        Assert.Equal(["hello world", "foo bar", "baz qux"], valid.Arguments);

        ShellParseResult unterminated = ShellParser.Parse("echo \"hello");
        Assert.False(unterminated.IsSuccess);
        Assert.Contains("unterminated double quote", unterminated.ErrorMessage);

        ShellParseResult trailingEscape = ShellParser.Parse("echo hello\\");
        Assert.False(trailingEscape.IsSuccess);
        Assert.Contains("trailing escape character", trailingEscape.ErrorMessage);
    }

    [Fact]
    public void TerminalSession_manages_working_directory_environment_and_prompt()
    {
        TerminalSession session = new("root", "/home/root");
        Assert.Equal("root@hackeros:~#", session.GetPrompt());

        session.SetCwd("projects");
        Assert.Equal("/home/root/projects", session.Cwd);
        Assert.Equal("root@hackeros:~/projects#", session.GetPrompt());

        session.SetCwd("..");
        Assert.Equal("/home/root", session.Cwd);

        session.SetCwd("/");
        Assert.Equal("/", session.Cwd);
        Assert.Equal("root@hackeros:/#", session.GetPrompt());
    }

    [Fact]
    public void ResolvePath_ComputesTargetWithoutMutatingCwd()
    {
        TerminalSession session = new("user", "/home/user");

        Assert.Equal("/home/user/projects", session.ResolvePath("projects"));
        Assert.Equal("/etc", session.ResolvePath("/etc"));
        Assert.Equal("/home", session.ResolvePath(".."));
        Assert.Equal("/home/user", session.ResolvePath("~"));

        // None of the above should have changed the actual working directory.
        Assert.Equal("/home/user", session.Cwd);
    }

    [Fact]
    public void TerminalSession_manages_command_history_navigation()
    {
        TerminalSession session = new("user", "/home/user");
        session.AddHistory("echo first");
        session.AddHistory("ls -la");

        Assert.Equal("ls -la", session.NavigateHistory(-1, "draft"));
        Assert.Equal("echo first", session.NavigateHistory(-1, "draft"));
        Assert.Equal("ls -la", session.NavigateHistory(1, "draft"));
        Assert.Equal(string.Empty, session.NavigateHistory(1, "draft"));
    }

    [Fact]
    public void TerminalCommandResolver_resolves_terminal_apps_by_name_or_alias()
    {
        AppManifest manifest = CreateTerminalManifest("org.hackeros.echo", "echo", ["print"]);
        AppCatalogBuildResult result = AppCatalog.Build([manifest]);
        Assert.True(result.IsSuccess);

        TerminalCommandResolver resolver = new(result.Catalog!);
        TerminalCommandResolution? resolvedByName = resolver.Resolve("echo", ["hello"]);
        Assert.NotNull(resolvedByName);
        Assert.Equal("org.hackeros.echo", resolvedByName.AppId);

        TerminalCommandResolution? resolvedByAlias = resolver.Resolve("print", ["hello"]);
        Assert.NotNull(resolvedByAlias);
        Assert.Equal("org.hackeros.echo", resolvedByAlias.AppId);

        Assert.Null(resolver.Resolve("nonexistent", []));
    }

    private static AppManifest CreateTerminalManifest(string id, string commandName, IReadOnlyList<string> aliases) =>
        new()
        {
            Id = id,
            Name = commandName,
            Version = "1.0.0",
            PublisherId = "pub.hackeros",
            Description = "Terminal command",
            Kind = AppKind.Terminal,
            EntryPoint = new AppEntryPointManifest("TestAssembly.dll", "TestAssembly.TestCommand"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
            Resources = AppResourceProfileManifest.None,
            Terminal = new TerminalCommandManifest(commandName, aliases, $"{commandName} [args]")
        };
}
