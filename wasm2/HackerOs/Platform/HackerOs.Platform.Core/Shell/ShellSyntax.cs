namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// A parsed command line (ADR 0050, section 2): one or more pipelines joined by <c>&amp;&amp;</c>.
/// </summary>
/// <param name="Pipelines">Pipelines in source order; the next one runs only if the previous one exited with 0.</param>
public sealed record ShellCommandList(IReadOnlyList<ShellPipeline> Pipelines);

/// <summary>One pipeline: commands joined by <c>|</c>, whose stages run at the same time.</summary>
/// <param name="Commands">Stages in source order; each stage's output feeds the next stage's input.</param>
public sealed record ShellPipeline(IReadOnlyList<ShellCommand> Commands);

/// <summary>
/// One simple command: its words (command name first) and its output redirections. A command made
/// only of redirections is valid, as in POSIX shells: <c>&gt; f</c> creates or empties <c>f</c>.
/// </summary>
/// <param name="Words">Unexpanded words in source order.</param>
/// <param name="Redirections">Output redirections in source order; the last one receives the output.</param>
public sealed record ShellCommand(IReadOnlyList<ShellWord> Words, IReadOnlyList<ShellRedirection> Redirections);

/// <summary>Kind of an output redirection.</summary>
public enum ShellRedirectionKind
{
    /// <summary><c>&gt;</c>: creates the file or empties it.</summary>
    Truncate,

    /// <summary><c>&gt;&gt;</c>: creates the file or appends to its end.</summary>
    Append
}

/// <summary>One output redirection of a command.</summary>
/// <param name="Kind">Truncate (<c>&gt;</c>) or append (<c>&gt;&gt;</c>).</param>
/// <param name="Target">Unexpanded target word, expanded like any other word before it is opened.</param>
public sealed record ShellRedirection(ShellRedirectionKind Kind, ShellWord Target);

/// <summary>
/// One unexpanded word. It keeps how each piece was quoted, so the expansion step knows what to
/// substitute without ever re-reading the result as shell syntax (ADR 0050, section 3).
/// </summary>
/// <param name="Parts">Pieces of the word in source order.</param>
/// <param name="Offset">Offset of the word's first character in the command line.</param>
/// <param name="Length">Number of source characters the word spans.</param>
public sealed record ShellWord(IReadOnlyList<ShellWordPart> Parts, int Offset, int Length)
{
    /// <summary>
    /// Gets the word's text when it is made only of unquoted, unescaped literal characters (the
    /// only words a shell considers for alias expansion); otherwise <see langword="null"/>.
    /// </summary>
    public string? PlainText =>
        Parts.Count == 1 && Parts[0] is ShellLiteralPart { Quoted: false } literal ? literal.Text : null;
}

/// <summary>One piece of a <see cref="ShellWord"/>.</summary>
public abstract record ShellWordPart;

/// <summary>Literal text.</summary>
/// <param name="Text">The characters, quotes and escapes already removed.</param>
/// <param name="Quoted">
/// <see langword="true"/> when the text came from quotes or a backslash escape, so it can never be
/// taken for an assignment, an alias name or a tilde.
/// </param>
public sealed record ShellLiteralPart(string Text, bool Quoted) : ShellWordPart;

/// <summary><c>$NAME</c> or <c>${NAME}</c>: the variable's value, or an empty string when it is not set.</summary>
/// <param name="Name">Variable name, matching <c>[A-Za-z_][A-Za-z0-9_]*</c>.</param>
public sealed record ShellVariablePart(string Name) : ShellWordPart;

/// <summary><c>$?</c>: the exit code of the last pipeline this shell ran.</summary>
public sealed record ShellExitStatusPart : ShellWordPart;

/// <summary>An unquoted <c>~</c> at the start of a word, alone or before <c>/</c>: the value of <c>HOME</c>.</summary>
public sealed record ShellTildePart : ShellWordPart;

/// <summary>
/// A structured syntax error (ADR 0014, kept by ADR 0050): a stable code, a message and the
/// offending source span. The parser returns it instead of throwing for user input.
/// </summary>
/// <param name="Code">Stable machine-readable code, one of <see cref="ShellSyntaxErrorCodes"/>.</param>
/// <param name="Message">Human-readable message, printed after <c>hsh: </c>.</param>
/// <param name="Offset">Offset of the first offending character.</param>
/// <param name="Length">Number of offending characters (at least 1, except at the end of the line).</param>
public sealed record ShellSyntaxError(string Code, string Message, int Offset, int Length);

/// <summary>Stable codes of <see cref="ShellSyntaxError"/>.</summary>
public static class ShellSyntaxErrorCodes
{
    /// <summary>An operator that ADR 0050 still refuses: <c>;</c>, <c>||</c>, <c>&amp;</c>, <c>&lt;</c>, <c>2&gt;</c>, <c>&amp;&gt;</c>, a backquote, <c>$(</c>, <c>(</c> or <c>)</c>.</summary>
    public const string Unsupported = "shell.syntax.unsupported";

    /// <summary>A first word of the form <c>NAME=value</c>: the shell asks for <c>export NAME=value</c>.</summary>
    public const string Assignment = "shell.syntax.assignment";

    /// <summary>An operator at the start or end of the line, or two operators in a row.</summary>
    public const string UnexpectedToken = "shell.syntax.unexpected-token";

    /// <summary>A <c>&gt;</c> or <c>&gt;&gt;</c> with no word after it.</summary>
    public const string MissingRedirectTarget = "shell.syntax.missing-redirect-target";

    /// <summary>A single or double quote that is never closed.</summary>
    public const string UnterminatedQuote = "shell.syntax.unterminated-quote";

    /// <summary>A backslash as the last character of the line.</summary>
    public const string TrailingEscape = "shell.syntax.trailing-escape";

    /// <summary>A <c>${</c> with no closing brace or with an invalid name inside.</summary>
    public const string BadSubstitution = "shell.syntax.bad-substitution";
}

/// <summary>Outcome of <see cref="ShellParser.Parse"/>.</summary>
/// <param name="Commands">The parsed line, or <see langword="null"/> on error or for a blank line.</param>
/// <param name="Error">The syntax error, or <see langword="null"/> on success.</param>
public sealed record ShellParseResult(ShellCommandList? Commands, ShellSyntaxError? Error)
{
    /// <summary>Gets whether the line parsed without error (a blank line included).</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets whether the line is blank: nothing to run, no history entry.</summary>
    public bool IsEmpty => Error is null && Commands is null;
}
