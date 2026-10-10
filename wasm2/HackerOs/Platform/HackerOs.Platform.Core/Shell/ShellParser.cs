using System.Text;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Parses one command line into a <see cref="ShellCommandList"/> (ADR 0050, section 2), or a
/// structured <see cref="ShellSyntaxError"/>. Never throws for user input.
/// </summary>
/// <remarks>
/// <para>
/// Grammar:
/// <c>list := pipeline ("&amp;&amp;" pipeline)*</c>,
/// <c>pipeline := command ("|" command)*</c>,
/// <c>command := (word | redirection)+</c>,
/// <c>redirection := (">" | ">>") word</c>.
/// </para>
/// <para>
/// Words, quotes and escapes follow ADR 0014: single quotes are literal; double quotes keep
/// whitespace, expand variables and understand <c>\"</c>, <c>\\</c>, <c>\$</c>, <c>\n</c>,
/// <c>\r</c> and <c>\t</c> (any other backslash stays as typed); outside quotes a backslash makes
/// the next character literal. Operators need no surrounding spaces (<c>ls|wc</c>).
/// </para>
/// <para>
/// The parser runs in two steps, like a real shell: a lexer cuts the line into words and
/// operators, then the token list is checked against the grammar. Variables and <c>~</c> are only
/// recorded here; <see cref="ShellInterpreter"/> substitutes them right before a pipeline starts.
/// </para>
/// </remarks>
public static class ShellParser
{
    /// <summary>Parses <paramref name="commandLine"/>.</summary>
    /// <param name="commandLine">One submitted line.</param>
    /// <returns>The tree, a blank result, or the first syntax error.</returns>
    public static ShellParseResult Parse(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return new ShellParseResult(null, null);
        }

        Lexer lexer = new(commandLine);
        if (!lexer.Run(out ShellSyntaxError? lexError))
        {
            return new ShellParseResult(null, lexError);
        }

