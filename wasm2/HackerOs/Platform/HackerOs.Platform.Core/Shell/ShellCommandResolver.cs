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
