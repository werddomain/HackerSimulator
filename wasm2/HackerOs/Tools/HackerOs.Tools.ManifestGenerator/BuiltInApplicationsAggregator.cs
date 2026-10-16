using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;

namespace HackerOs.Tools.ManifestGenerator;

/// <summary>One generated manifest file referenced by a host, with the host's system-app flag.</summary>
/// <param name="ManifestPath">Full path of the app project's generated manifest.</param>
/// <param name="IsSystemApp">The <c>IsSystemApp</c> metadata of the host's <c>ProjectReference</c>.</param>
/// <param name="AssemblyPath">The app's built assembly, measured for the image layer (ADR 0068); <see langword="null"/> when unknown.</param>
/// <param name="DependenciesFile">The app project's dependency list, to resolve the shared assemblies it maps.</param>
public sealed record AggregationInput(string ManifestPath, bool IsSystemApp, string? AssemblyPath = null, string? DependenciesFile = null);

/// <summary>Result of an aggregation: the document when valid, diagnostics otherwise.</summary>
/// <param name="Json">Canonical <c>BuiltInApplications.json</c>, or <see langword="null"/> on error.</param>
/// <param name="Diagnostics">Every problem found.</param>
public sealed record AggregationResult(string? Json, IReadOnlyList<GeneratorDiagnostic> Diagnostics);

/// <summary>
/// Builds a host's <c>BuiltInApplications.json</c> (ADR 0047 §3): re-reads and validates each
/// generated manifest, then builds the catalog with <see cref="AppCatalog.Build"/> so duplicate ids
/// and missing or cyclic dependencies fail the host build.
/// </summary>
public static class BuiltInApplicationsAggregator
{
    /// <summary>Aggregates <paramref name="inputs"/>.</summary>
    /// <param name="inputs">Manifest files from every referenced app project.</param>
    /// <returns>The canonical document or the diagnostics.</returns>
    public static AggregationResult Aggregate(IReadOnlyList<AggregationInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        List<GeneratorDiagnostic> diagnostics = [];
        List<BuiltInApplication> applications = [];
        foreach (AggregationInput input in inputs)
        {
            if (!File.Exists(input.ManifestPath))
            {
                diagnostics.Add(new(GeneratorCodes.UnreadableManifest, input.ManifestPath, "Manifest file not found; build the app project first."));
                continue;
            }

            if (ManifestFileReader.TryRead(input.ManifestPath, out AppManifest? manifest, out IReadOnlyList<GeneratorDiagnostic> readErrors))
            {
                applications.Add(new BuiltInApplication(input.IsSystemApp, manifest!, InspectImage(input)));
            }
            else
            {
                diagnostics.AddRange(readErrors);
            }
        }

        if (diagnostics.Count > 0)
        {
            return new AggregationResult(null, diagnostics);
        }

        AppCatalogBuildResult catalog = AppCatalog.Build(applications.Select(item => item.Manifest));
        foreach (AppCatalogError error in catalog.Errors)
        {
            diagnostics.Add(new(GeneratorCodes.InvalidCatalog, error.AppId, $"{error.Message} ({error.Code})"));
        }

        return diagnostics.Count > 0
            ? new AggregationResult(null, diagnostics)
            : new AggregationResult(BuiltInApplicationsJsonSerializer.SerializeCanonical(applications), []);
    }

    /// <summary>
    /// Measures an app's image (ADR 0068) when its assembly is known; <see langword="null"/> otherwise,
    /// and the app's memory then has no image layer.
    /// </summary>
    private static AppImage? InspectImage(AggregationInput input)
    {
        if (input.AssemblyPath is not { Length: > 0 } assembly || !File.Exists(assembly))
        {
            return null;
        }

        IEnumerable<string> dependencies = input.DependenciesFile is { Length: > 0 } list && File.Exists(list)
            ? File.ReadAllLines(list)
            : [];
        return AppImageInspector.Inspect(assembly, dependencies);
    }

    /// <summary>
    /// Parses the list file written by the <c>CollectHackerOsBuiltInApplications</c> target: one
    /// <c>&lt;isSystemApp&gt;|&lt;manifest path&gt;[|&lt;assembly path&gt;|&lt;dependency list&gt;]</c>
    /// line per manifest (an empty flag means <c>false</c>; the last two fields, for the image layer of
    /// ADR 0068, are optional). Duplicate paths (a project referenced twice) collapse to one entry,
    /// system flag OR-ed.
    /// </summary>
    /// <param name="lines">File lines.</param>
    /// <returns>The inputs, ordered by path.</returns>
    public static IReadOnlyList<AggregationInput> ParseList(IEnumerable<string> lines)
    {
        Dictionary<string, AggregationInput> byPath = new(StringComparer.Ordinal);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int separator = line.IndexOf('|', StringComparison.Ordinal);
            if (separator < 0)
            {
                throw new ArgumentException($"Invalid aggregation list line '{line}'. Expected '<isSystemApp>|<path>'.");
            }

            bool isSystem = string.Equals(line[..separator].Trim(), "true", StringComparison.OrdinalIgnoreCase);
            string[] fields = line[(separator + 1)..].Split('|');
            string path = Path.GetFullPath(fields[0].Trim());
            string? assembly = fields.Length > 1 && fields[1].Trim() is { Length: > 0 } a ? Path.GetFullPath(a) : null;
            string? dependencies = fields.Length > 2 && fields[2].Trim() is { Length: > 0 } d ? Path.GetFullPath(d) : null;
            byPath[path] = byPath.TryGetValue(path, out AggregationInput? existing)
                ? existing with
                {
                    IsSystemApp = existing.IsSystemApp || isSystem,
                    AssemblyPath = existing.AssemblyPath ?? assembly,
                    DependenciesFile = existing.DependenciesFile ?? dependencies
                }
                : new AggregationInput(path, isSystem, assembly, dependencies);
        }

        return [.. byPath.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value)];
    }
}
