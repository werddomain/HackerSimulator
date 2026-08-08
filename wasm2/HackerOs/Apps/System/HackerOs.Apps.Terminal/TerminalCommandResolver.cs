using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;

namespace HackerOs.Apps.Terminal;

/// <summary>Represents a resolved terminal command target from the AppCatalog.</summary>
public sealed record TerminalCommandResolution(
    string AppId,
    AppManifest Manifest,
    string CommandName,
    IReadOnlyList<string> Arguments);

/// <summary>
/// Resolves executable commands and static catalog aliases against registered catalog manifests (`P2-TERM-004`, `P2-TERM-004A`).
/// </summary>
public sealed class TerminalCommandResolver
{
    private readonly AppCatalog _catalog;

    /// <summary>Initializes a command resolver over the application catalog.</summary>
    public TerminalCommandResolver(AppCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Attempts to resolve a command line or command name to an installed terminal app manifest.</summary>
    public TerminalCommandResolution? Resolve(string commandName, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        foreach (AppManifest manifest in _catalog.Manifests.Values)
        {
            if (manifest.Kind != AppKind.Terminal || manifest.Terminal is null)
            {
                continue;
            }

            bool matchesName = string.Equals(manifest.Terminal.Name, commandName, StringComparison.OrdinalIgnoreCase);
            bool matchesAlias = manifest.Terminal.Aliases.Any(alias => string.Equals(alias, commandName, StringComparison.OrdinalIgnoreCase));
            bool matchesAppId = string.Equals(manifest.Id, commandName, StringComparison.OrdinalIgnoreCase);

            if (matchesName || matchesAlias || matchesAppId)
            {
                return new TerminalCommandResolution(manifest.Id, manifest, commandName, arguments);
            }
        }

        return null;
    }

    /// <summary>Gets every registered terminal command manifest, ordered by command name.</summary>
    public IReadOnlyList<AppManifest> GetTerminalCommands() =>
        _catalog.Manifests.Values
            .Where(m => m.Kind == AppKind.Terminal && m.Terminal is not null)
            .OrderBy(m => m.Terminal!.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
