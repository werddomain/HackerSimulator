extern alias generator;

using System.Collections.Immutable;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Web.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using RouteGenerator = generator::HackerOs.Tools.WebRouteGenerator.WebRouteGenerator;

namespace HackerOs.AppSdk.Web.Tests.Generator;

/// <summary>The build-time route generator: its errors and its output (ADR 0081).</summary>
public sealed class WebRouteGeneratorTests
{
    private const string Usings = """
        using System.Collections.Generic;
        using HackerOs.App.Abstractions;
        using HackerOs.AppSdk.Web.Hosting;
        using HackerOs.AppSdk.Web.Mvc;

        """;

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
    {
        // The test host's framework assemblies, plus the SDK assemblies a site references.
        string[] platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        IEnumerable<string> sdk = new[] { typeof(WebAppDefinition), typeof(AppDefinition), typeof(IServiceProvider), typeof(ServiceCollection) }
            .Select(type => type.Assembly.Location);
        return [.. platform.Concat(sdk).Distinct().Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    });

    private static (ImmutableArray<Diagnostic> Diagnostics, Compilation Output) Run(string source)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Latest);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Site",
            [CSharpSyntaxTree.ParseText(Usings + source, parseOptions)],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new RouteGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
        return (diagnostics, output);
    }

    private static string[] Ids(string source) => [.. Run(source).Diagnostics.Select(diagnostic => diagnostic.Id).Distinct()];

    [Fact]
    public void A_valid_site_compiles_with_its_route_table_and_entry_point()
    {
        (ImmutableArray<Diagnostic> diagnostics, Compilation output) = Run("""
            [AppManifest]
            public sealed partial class Intranet : WebAppDefinition
            {
                public override string Id => "org.example.intranet";
                public override string Name => "Intranet";
                public override IReadOnlyList<string> Urls => ["http://intranet.hack/"];
            }

            public sealed class HomeController : HackController
            {
                public string Index() => "home";
            }
            """);

        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(output.GetTypeByMetadataName("HackerOs.AppSdk.Web.Generated.HackWebGeneratedRoutes"));
        Assert.NotNull(output.GetTypeByMetadataName("IntranetWebHost"));
    }

    [Fact]
    public void Two_actions_on_the_same_route_and_method_conflict()
    {
        Assert.Contains("HOSWEB0001", Ids("""
            [Route("api/notes")]
            public sealed class NotesController : HackApiController
            {
                [HttpGet("{id:int}")] public string A(int id) => "a";
                [HttpGet("{key:int}")] public string B(int key) => "b";
            }
            """));
    }

    [Fact]
    public void Same_route_with_different_methods_does_not_conflict()
    {
        Assert.Empty(Ids("""
            [Route("api/notes")]
            public sealed class NotesController : HackApiController
            {
                [HttpGet("{id:int}")] public string Get(int id) => "a";
                [HttpDelete("{id:int}")] public string Delete(int id) => "b";
            }
            """));
    }

    [Fact]
    public void An_invalid_template_is_reported()
    {
        Assert.Contains("HOSWEB0002", Ids("""
            public sealed class NotesController : HackApiController
            {
                [HttpGet("notes/{id")] public string Get(int id) => "a";
            }
            """));
    }

    [Fact]
    public void A_duplicate_route_name_is_reported()
    {
        Assert.Contains("HOSWEB0003", Ids("""
            public sealed class NotesController : HackApiController
            {
                [HttpGet("a", Name = "same")] public string A() => "a";
                [HttpGet("b", Name = "same")] public string B() => "b";
            }
            """));
    }

    [Fact]
    public void A_parameter_that_cannot_be_bound_is_reported()
    {
        Assert.Contains("HOSWEB0004", Ids("""
            public sealed class NotesController : HackController
            {
                public string Get(System.IO.Stream stream) => "a";
            }
            """));
    }

    [Fact]
    public void A_controller_without_public_constructor_is_reported()
    {
        Assert.Contains("HOSWEB0005", Ids("""
            public sealed class NotesController : HackController
            {
                private NotesController() { }
                public string Index() => "a";
            }
            """));
    }

    [Fact]
    public void A_definition_that_is_not_partial_is_reported()
    {
        Assert.Contains("HOSWEB0006", Ids("""
            [AppManifest]
            public sealed class Intranet : WebAppDefinition
            {
                public override string Id => "org.example.intranet";
                public override string Name => "Intranet";
                public override IReadOnlyList<string> Urls => ["http://intranet.hack/"];
            }
            """));
    }

    [Fact]
    public void An_api_action_without_attribute_route_is_reported()
    {
        Assert.Contains("HOSWEB0007", Ids("""
            public sealed class NotesController : HackApiController
            {
                public string List() => "a";
            }
            """));
    }
}