        return Build(lexer.Tokens, commandLine.Length);
    }

    /// <summary>Checks the token list against the grammar and builds the tree.</summary>
    private static ShellParseResult Build(List<Token> tokens, int lineLength)
    {
        List<ShellPipeline> pipelines = [];
        List<ShellCommand> commands = [];
        List<ShellWord> words = [];
        List<ShellRedirection> redirections = [];
        Token? lastSeparator = null;

        for (int index = 0; index < tokens.Count; index++)
        {
            Token token = tokens[index];
            switch (token.Kind)
            {
                case TokenKind.Word:
                    words.Add(token.Word!);
                    break;

                case TokenKind.Truncate or TokenKind.Append:
                    Token? target = index + 1 < tokens.Count ? tokens[index + 1] : null;
                    if (target is not { Kind: TokenKind.Word })
                    {
                        return Error(
                            ShellSyntaxErrorCodes.MissingRedirectTarget,
                            target is null
                                ? $"syntax error: missing file name after `{token.Text}'"
                                : $"syntax error near unexpected token `{target.Text}'",
                            target ?? token);
                    }

                    redirections.Add(new ShellRedirection(
                        token.Kind == TokenKind.Append ? ShellRedirectionKind.Append : ShellRedirectionKind.Truncate,
                        target.Word!));
                    index++;
                    break;

                case TokenKind.Pipe or TokenKind.AndAnd:
                    if (words.Count == 0 && redirections.Count == 0)
                    {
                        return Error(ShellSyntaxErrorCodes.UnexpectedToken, $"syntax error near unexpected token `{token.Text}'", token);
                    }

                    if (CheckAssignment(words) is { } assignment)
                    {
                        return new ShellParseResult(null, assignment);
                    }

                    commands.Add(new ShellCommand([.. words], [.. redirections]));
                    words.Clear();
                    redirections.Clear();
                    if (token.Kind == TokenKind.AndAnd)
                    {
                        pipelines.Add(new ShellPipeline([.. commands]));
                        commands.Clear();
                    }

                    lastSeparator = token;
                    break;
            }
        }

        if (words.Count == 0 && redirections.Count == 0)
        {
            // Only reachable after a trailing `|` or `&&`: a blank line returned earlier.
            return new ShellParseResult(null, new ShellSyntaxError(
                ShellSyntaxErrorCodes.UnexpectedToken,
                $"syntax error: unexpected end of line after `{lastSeparator!.Text}'",
                lineLength,
                0));
        }

        if (CheckAssignment(words) is { } lastAssignment)
        {
            return new ShellParseResult(null, lastAssignment);
        }

        commands.Add(new ShellCommand([.. words], [.. redirections]));
        pipelines.Add(new ShellPipeline([.. commands]));
        return new ShellParseResult(new ShellCommandList(pipelines), null);
    }

    /// <summary>
    /// Refuses <c>NAME=value</c> as a command's first word (ADR 0050, section 2): it would otherwise
    /// silently run a command literally named <c>NAME=value</c>.
    /// </summary>
    private static ShellSyntaxError? CheckAssignment(List<ShellWord> words)
    {
        if (words.Count == 0 || words[0].Parts[0] is not ShellLiteralPart { Quoted: false } first)
        {
            return null;
        }

        int equals = first.Text.IndexOf('=');
        if (equals <= 0 || !IsName(first.Text.AsSpan(0, equals)))
        {
            return null;
        }

        string name = first.Text[..equals];
        return new ShellSyntaxError(
            ShellSyntaxErrorCodes.Assignment,
            $"{name}=…: variable assignment is not supported, use export {name}=value",
            words[0].Offset,
            words[0].Length);
    }

    private static ShellParseResult Error(string code, string message, Token at) =>
        new(null, new ShellSyntaxError(code, message, at.Offset, at.Length));

    /// <summary>Gets whether <paramref name="text"/> is a valid variable name.</summary>
    internal static bool IsName(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || !IsNameStart(text[0]))
        {
            return false;
        }

        foreach (char ch in text[1..])
        {
            if (!IsNameChar(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNameStart(char ch) => ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';

    private static bool IsNameChar(char ch) => IsNameStart(ch) || ch is >= '0' and <= '9';

    private enum TokenKind
    {
        Word,
        Pipe,
        AndAnd,
        Truncate,
        Append
    }

    private sealed record Token(TokenKind Kind, string Text, int Offset, int Length, ShellWord? Word = null);

    /// <summary>Cuts a line into words and operators, recording quoting per word piece.</summary>
    private sealed class Lexer(string line)
    {
        private readonly List<ShellWordPart> _parts = [];
        private readonly StringBuilder _literal = new();
        private bool _literalQuoted;
        private bool _inWord;
        private int _wordStart;
        private int _position;

        public List<Token> Tokens { get; } = [];

        public bool Run(out ShellSyntaxError? error)
        {
            error = null;
            while (_position < line.Length)
            {
                char ch = line[_position];
                if (char.IsWhiteSpace(ch))
                {
                    EndWord();
                    _position++;
                    continue;
                }

                error = ch switch
                {
                    '\'' => ReadSingleQuoted(),
                    '"' => ReadDoubleQuoted(),
                    '\\' => ReadEscape(),
                    '$' => ReadDollar(quoted: false),
                    '~' when !_inWord => ReadTilde(),
                    '|' or '&' or '>' or ';' or '<' or '`' or '(' or ')' => ReadOperator(ch),
                    _ => AppendLiteral(ch, quoted: false)
                };

                if (error is not null)
                {
                    return false;
                }
            }

            EndWord();
            return true;
        }

        private ShellSyntaxError? AppendLiteral(char ch, bool quoted, int advance = 1)
        {
            StartWord();
            if (_literal.Length > 0 && _literalQuoted != quoted)
            {
                FlushLiteral();
            }

            _literalQuoted = quoted;
            _literal.Append(ch);
            _position += advance;
            return null;
        }

        private ShellSyntaxError? ReadSingleQuoted()
        {
            int start = _position;
            StartWord();
            int end = line.IndexOf('\'', start + 1);
            if (end < 0)
            {
                return new ShellSyntaxError(ShellSyntaxErrorCodes.UnterminatedQuote, "syntax error: unterminated single quote", start, line.Length - start);
            }

            for (int i = start + 1; i < end; i++)
            {
                AppendQuotedChar(line[i]);
            }

            _position = end + 1;
            return null;
        }

        private ShellSyntaxError? ReadDoubleQuoted()
        {
            int start = _position;
            StartWord();
            _position++;
            while (_position < line.Length)
            {
                char ch = line[_position];
                if (ch == '"')
                {
                    _position++;
                    return null;
                }

                if (ch == '\\' && _position + 1 < line.Length)
                {
                    char next = line[_position + 1];
                    char? escaped = next switch
                    {
                        '"' or '\\' or '$' => next,
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => null
                    };
                    if (escaped is { } value)
                    {
                        AppendQuotedChar(value);
                        _position += 2;
                    }
                    else
                    {
                        // Unknown escapes keep their backslash, as inside POSIX double quotes.
                        AppendQuotedChar('\\');
                        _position++;
                    }

                    continue;
                }

                if (ch == '$')
                {
                    if (ReadDollar(quoted: true) is { } error)
                    {
                        return error;
                    }

                    continue;
                }

                AppendQuotedChar(ch);
                _position++;
            }

            return new ShellSyntaxError(ShellSyntaxErrorCodes.UnterminatedQuote, "syntax error: unterminated double quote", start, line.Length - start);
        }

        private ShellSyntaxError? ReadEscape()
        {
            if (_position + 1 >= line.Length)
            {
                return new ShellSyntaxError(ShellSyntaxErrorCodes.TrailingEscape, "syntax error: trailing escape character '\\'", _position, 1);
            }

            return AppendLiteral(line[_position + 1], quoted: true, advance: 2);
        }

        /// <summary>Reads <c>$NAME</c>, <c>${NAME}</c>, <c>$?</c>; a <c>$</c> before anything else is literal.</summary>
        private ShellSyntaxError? ReadDollar(bool quoted)
        {
            int start = _position;
            char next = _position + 1 < line.Length ? line[_position + 1] : '\0';
            if (next == '(' && !quoted)
            {
                return Unsupported("$(", start, 2);
            }

            if (next == '?')
            {
                AddPart(new ShellExitStatusPart());
                _position += 2;
                return null;
            }

            if (next == '{')
            {
                int close = line.IndexOf('}', start + 2);
                string name = close < 0 ? string.Empty : line[(start + 2)..close];
                if (close < 0 || (name != "?" && !IsName(name)))
                {
                    int length = close < 0 ? line.Length - start : close - start + 1;
                    return new ShellSyntaxError(ShellSyntaxErrorCodes.BadSubstitution, $"{line.Substring(start, length)}: bad substitution", start, length);
                }

                AddPart(name == "?" ? new ShellExitStatusPart() : new ShellVariablePart(name));
                _position = close + 1;
                return null;
            }

            if (IsNameStart(next))
            {
                int end = _position + 1;
                while (end < line.Length && IsNameChar(line[end]))
                {
                    end++;
                }

                AddPart(new ShellVariablePart(line[(_position + 1)..end]));
                _position = end;
                return null;
            }

            return AppendLiteral('$', quoted);
        }

        /// <summary>An unquoted <c>~</c> opening a word is a tilde only before the end of the word or <c>/</c>.</summary>
        private ShellSyntaxError? ReadTilde()
        {
            char next = _position + 1 < line.Length ? line[_position + 1] : ' ';
            if (char.IsWhiteSpace(next) || next is '/' or '|' or '&' or '>' or ';' or '<')
            {
                AddPart(new ShellTildePart());
                _position++;
                return null;
            }

            return AppendLiteral('~', quoted: false);
        }

        private ShellSyntaxError? ReadOperator(char ch)
        {
            int start = _position;
            char next = _position + 1 < line.Length ? line[_position + 1] : '\0';
            switch (ch)
            {
                case '|' when next == '|':
                    return Unsupported("||", start, 2);
                case '|':
                    return AddOperator(TokenKind.Pipe, "|");
                case '&' when next == '&':
                    return AddOperator(TokenKind.AndAnd, "&&");
                case '&' when next == '>':
                    return Unsupported("&>", start, 2);
                case '&':
                    return Unsupported("&", start, 1);
                case '>' when IsDescriptorPrefix():
                    return Unsupported($"{_literal}>", _wordStart, start - _wordStart + 1);
                case '>' when next == '&':
                    return Unsupported(">&", start, 2);
                case '>' when next == '>':
                    return AddOperator(TokenKind.Append, ">>");
                case '>':
                    return AddOperator(TokenKind.Truncate, ">");
                default:
                    return Unsupported(ch.ToString(), start, 1);
            }
        }

        /// <summary>
        /// Gets whether the word being read is a bare file-descriptor number (<c>2</c> in
        /// <c>2&gt;</c>), which a POSIX shell would read as a descriptor redirection.
        /// </summary>
        private bool IsDescriptorPrefix() =>
            _inWord && _parts.Count == 0 && !_literalQuoted && _literal.Length > 0
            && _literal.ToString().All(char.IsAsciiDigit);

        private ShellSyntaxError? AddOperator(TokenKind kind, string text)
        {
            EndWord();
            Tokens.Add(new Token(kind, text, _position, text.Length));
            _position += text.Length;
            return null;
        }

        private static ShellSyntaxError Unsupported(string text, int offset, int length) =>
            new(ShellSyntaxErrorCodes.Unsupported,
                $"syntax error: `{text}' is not supported (quote or escape it to pass it literally)",
                offset,
                length);

        private void AppendQuotedChar(char ch)
        {
            if (_literal.Length > 0 && !_literalQuoted)
            {
                FlushLiteral();
            }

            _literalQuoted = true;
            _literal.Append(ch);
        }

        private void AddPart(ShellWordPart part)
        {
            StartWord();
            FlushLiteral();
            _parts.Add(part);
        }

        private void StartWord()
        {
            if (!_inWord)
            {
                _inWord = true;
                _wordStart = _position;
            }
        }

        private void FlushLiteral()
        {
            if (_literal.Length > 0)
            {
                _parts.Add(new ShellLiteralPart(_literal.ToString(), _literalQuoted));
                _literal.Clear();
            }
        }

        private void EndWord()
        {
            if (!_inWord)
            {
                return;
            }

            FlushLiteral();
            if (_parts.Count == 0)
            {
                // Empty quotes ('' or "") still make one empty argument.
                _parts.Add(new ShellLiteralPart(string.Empty, Quoted: true));
            }

            ShellWord word = new([.. _parts], _wordStart, _position - _wordStart);
            Tokens.Add(new Token(TokenKind.Word, line.Substring(_wordStart, _position - _wordStart), _wordStart, _position - _wordStart, word));
            _parts.Clear();
            _inWord = false;
        }
    }
}
