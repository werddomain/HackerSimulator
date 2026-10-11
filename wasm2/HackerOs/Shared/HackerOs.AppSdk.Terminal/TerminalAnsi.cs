using System.Text;
using System.Text.RegularExpressions;

namespace HackerOs.AppSdk.Terminal;

/// <summary>
/// The ECMA-48 bytes <see cref="TerminalPanel"/> writes to its terminal emulator (ADR 0061):
/// the coloured prompt, newline translation of command output, and the alternate-screen frames
/// of full-screen commands.
/// </summary>
internal static partial class TerminalAnsi
{
    /// <summary>Control Sequence Introducer.</summary>
    public const string Csi = "\x1b[";

    /// <summary>Resets every graphic attribute.</summary>
    public const string Reset = Csi + "0m";

    /// <summary>Bold bright green, the colour bash's default Debian prompt gives user@host.</summary>
    public const string PromptUser = Csi + "1;32m";

    /// <summary>Bold blue, the colour of the prompt's directory.</summary>
    public const string PromptDirectory = Csi + "1;34m";

    /// <summary>Red, for errors the terminal itself reports.</summary>
    public const string Error = Csi + "31m";

    /// <summary>Yellow, for notices the terminal or the OS writes (approval banners, ^C).</summary>
    public const string Notice = Csi + "33m";

    /// <summary>Green, for the welcome banner.</summary>
    public const string Banner = Csi + "32m";

    /// <summary>Erases the screen and the scrollback and homes the cursor, as <c>clear</c> does.</summary>
    public const string ClearScreen = Csi + "H" + Csi + "2J" + Csi + "3J";

    /// <summary>Switches to the alternate screen and clears it (smcup).</summary>
    public const string EnterAlternateScreen = Csi + "?1049h" + Csi + "H" + Csi + "2J";

    /// <summary>Returns to the normal screen, with its content and cursor as they were (rmcup).</summary>
    public const string LeaveAlternateScreen = Csi + "?1049l" + Csi + "?25h";

    /// <summary>
    /// Formats a <c>user@host:dir$</c> prompt in bash's colours and returns its width in cells
    /// (the trailing space included). Any other prompt shape is printed as is.
    /// </summary>
    public static (string Text, int Width) FormatPrompt(string prompt)
    {
        prompt ??= string.Empty;
        string plain = prompt + " ";
        int at = prompt.IndexOf('@');
        int colon = at < 0 ? -1 : prompt.IndexOf(':', at);
        if (at <= 0 || colon < 0 || prompt.Length < 2)
        {
            return (plain, plain.Length);
        }

        char last = prompt[^1];
        bool hasSigil = last is '$' or '#';
        string identity = prompt[..colon];
        string directory = prompt[(colon + 1)..(hasSigil ? prompt.Length - 1 : prompt.Length)];
        string sigil = hasSigil ? last.ToString() : string.Empty;
        string text = PromptUser + identity + Reset + ":" + PromptDirectory + directory + Reset + sigil + " ";
        return (text, plain.Length);
    }

    /// <summary>
    /// Translates a command's output for the terminal the way a tty's <c>ONLCR</c> flag does:
    /// each <c>\n</c> becomes <c>\r\n</c>, so the next line starts in the first column.
    /// </summary>
    public static string TranslateOutput(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n").Replace("\n", "\r\n");

    /// <summary>
    /// Tells whether the terminal cursor is in the first column of a row once
    /// <paramref name="translated"/> (already ONLCR-translated) is written: after a line break or
    /// a cursor-addressing sequence. Text made only of other control sequences (colours) keeps
    /// <paramref name="previous"/>. Used to start the prompt on a fresh row.
    /// </summary>
    public static bool EndsAtLineStart(string translated, bool previous)
    {
        if (string.IsNullOrEmpty(translated))
        {
            return previous;
        }

        Match trailing = TrailingSequences().Match(translated);
        string text = trailing.Success ? translated[..trailing.Index] : translated;
        if (trailing.Success && trailing.Value.Contains('H'))
        {
            return true;
        }

        return text.Length == 0 ? previous : text[^1] is '\n' or '\r';
    }

    [GeneratedRegex(@"(?:\x1b\[[0-?]*[ -/]*[@-~])+$")]
    private static partial Regex TrailingSequences();

    /// <summary>
    /// Draws a full-screen command's frame on the alternate screen: each row is written in
    /// place and cleared to its end, rows the frame does not cover are erased, then the cursor
    /// is placed (or hidden). Rows longer than the viewport are cut, so nothing wraps.
    /// </summary>
    public static string RenderFrame(TerminalScreenFrame frame, int columns, int rows)
    {
        ArgumentNullException.ThrowIfNull(frame);
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);
        StringBuilder output = new();
        output.Append(Csi).Append("?25l");
        int lineCount = Math.Min(frame.Lines.Count, rows);
        for (int row = 0; row < lineCount; row++)
        {
            string line = (frame.Lines[row] ?? string.Empty).Replace('\t', ' '); // one cell, as the frame's cursor column counts it
            if (line.Length > columns)
            {
                line = line[..columns];
            }

            output.Append(Csi).Append(row + 1).Append(";1H").Append(line);
            if (line.Length < columns)
            {
                // Not after a full row: with the cursor parked on the last cell, EL would erase it.
                output.Append(Csi).Append('K');
            }
        }

        if (lineCount < rows)
        {
            output.Append(Csi).Append(lineCount + 1).Append(";1H").Append(Csi).Append('J');
        }

        TerminalCursor cursor = frame.Cursor;
        int cursorRow = Math.Clamp(cursor.Row, 0, rows - 1);
        int cursorColumn = Math.Clamp(cursor.Column, 0, columns - 1);
        output.Append(Csi).Append(cursorRow + 1).Append(';').Append(cursorColumn + 1).Append('H');
        if (cursor.Visible)
        {
            output.Append(Csi).Append("?25h");
        }

        return output.ToString();
    }
}
