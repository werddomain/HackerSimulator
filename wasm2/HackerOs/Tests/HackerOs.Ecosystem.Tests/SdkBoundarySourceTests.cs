using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HackerOs.Ecosystem.Tests;

/// <summary>
/// Source-level checks of the SDK boundary (ADR 0055 §3 and §6) that the build cannot express:
/// <list type="bullet">
/// <item>an app only calls <c>IJSRuntime</c> to <c>import</c> its own module
/// (<c>./_content/&lt;app project&gt;/…</c>); calls on the imported <c>IJSObjectReference</c> are free;</item>
/// <item>the temporary exemption list of <c>build/HackerOs.Apps.targets</c> never grows.</item>
/// </list>
/// The check is textual on purpose: it needs no compilation and runs on every <c>.razor</c> and
/// <c>.cs</c> file under <c>Apps/</c>.
/// </summary>
public sealed partial class SdkBoundarySourceTests
{
    /// <summary>
    /// Projects exempted when <c>4-sdk-boundary</c> introduced the check (ADR 0055 §6). The list may
    /// only shrink: each phase-4 chantier removes its projects, <c>4-sdk-boundary-final</c> the rest.
    /// </summary>
    private static readonly HashSet<string> InitialExemptions = new(StringComparer.Ordinal)
    {
        "HackerOs.Apps.Settings",
        "HackerOs.Apps.Diagnostic",
        "HackerOs.Apps.SystemMonitor",
        "HackerOs.Apps.FileExplorer",
        "HackerOs.Apps.TextEditor",
        "HackerOs.Apps.CodeEditor",
        "HackerOs.Apps.Browser",
        "HackerOs.Commands",
        "HackerOs.AppSdk.Terminal",
    };

    [Fact]
    public void Exemption_list_only_contains_projects_from_the_initial_list()
    {
        IReadOnlyList<XElement> exemptions = ReadExemptions();

        string[] added = [.. exemptions.Select(item => (string)item.Attribute("Include")!).Where(name => !InitialExemptions.Contains(name))];
        Assert.True(added.Length == 0,
            $"No project may be added to HackerOsSdkBoundaryExemption (ADR 0055 §6). Fix it instead: {string.Join(", ", added)}");
    }

