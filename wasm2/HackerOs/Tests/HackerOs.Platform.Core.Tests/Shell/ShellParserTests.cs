using HackerOs.Platform.Core.Shell;

namespace HackerOs.Platform.Core.Tests.Shell;

/// <summary>Grammar of the shell (ADR 0050, section 2; quoting rules of ADR 0014).</summary>
public sealed class ShellParserTests
{
    [Fact]
    public void Quotes_and_escapes_keep_spaces_inside_one_word()
    {
        ShellCommand command = Single("echo \"hello world\" 'foo bar' baz\\ qux \"\"");

        Assert.Equal(["echo", "hello world", "foo bar", "baz qux", ""], Texts(command));
    }

    [Fact]
    public void Double_quote_escapes_follow_adr_0014()
    {
        ShellCommand command = Single("echo \"a\\\"b\\\\c\\$d\\te\\q\"");

        Assert.Equal("a\"b\\c$d\te\\q", Texts(command)[1]);
    }

    [Fact]
    public void Blank_line_is_empty_and_not_an_error()
    {
        ShellParseResult result = ShellParser.Parse("   ");

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Operators_need_no_spaces()
    {
        ShellCommandList list = Parse("ls|wc&&echo a>f>>g");

        Assert.Equal(2, list.Pipelines.Count);
        Assert.Equal(2, list.Pipelines[0].Commands.Count);
        ShellCommand echo = list.Pipelines[1].Commands[0];
        Assert.Equal(["echo", "a"], Texts(echo));
        Assert.Equal(
            [ShellRedirectionKind.Truncate, ShellRedirectionKind.Append],
            echo.Redirections.Select(r => r.Kind));
        Assert.Equal(["f", "g"], echo.Redirections.Select(r => Literal(r.Target)));
    }

    [Fact]
    public void A_command_may_be_only_a_redirection()
    {
        ShellCommand command = Single("> out.txt");

        Assert.Empty(command.Words);
        Assert.Single(command.Redirections);
    }

    [Theory]
    [InlineData("a ; b", ";")]
    [InlineData("a || b", "||")]
    [InlineData("a &", "&")]
    [InlineData("a < f", "<")]
    [InlineData("a 2> f", "2>")]
    [InlineData("a &> f", "&>")]
    [InlineData("a `b`", "`")]
    [InlineData("a $(b)", "$(")]
    [InlineData("(a)", "(")]
    [InlineData("a )", ")")]
    public void Unsupported_operators_are_refused_with_a_clear_error(string line, string token)
    {
        ShellParseResult result = ShellParser.Parse(line);

        Assert.Equal(ShellSyntaxErrorCodes.Unsupported, result.Error?.Code);
        Assert.Contains($"`{token}'", result.Error!.Message);
        Assert.Equal(line.IndexOf(token[0]), result.Error.Offset);
    }

    [Fact]
    public void Unsupported_operators_are_fine_when_quoted_or_escaped()
    {
        ShellCommand command = Single("echo ';' \"a || b\" \\< '$(x)' \"`\" a2>f");

        Assert.Equal(["echo", ";", "a || b", "<", "$(x)", "`", "a2"], Texts(command));
        Assert.Single(command.Redirections);
    }

    [Theory]
    [InlineData("| wc")]
    [InlineData("ls |")]
    [InlineData("ls &&")]
    [InlineData("&& ls")]
    [InlineData("ls | | wc")]
    [InlineData("ls && | wc")]
    public void Operators_at_the_edges_or_doubled_are_syntax_errors(string line)
    {
        Assert.Equal(ShellSyntaxErrorCodes.UnexpectedToken, ShellParser.Parse(line).Error?.Code);
    }

    [Theory]
    [InlineData("echo >")]
    [InlineData("echo > | wc")]
    [InlineData("echo >> && ls")]
    public void A_redirection_needs_a_target(string line)
    {
        Assert.Equal(ShellSyntaxErrorCodes.MissingRedirectTarget, ShellParser.Parse(line).Error?.Code);
    }

    [Fact]
    public void An_assignment_as_first_word_asks_for_export()
    {
        ShellParseResult result = ShellParser.Parse("ls && FOO=bar");

        Assert.Equal(ShellSyntaxErrorCodes.Assignment, result.Error?.Code);
        Assert.Contains("export FOO=value", result.Error!.Message);
        Assert.True(ShellParser.Parse("echo FOO=bar").IsSuccess);
        Assert.True(ShellParser.Parse("'FOO=bar'").IsSuccess);
    }

    [Theory]
    [InlineData("echo \"abc", ShellSyntaxErrorCodes.UnterminatedQuote)]
    [InlineData("echo 'abc", ShellSyntaxErrorCodes.UnterminatedQuote)]
    [InlineData("echo abc\\", ShellSyntaxErrorCodes.TrailingEscape)]
    [InlineData("echo ${HOME", ShellSyntaxErrorCodes.BadSubstitution)]
    [InlineData("echo ${1x}", ShellSyntaxErrorCodes.BadSubstitution)]
    public void Lexical_errors_are_structured(string line, string code)
    {
        ShellParseResult result = ShellParser.Parse(line);

        Assert.Equal(code, result.Error?.Code);
        Assert.True(result.Error!.Offset >= 5);
    }

    [Fact]
    public void Variables_exit_status_and_tilde_are_recorded_not_substituted()
    {
        ShellCommand command = Single("echo $HOME ${USER}x \"$?\" '$HOME' ~ ~/docs ~bob a~ \"~\" $ $5");

        IReadOnlyList<ShellWord> words = command.Words;
        Assert.Equal(new ShellVariablePart("HOME"), words[1].Parts.Single());
        Assert.Equal([new ShellVariablePart("USER"), new ShellLiteralPart("x", false)], words[2].Parts);
        Assert.IsType<ShellExitStatusPart>(words[3].Parts.Single());
        Assert.Equal(new ShellLiteralPart("$HOME", true), words[4].Parts.Single());
        Assert.IsType<ShellTildePart>(words[5].Parts.Single());
        Assert.Equal([new ShellTildePart(), new ShellLiteralPart("/docs", false)], words[6].Parts);
        Assert.Equal("~bob", Literal(words[7]));
        Assert.Equal("a~", Literal(words[8]));
        Assert.Equal(new ShellLiteralPart("~", true), words[9].Parts.Single());
        Assert.Equal("$", Literal(words[10]));
        Assert.Equal("$5", Literal(words[11]));
    }

    private static ShellCommandList Parse(string line)
    {
        ShellParseResult result = ShellParser.Parse(line);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Commands!;
    }

    private static ShellCommand Single(string line) => Parse(line).Pipelines.Single().Commands.Single();

    private static List<string> Texts(ShellCommand command) => [.. command.Words.Select(Literal)];

    private static string Literal(ShellWord word) =>
        string.Concat(word.Parts.Select(part => Assert.IsType<ShellLiteralPart>(part).Text));
}
