using System.Text;
using System.Text.RegularExpressions;

namespace HackerOs.Theming.Abstractions;

/// <summary>One warning or error found while reading a <c>.theme</c> file.</summary>
/// <param name="Line">1-based line number, or <see langword="null"/> for the whole file.</param>
/// <param name="Message">Human-readable reason.</param>
/// <param name="IsError"><see langword="true"/> when the whole theme is refused; otherwise the line is ignored.</param>
/// <param name="Path">File path, when known.</param>
public sealed record ThemeFileDiagnostic(int? Line, string Message, bool IsError, string? Path = null)
{
    /// <inheritdoc />
    public override string ToString() =>
        $"{Path ?? "theme"}{(Line is { } line ? $":{line}" : string.Empty)}: {(IsError ? "error" : "warning")}: {Message}";
}

/// <summary>Result of <see cref="ThemeFile.Parse"/>.</summary>
/// <param name="Theme">The theme, or <see langword="null"/> when an error refused it.</param>
/// <param name="Diagnostics">Every warning and error, in line order.</param>
public sealed record ThemeFileParseResult(UserTheme? Theme, IReadOnlyList<ThemeFileDiagnostic> Diagnostics);

/// <summary>
/// Reads and writes the <c>.theme</c> format of ADR 0103 §2.2: the line-based format of the settings
/// files (ADR 0044) with a <c>[theme]</c> section (<c>id</c>, <c>name</c>, <c>base</c>,
/// <c>platforms</c>) and a <c>[tokens]</c> section of verified token values.
/// </summary>
/// <remarks>
/// Like the settings files, reading is tolerant (ADR 0044 §5): an unknown token, an invalid value or
/// a malformed line is ignored with a warning and the rest of the file still applies. Only a missing
/// or invalid identity (<c>id</c>, <c>name</c>, <c>base</c>) refuses the whole theme.
/// </remarks>
public static partial class ThemeFile
{
    /// <summary>File extension of theme files, as <c>index.theme</c> on freedesktop.</summary>
    public const string Extension = ".theme";

    /// <summary>Media type used for theme files in file associations.</summary>
    public const string MediaType = "text/x-hackeros-theme";

    /// <summary>Largest theme file accepted, in bytes (64 KiB): a theme is a short list of values.</summary>
    public const int MaxFileBytes = 64 * 1024;

    /// <summary>Longest display name, in characters.</summary>
    public const int MaxNameLength = 80;

    private const string ThemeSection = "theme";
    private const string TokensSection = "tokens";