    [Fact]
    public void Every_exemption_names_its_adr_and_chantier()
    {
        foreach (XElement item in ReadExemptions())
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)item.Attribute("Adr")), $"{item.Attribute("Include")} has no Adr.");
            Assert.False(string.IsNullOrWhiteSpace((string?)item.Attribute("Chantier")), $"{item.Attribute("Include")} has no Chantier.");
        }
    }

    [Fact]
    public void Apps_only_use_IJSRuntime_to_import_their_own_module()
    {
        string root = FindSolutionDirectory();
        HashSet<string> exempt = [.. ReadExemptions().Select(item => (string)item.Attribute("Include")!)];
        List<string> violations = [];

        foreach (string projectFile in Directory.EnumerateFiles(Path.Combine(root, "Apps"), "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(projectFile);
            if (exempt.Contains(projectName))
            {
                continue;
            }

            violations.AddRange(FindJsRuntimeViolations(Path.GetDirectoryName(projectFile)!, projectName));
        }

        Assert.True(violations.Count == 0,
            "IJSRuntime may only import the app's own module (ADR 0055 §3):" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Checker_flags_a_foreign_call_and_accepts_an_own_import()
    {
        string directory = Directory.CreateTempSubdirectory("sdk-boundary-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "Good.razor"), """
                @inject IJSRuntime JS
                @code {
                    async Task LoadAsync() => _module = await JS.InvokeAsync<IJSObjectReference>(
                        "import", "./_content/My.App/Good.razor.js");
                    async Task UseAsync() => await _module.InvokeVoidAsync("anything");
                }
                """);
            File.WriteAllText(Path.Combine(directory, "Bad.razor.cs"), """
                public partial class Bad
                {
                    [Inject] private IJSRuntime Runtime { get; set; } = default!;
                    Task CopyAsync() => Runtime.InvokeVoidAsync("navigator.clipboard.writeText", "x").AsTask();
                    Task OtherAsync() => Runtime.InvokeAsync<IJSObjectReference>("import", "./_content/Other.Library/x.js").AsTask();
                    Task DynamicAsync(string id) => Runtime.InvokeVoidAsync(id).AsTask();
                }
                """);

            List<string> violations = FindJsRuntimeViolations(directory, "My.App");

            Assert.Equal(3, violations.Count);
            Assert.All(violations, violation => Assert.Contains("Bad.razor.cs", violation, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Finds the identifiers typed <c>IJSRuntime</c> anywhere in the project (an <c>@inject</c> in a
    /// <c>.razor</c> file is often used from its <c>.razor.cs</c>), then checks every
    /// <c>Invoke*Async</c> call on them.
    /// </summary>
    private static List<string> FindJsRuntimeViolations(string projectDirectory, string projectName)
    {
        string[] files = [.. Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => !IsBuildOutput(projectDirectory, path))
            .Order(StringComparer.Ordinal)];
        Dictionary<string, string> sources = files.ToDictionary(path => path, File.ReadAllText, StringComparer.Ordinal);

        HashSet<string> runtimeNames = new(StringComparer.Ordinal);
        foreach (string source in sources.Values)
        {
            foreach (Match match in JsRuntimeDeclaration().Matches(source))
            {
                runtimeNames.Add(match.Groups["name"].Value);
            }
        }

        List<string> violations = [];
        string ownPrefix = $"./_content/{projectName}/";
        foreach ((string path, string source) in sources)
        {
            foreach (string name in runtimeNames)
            {
                Regex call = new($@"\b{Regex.Escape(name)}\s*\.\s*Invoke\w*Async\s*(<[^()]*>)?\s*\(");
                foreach (Match match in call.Matches(source))
                {
                    Match arguments = ImportArguments().Match(source, match.Index + match.Length);
                    bool ownImport = arguments.Success
                        && arguments.Index == match.Index + match.Length
                        && arguments.Groups["path"].Value.StartsWith(ownPrefix, StringComparison.Ordinal);
                    if (!ownImport)
                    {
                        int line = source.AsSpan(0, match.Index).Count('\n') + 1;
                        violations.Add($"{Path.GetRelativePath(projectDirectory, path)}:{line}: {name}.{match.Value.Trim()}… must be InvokeAsync<IJSObjectReference>(\"import\", \"{ownPrefix}…\")");
                    }
                }
            }
        }

        return violations;
    }

    private static bool IsBuildOutput(string projectDirectory, string path)
    {
        string relative = Path.GetRelativePath(projectDirectory, path).Replace('\\', '/');
        return relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal);
    }

    private static IReadOnlyList<XElement> ReadExemptions()
    {
        XDocument targets = XDocument.Load(Path.Combine(FindSolutionDirectory(), "build", "HackerOs.Apps.targets"));
        return [.. targets.Descendants().Where(element => element.Name.LocalName == "HackerOsSdkBoundaryExemption")];
    }

    private static string FindSolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HackerOs.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate HackerOs.sln.");
    }

    /// <summary><c>@inject IJSRuntime X</c>, a property, field or parameter typed <c>IJSRuntime</c>.</summary>
    [GeneratedRegex(@"(?:@inject\s+(?:Microsoft\.JSInterop\.)?IJSRuntime\s+|\b(?:Microsoft\.JSInterop\.)?IJSRuntime\??\s+)(?<name>[A-Za-z_]\w*)")]
    private static partial Regex JsRuntimeDeclaration();

    /// <summary>The literal arguments of an own-module import: <c>"import", "./_content/…"</c>.</summary>
    [GeneratedRegex(@"\G\s*""import""\s*,\s*""(?<path>[^""]*)""")]
    private static partial Regex ImportArguments();
}
