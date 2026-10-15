using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Lifecycle;

namespace HackerOs.Platform.Core.Shell;

/// <summary>Represents a resolved command target from the AppCatalog.</summary>
public sealed record ShellCommandResolution(
    string AppId,
    AppManifest Manifest,
    string CommandName,
    IReadOnlyList<string> Arguments);

/// <summary>
/// Resolves executable commands and static catalog aliases against registered catalog manifests
/// (`P2-TERM-004`, `P2-TERM-004A`): the shell's equivalent of a <c>PATH</c> lookup. Used by
/// <see cref="ShellInterpreter"/> to run a command and by the terminal for <c>help</c> and completion.
/// </summary>
public sealed class ShellCommandResolver
{
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry? _enablement;

    /// <summary>Initializes a command resolver over the application catalog.</summary>
    /// <param name="catalog">Application catalog.</param>
    /// <param name="enablement">
    /// Optional launch checkpoint: an uninstalled command (ADR 0048) is then not resolved, so the
    /// shell answers <c>command not found</c> like a real shell whose binary was removed.
    /// </param>
    public ShellCommandResolver(AppCatalog catalog, IAppEnablementRegistry? enablement = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement;
    }

    /// <summary>Attempts to resolve a command line or command name to an installed terminal app manifest.</summary>
    public ShellCommandResolution? Resolve(string commandName, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        foreach (AppManifest manifest in _catalog.Manifests.Values)
        {
            if (!IsInstalledCommand(manifest) || manifest.Terminal is null)
            {
                continue;
            }

            bool matchesName = string.Equals(manifest.Terminal.Name, commandName, StringComparison.OrdinalIgnoreCase);
            bool matchesAlias = manifest.Terminal.Aliases.Any(alias => string.Equals(alias, commandName, StringComparison.OrdinalIgnoreCase));
            bool matchesAppId = string.Equals(manifest.Id, commandName, StringComparison.OrdinalIgnoreCase);

            if (matchesName || matchesAlias || matchesAppId)
            {
                return new ShellCommandResolution(manifest.Id, manifest, commandName, arguments);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a window app by its short name (ADR 0064): the last segment of its id, without the
    /// publisher namespace (<c>text-editor</c> for <c>org.hackeros.text-editor</c>), or its full
    /// id. Used by the shell when no command has the name, like a GUI program found on
    /// <c>PATH</c>.
    /// </summary>
    /// <param name="name">The name typed as the command.</param>
    /// <returns>The app's manifest, or <see langword="null"/> when none (or several) match.</returns>
    public AppManifest? ResolveApplication(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/'))
        {
            return null;
        }

        List<AppManifest> matches = [.. _catalog.Manifests.Values.Where(manifest =>
            manifest.Kind == AppKind.Window
            && (_enablement is null || _enablement.IsEnabled(manifest.Id))
            && (string.Equals(manifest.Id, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ShortName(manifest.Id), name, StringComparison.OrdinalIgnoreCase)))];
        return matches.Count == 1 ? matches[0] : matches.FirstOrDefault(m => string.Equals(m.Id, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets an app id without its namespace: the text after its last dot.</summary>
    public static string ShortName(string appId) =>
        appId.LastIndexOf('.') is var dot and >= 0 ? appId[(dot + 1)..] : appId;

    /// <summary>Gets the window apps the shell can start by short name, ordered by that name (completion).</summary>
    public IReadOnlyList<AppManifest> GetApplications() =>
        [.. _catalog.Manifests.Values
            .Where(manifest => manifest.Kind == AppKind.Window && (_enablement is null || _enablement.IsEnabled(manifest.Id)))
            .OrderBy(manifest => ShortName(manifest.Id), StringComparer.OrdinalIgnoreCase)];

    /// <summary>Gets every registered terminal command manifest, ordered by command name.</summary>
    public IReadOnlyList<AppManifest> GetTerminalCommands() =>
        _catalog.Manifests.Values
            .Where(IsInstalledCommand)
            .OrderBy(m => m.Terminal!.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool IsInstalledCommand(AppManifest manifest) =>
        manifest.Kind == AppKind.Terminal
        && manifest.Terminal is not null
        && (_enablement is null || _enablement.IsEnabled(manifest.Id));
}