    /// <summary>Checks a theme identifier: lowercase letters, digits and dashes, 1 to 64 characters.</summary>
    /// <param name="id">Candidate identifier.</param>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>Parses a theme file.</summary>
    /// <param name="content">Whole file content.</param>
    /// <param name="origin">Folder the file came from.</param>
    /// <param name="path">File path, for diagnostics and <see cref="UserTheme.SourcePath"/>.</param>
    public static ThemeFileParseResult Parse(string content, ThemeOrigin origin = ThemeOrigin.User, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        List<ThemeFileDiagnostic> diagnostics = [];
        void Warn(int? line, string message) => diagnostics.Add(new(line, message, IsError: false, path));
        ThemeFileParseResult Fail(int? line, string message)
        {
            diagnostics.Add(new(line, message, IsError: true, path));
            return new ThemeFileParseResult(null, diagnostics);
        }

        if (Encoding.UTF8.GetByteCount(content) > MaxFileBytes)
        {
            return Fail(null, $"The file is larger than {MaxFileBytes / 1024} KiB.");
        }

        string? id = null;
        string? name = null;
        string? baseThemeId = null;
        string? platformsValue = null;
        int platformsLine = 0;
        List<KeyValuePair<string, string>> tokens = [];
        string? section = null;
        string[] lines = content.ReplaceLineEndings("\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = lines[index].Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line[0] == '[')
            {
                if (line[^1] != ']')
                {
                    Warn(lineNumber, "Malformed section header; line ignored.");
                    section = null;
                    continue;
                }

                section = line[1..^1].Trim().ToLowerInvariant();
                if (section is not (ThemeSection or TokensSection))
                {
                    Warn(lineNumber, $"Unknown section [{section}]; its lines are ignored.");
                }

                continue;
            }

            int equals = line.IndexOf('=');
            if (equals <= 0)
            {
                Warn(lineNumber, "Expected 'key = value'; line ignored.");
                continue;
            }

            string key = line[..equals].Trim();
            string value = line[(equals + 1)..].Trim();
            switch (section)
            {
                case ThemeSection:
                    switch (key)
                    {
                        case "id": id = value; break;
                        case "name": name = value; break;
                        case "base": baseThemeId = value; break;
                        case "platforms": platformsValue = value; platformsLine = lineNumber; break;
                        default: Warn(lineNumber, $"Unknown key '{key}' in [theme]; ignored."); break;
                    }

                    break;
                case TokensSection:
                    if (!ThemeTokenValidator.TryValidate(key, value, out string normalized, out string? error))
                    {
                        Warn(lineNumber, $"{error} Token '{key}' ignored.");
                        break;
                    }

                    int existing = tokens.FindIndex(pair => pair.Key == key);
                    if (existing >= 0)
                    {
                        Warn(lineNumber, $"Token '{key}' is set twice; the last value wins.");
                        tokens.RemoveAt(existing);
                    }

                    tokens.Add(new(key, normalized));
                    break;
                case null:
                    Warn(lineNumber, "Key outside of any section; line ignored.");
                    break;
                default:
                    // Unknown section, already reported on its header.
                    break;
            }
        }

        if (!IsValidId(id))
        {
            return Fail(null, id is null
                ? "Missing 'id' in [theme]."
                : $"Invalid id '{id}': use 1 to 64 lowercase letters, digits and dashes.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength || name.Any(char.IsControl))
        {
            return Fail(null, $"Missing or invalid 'name' in [theme] (1 to {MaxNameLength} characters).");
        }

        if (!ThemeCatalog.TryGet(baseThemeId, out ThemeDefinition? baseTheme))
        {
            return Fail(null, baseThemeId is null
                ? "Missing 'base' in [theme]."
                : $"Unknown base theme '{baseThemeId}': use a built-in theme such as {WellKnownThemeIds.HackerOs}.");
        }

        List<ThemePlatform> platforms = [];
        if (platformsValue is null)
        {
            platforms.Add(baseTheme.Platform);
        }
        else
        {
            foreach (string part in platformsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                ThemePlatform? platform = part.ToLowerInvariant() switch
                {
                    "desktop" => ThemePlatform.Desktop,
                    "mobile" => ThemePlatform.Mobile,
                    _ => null
                };
                if (platform is null)
                {
                    Warn(platformsLine, $"Unknown platform '{part}'; use desktop or mobile.");
                }
                else if (!platforms.Contains(platform.Value))
                {
                    platforms.Add(platform.Value);
                }
            }

            if (platforms.Count == 0)
            {
                Warn(platformsLine, $"No valid platform; the base theme's ({FormatPlatform(baseTheme.Platform)}) is used.");
                platforms.Add(baseTheme.Platform);
            }
        }

        return new ThemeFileParseResult(
            new UserTheme(id!, name, baseTheme.Id, platforms, tokens, origin, path),
            diagnostics);
    }

    /// <summary>Writes a theme in the canonical <c>.theme</c> layout, tokens in file order.</summary>
    /// <param name="theme">Theme to write.</param>
    public static string Serialize(UserTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        StringBuilder builder = new();
        builder.Append("# HackerOS theme (ADR 0103). Only the tokens listed in the Theme Editor's documentation are read.\n");
        builder.Append("# Missing tokens keep the value of the base theme.\n\n");
        builder.Append('[').Append(ThemeSection).Append("]\n");
        builder.Append("id = ").Append(theme.Id).Append('\n');
        builder.Append("name = ").Append(theme.Name).Append('\n');
        builder.Append("base = ").Append(theme.BaseThemeId).Append('\n');
        builder.Append("platforms = ").Append(string.Join(", ", theme.Platforms.Select(FormatPlatform))).Append("\n\n");
        builder.Append('[').Append(TokensSection).Append("]\n");
        foreach ((string key, string value) in theme.Tokens)
        {
            builder.Append(key).Append(" = ").Append(value).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Gets the lowercase name of a platform as written in <c>platforms</c>.</summary>
    /// <param name="platform">Platform.</param>
    public static string FormatPlatform(ThemePlatform platform) => platform == ThemePlatform.Mobile ? "mobile" : "desktop";

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$")]
    private static partial Regex IdPattern();
}
