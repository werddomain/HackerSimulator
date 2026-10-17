namespace HackerOs.Theming.Abstractions;

/// <summary>One theme file read from disk, before parsing.</summary>
/// <param name="Path">Absolute virtual path.</param>
/// <param name="Origin">Folder it came from.</param>
/// <param name="Content">Whole content.</param>
public sealed record ThemeFileSource(string Path, ThemeOrigin Origin, string Content);

/// <summary>Result of loading the user themes: the accepted themes and every warning.</summary>
/// <param name="Themes">Accepted themes: <c>/etc</c> first, then <c>~</c>, each folder by file name.</param>
/// <param name="Diagnostics">Warnings and errors of every file.</param>
public sealed record UserThemeLibraryResult(IReadOnlyList<UserTheme> Themes, IReadOnlyList<ThemeFileDiagnostic> Diagnostics)
{
    /// <summary>Gets an empty result.</summary>
    public static UserThemeLibraryResult Empty { get; } = new([], []);

    /// <summary>Finds an accepted theme by id.</summary>
    /// <param name="id">Theme id.</param>
    public UserTheme? Find(string? id) => id is null ? null : Themes.FirstOrDefault(theme => theme.Id == id);
}

/// <summary>
/// The catalog of user themes (ADR 0103 §2.2): files of <c>/etc/hackeros/themes/</c> (administrator)
/// and <c>~/.config/hackeros/themes/</c> (user), shown with the built-in themes. An identifier that
/// repeats a built-in theme, or a <c>~</c> identifier that repeats an <c>/etc</c> one, is refused:
/// a theme never hides another one.
/// </summary>
public static class UserThemeLibrary
{
    /// <summary>Folder of the themes installed for every user.</summary>
    public const string SystemDirectory = "/etc/hackeros/themes";

    /// <summary>Gets the folder of a user's own themes.</summary>
    /// <param name="homeDirectory">The user's home, such as <c>/home/alice</c>.</param>
    public static string GetUserDirectory(string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        return $"{homeDirectory.TrimEnd('/')}/.config/hackeros/themes";
    }

    /// <summary>Gets the path a user theme is saved to: <c>~/.config/hackeros/themes/&lt;id&gt;.theme</c>.</summary>
    /// <param name="homeDirectory">The user's home.</param>
    /// <param name="themeId">Valid theme id.</param>
    public static string GetUserThemePath(string homeDirectory, string themeId)
    {
        if (!ThemeFile.IsValidId(themeId))
        {
            throw new ArgumentException($"Invalid theme id '{themeId}'.", nameof(themeId));
        }

        return $"{GetUserDirectory(homeDirectory)}/{themeId}{ThemeFile.Extension}";
    }

    /// <summary>Parses files and applies the no-masking rule.</summary>
    /// <param name="files">Files of both folders, in any order.</param>
    public static UserThemeLibraryResult Build(IEnumerable<ThemeFileSource> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        List<UserTheme> themes = [];
        List<ThemeFileDiagnostic> diagnostics = [];
        IEnumerable<ThemeFileSource> ordered = files
            .OrderBy(static file => file.Origin)
            .ThenBy(static file => file.Path, StringComparer.Ordinal);
        foreach (ThemeFileSource file in ordered)
        {
            ThemeFileParseResult parsed = ThemeFile.Parse(file.Content, file.Origin, file.Path);
            diagnostics.AddRange(parsed.Diagnostics);
            if (parsed.Theme is not { } theme)
            {
                continue;
            }

            if (ThemeCatalog.TryGet(theme.Id, out _))
            {
                diagnostics.Add(new(null, $"The id '{theme.Id}' is a built-in theme; this file is ignored.", IsError: true, file.Path));
                continue;
            }

            if (themes.FirstOrDefault(existing => existing.Id == theme.Id) is { } previous)
            {
                diagnostics.Add(new(
                    null,
                    $"The id '{theme.Id}' is already used by {previous.SourcePath}; this file is ignored.",
                    IsError: true,
                    file.Path));
                continue;
            }

            themes.Add(theme);
        }

        return new UserThemeLibraryResult(themes, diagnostics);
    }

