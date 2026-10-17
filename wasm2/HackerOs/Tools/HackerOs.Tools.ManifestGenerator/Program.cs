using System.Reflection;
using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestGenerator;

/// <summary>
/// Entry point of the HackerOS manifest generator (ADR 0047). Commands:
/// <list type="bullet">
/// <item><c>generate</c>: run by the <c>GenerateHackerOsAppManifests</c> MSBuild target after an app
/// project compiles; writes its <c>manifest.json</c>.</item>
/// <item><c>aggregate</c>: run by the <c>CollectHackerOsBuiltInApplications</c> MSBuild target of a
/// host; writes its <c>BuiltInApplications.json</c>.</item>
/// <item><c>validate &lt;file&gt;</c>: strict schema-v1 read and <c>AppManifestValidator</c> pass on
/// one manifest file (absorbs the former <c>HackerOs.Tools.ManifestValidator</c>).</item>
/// </list>
/// Errors are printed in MSBuild's canonical format and the exit code is non-zero.
/// </summary>
public static class Program
{
    /// <summary>Runs one command.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>0 on success, 1 on any error.</returns>
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            return args[0] switch
            {
                "generate" => Generate(ParseOptions(args.AsSpan(1))),
                "aggregate" => Aggregate(ParseOptions(args.AsSpan(1))),
                "validate" when args.Length == 2 => Validate(args[1]),
                _ => Fail("ManifestGenerator", new GeneratorDiagnostic(GeneratorCodes.InvalidArguments, "arguments", $"Unknown command line: {string.Join(' ', args)}"))
            };
        }
        catch (ArgumentException exception)
        {
            return Fail("ManifestGenerator", new GeneratorDiagnostic(GeneratorCodes.InvalidArguments, "arguments", exception.Message));
        }
    }

    /// <summary>
    /// <c>generate --assembly &lt;dll&gt; --project &lt;csproj&gt; --dependencies &lt;list file&gt; --stamp &lt;file&gt;</c>.
    /// The dependency list file holds one path per line, in resolution priority order. An SDK
    /// boundary violation (<c>HOSAPP0031</c>, ADR 0055 §3) always fails: there is no exemption.
    /// </summary>
    private static int Generate(IReadOnlyDictionary<string, string> options)
    {
        string assemblyPath = Path.GetFullPath(Required(options, "assembly"));
        string project = Path.GetFullPath(Required(options, "project"));
        string stamp = Path.GetFullPath(Required(options, "stamp"));
        string[] dependencies = options.TryGetValue("dependencies", out string? listFile) && File.Exists(listFile)
            ? File.ReadAllLines(listFile).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToArray()
            : [];

        // The app's own output directory first, so sibling project references copied there win.
        string outputDirectory = Path.GetDirectoryName(assemblyPath)!;
        string[] candidates = [.. dependencies
            .Concat(Directory.Exists(outputDirectory) ? Directory.EnumerateFiles(outputDirectory, "*.dll") : [])];

        AppAssemblyLoadContext context = new(candidates);
        try
        {
            Assembly assembly;
            try
            {
                assembly = context.LoadFromAssemblyPath(assemblyPath);
            }
            catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                return Fail(project, new GeneratorDiagnostic(GeneratorCodes.AssemblyLoadFailed, assemblyPath, exception.Message));
            }

            // ADR 0098 §5: declared asset paths are relative to the project's wwwroot/ (served as _content/<assembly>/).
            InspectionResult result = AppDefinitionInspector.Inspect(assembly, candidates, Path.Combine(Path.GetDirectoryName(project)!, "wwwroot"));
            if (!result.IsSuccess)
            {
                return Fail(project, [.. result.Diagnostics]);
            }

            // SDK boundary (ADR 0055 §3): checked before anything is written, so a failing build never
            // leaves a fresh stamp that would make the next incremental build skip the check.
            IReadOnlyList<GeneratorDiagnostic> boundary = SdkBoundaryInspector.Inspect(
                assembly.GetTypes(),
                result.Manifests.SelectMany(manifest => manifest.EntryPointTypes));
            if (boundary.Count > 0)
            {
                return Fail(project, [.. boundary]);
            }

            IReadOnlyList<string> files = ManifestWriter.Write(Path.GetDirectoryName(project)!, result.Manifests);
            ManifestWriter.WriteIfChanged(stamp, string.Join('\n', files) + "\n");
            foreach (GeneratedManifest manifest in result.Manifests)
            {
                Console.WriteLine($"HackerOS manifest: {manifest.Manifest.Id} <- {manifest.DefinitionType}");
            }

            return 0;
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    /// <c>aggregate --project &lt;host csproj&gt; --apps &lt;list file&gt; --output &lt;json&gt;</c>. The output is
    /// rewritten only when its bytes change, so the host does not recompile needlessly.
    /// </summary>
    private static int Aggregate(IReadOnlyDictionary<string, string> options)
    {
        string project = Path.GetFullPath(Required(options, "project"));
        string output = Path.GetFullPath(Required(options, "output"));
        string listFile = Required(options, "apps");
        IReadOnlyList<AggregationInput> inputs = File.Exists(listFile)
            ? BuiltInApplicationsAggregator.ParseList(File.ReadAllLines(listFile))
            : [];

        AggregationResult result = BuiltInApplicationsAggregator.Aggregate(inputs);
        if (result.Json is null)
        {
            return Fail(project, [.. result.Diagnostics]);
        }

        ManifestWriter.WriteIfChanged(output, result.Json);
        Console.WriteLine($"HackerOS built-in applications: {inputs.Count} app(s) -> {output}");
        return 0;
    }

    private static int Validate(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return Fail(manifestPath, new GeneratorDiagnostic(GeneratorCodes.UnreadableManifest, manifestPath, "File not found."));
        }

        if (!ManifestFileReader.TryRead(manifestPath, out AppManifest? manifest, out IReadOnlyList<GeneratorDiagnostic> diagnostics))
        {
            return Fail(manifestPath, [.. diagnostics]);
        }

        Console.WriteLine($"[OK] Manifest '{manifest!.Id}' v{manifest.Version} is valid.");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(ReadOnlySpan<string> args)
    {
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Expected '--name value' pairs, got '{key}'.");
            }

            options[key[2..]] = args[++index];
        }

        return options;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required option --{name}.");

    private static int Fail(string origin, params GeneratorDiagnostic[] diagnostics)
    {
        foreach (GeneratorDiagnostic diagnostic in diagnostics)
        {
            Console.Error.WriteLine(diagnostic.ToMsBuildLine(origin));
        }

        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("HackerOS manifest generator (ADR 0047)");
        Console.WriteLine("  generate --assembly <app.dll> --project <app.csproj> --stamp <file> [--dependencies <list file>]");
        Console.WriteLine("  aggregate --project <host.csproj> --apps <list file> --output <BuiltInApplications.json>");
        Console.WriteLine("  validate <manifest.json>");
    }
}