    /// <summary>
    /// Reads every <c>.theme</c> file of both folders through the caller's own file access (an app
    /// passes <c>HackDirectory</c>/<c>HackFile</c>, the shell its gateways), then calls <see cref="Build"/>.
    /// A missing or unreadable folder or file is skipped with a warning.
    /// </summary>
    /// <param name="listFiles">Lists the files of a folder (absolute paths); may throw when the folder is missing.</param>
    /// <param name="readFile">Reads a file as UTF-8 text.</param>
    /// <param name="homeDirectory">Session user's home, or <see langword="null"/> to read <c>/etc</c> only.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public static async Task<UserThemeLibraryResult> LoadAsync(
        Func<string, CancellationToken, Task<IReadOnlyList<string>>> listFiles,
        Func<string, CancellationToken, Task<string>> readFile,
        string? homeDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listFiles);
        ArgumentNullException.ThrowIfNull(readFile);
        List<ThemeFileSource> files = [];
        List<ThemeFileDiagnostic> diagnostics = [];
        List<(string Directory, ThemeOrigin Origin)> folders = [(SystemDirectory, ThemeOrigin.System)];
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            folders.Add((GetUserDirectory(homeDirectory), ThemeOrigin.User));
        }

        foreach ((string directory, ThemeOrigin origin) in folders)
        {
            IReadOnlyList<string> paths;
            try
            {
                paths = await listFiles(directory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A user who never saved a theme has no folder: that is the normal case, not a warning.
                continue;
            }

            foreach (string path in paths.Where(static path => path.EndsWith(ThemeFile.Extension, StringComparison.Ordinal)))
            {
                try
                {
                    files.Add(new ThemeFileSource(path, origin, await readFile(path, cancellationToken).ConfigureAwait(false)));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    diagnostics.Add(new(null, $"Cannot read the file: {exception.Message}", IsError: true, path));
                }
            }
        }

        UserThemeLibraryResult built = Build(files);
        return built with { Diagnostics = [.. diagnostics, .. built.Diagnostics] };
    }
}

/// <summary>The theme a shell actually renders for a preference (ADR 0103 §2.2, §2.3).</summary>
/// <param name="BaseThemeId">Built-in theme whose stylesheet applies (its <c>data-theme</c>).</param>
/// <param name="UserTheme">User theme whose tokens apply over it, or <see langword="null"/>.</param>
/// <param name="IsFallback"><see langword="true"/> when the requested id was unknown or not offered on this platform.</param>
public sealed record ResolvedTheme(string BaseThemeId, UserTheme? UserTheme, bool IsFallback)
{
    /// <summary>
    /// Resolves the theme id written in <c>appearance.config</c> for one form factor: a built-in
    /// theme of that form factor, a user theme offered on it, or the form factor's default theme.
    /// </summary>
    /// <param name="themeId">Requested id.</param>
    /// <param name="platform">Active form factor.</param>
    /// <param name="userThemes">Accepted user themes.</param>
    /// <remarks>
    /// A user theme offered on a form factor its base does not target (a <c>hackeros</c>-based theme
    /// with <c>platforms = desktop, mobile</c>, as in ADR 0103) applies its tokens over that form
    /// factor's default theme there, since a desktop stylesheet cannot skin the mobile shell.
    /// </remarks>
    public static ResolvedTheme Resolve(string? themeId, ThemePlatform platform, IEnumerable<UserTheme>? userThemes)
    {
        string defaultId = platform == ThemePlatform.Mobile
            ? ThemePreferences.Default.MobileThemeId
            : ThemePreferences.Default.DesktopThemeId;
        if (ThemeCatalog.TryGet(themeId, out ThemeDefinition? builtIn))
        {
            return builtIn.Platform == platform
                ? new ResolvedTheme(builtIn.Id, null, IsFallback: false)
                : new ResolvedTheme(defaultId, null, IsFallback: true);
        }

        UserTheme? user = userThemes?.FirstOrDefault(theme => theme.Id == themeId);
        if (user is null || !user.Supports(platform))
        {
            return new ResolvedTheme(defaultId, null, IsFallback: true);
        }

        string baseId = ThemeCatalog.Get(user.BaseThemeId).Platform == platform ? user.BaseThemeId : defaultId;
        return new ResolvedTheme(baseId, user, IsFallback: false);
    }
}
