using System.Reflection;
using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestGenerator.Tests;

/// <summary>One test per HOSAPP rule of ADR 0047 §1, run in-process on fixture types.</summary>
public sealed class AppDefinitionInspectorTests
{
    private static readonly Assembly Fixtures = typeof(ValidCommandApp).Assembly;

    private static InspectionResult Inspect(params Type[] types) =>
        AppDefinitionInspector.Inspect(Fixtures, types, "1.0.0");

    private static void AssertSingleCode(InspectionResult result, string code, Type subject)
    {
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Manifests);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Subject == subject.FullName);
    }

    [Fact]
    public void Valid_definition_produces_a_validated_canonical_manifest()
    {
        InspectionResult result = Inspect(typeof(ValidCommandApp));

        Assert.True(result.IsSuccess);
        GeneratedManifest generated = Assert.Single(result.Manifests);
        Assert.Equal("org.hackeros.tests.valid", generated.Manifest.Id);
        Assert.Equal(typeof(ValidCommandApp).FullName, generated.DefinitionType);
        Assert.Equal(AppManifestJsonSerializer.SerializeCanonical(generated.Manifest), generated.CanonicalJson);
        Assert.Equal(new AppEntryPointManifest("HackerOs.Tools.ManifestGenerator.Tests.dll", typeof(FixtureCommand).FullName!), generated.Manifest.EntryPoint);
    }

    [Fact]
    public void Two_definitions_produce_two_manifests_sorted_by_id()
    {
        InspectionResult result = Inspect(typeof(ValidCommandApp), typeof(SecondCommandApp));

        Assert.True(result.IsSuccess);
        Assert.Equal(["org.hackeros.tests.second", "org.hackeros.tests.valid"], result.Manifests.Select(item => item.Manifest.Id));
    }

    [Fact]
    public void HOSAPP0001_no_definition() =>
        Assert.Equal(GeneratorCodes.NoDefinition, Assert.Single(Inspect().Diagnostics).Code);

    [Fact]
    public void HOSAPP0002_not_public() => AssertSingleCode(Inspect(typeof(InternalApp)), GeneratorCodes.NotPublic, typeof(InternalApp));

    [Fact]
    public void HOSAPP0003_abstract() => AssertSingleCode(Inspect(typeof(AbstractApp)), GeneratorCodes.Abstract, typeof(AbstractApp));

    [Fact]
    public void HOSAPP0004_not_an_app_definition() =>
        AssertSingleCode(Inspect(typeof(NotADefinition)), GeneratorCodes.NotAnAppDefinition, typeof(NotADefinition));

    [Fact]
    public void HOSAPP0005_no_parameterless_constructor() =>
        AssertSingleCode(Inspect(typeof(NoDefaultConstructorApp)), GeneratorCodes.NoParameterlessConstructor, typeof(NoDefaultConstructorApp));

    [Fact]
    public void HOSAPP0006_getter_throws() => AssertSingleCode(Inspect(typeof(ThrowingApp)), GeneratorCodes.DefinitionThrew, typeof(ThrowingApp));

    [Fact]
    public void HOSAPP0007_entry_point_in_another_assembly() =>
        AssertSingleCode(Inspect(typeof(ForeignEntryPointApp)), GeneratorCodes.EntryPointAssembly, typeof(ForeignEntryPointApp));

    [Fact]
    public void HOSAPP0008_non_deterministic_getter() => AssertSingleCode(Inspect(typeof(ClockApp)), GeneratorCodes.NonDeterministic, typeof(ClockApp));

    [Fact]
    public void HOSAPP0009_validator_rejects_manifest() => AssertSingleCode(Inspect(typeof(InvalidIdApp)), GeneratorCodes.InvalidManifest, typeof(InvalidIdApp));

    [Fact]
    public void HOSAPP0010_entry_point_breaks_discovery_rules() =>
        AssertSingleCode(Inspect(typeof(WrongKindApp)), GeneratorCodes.EntryPointKind, typeof(WrongKindApp));

    [Fact]
    public void HOSAPP0011_duplicate_id_in_one_assembly()
    {
        InspectionResult result = Inspect(typeof(ValidCommandApp), typeof(DuplicateIdApp));

        GeneratorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorCodes.DuplicateIdInAssembly, diagnostic.Code);
        Assert.Empty(result.Manifests);
    }

    [Fact]
    public void Any_error_suppresses_every_manifest()
    {
        InspectionResult result = Inspect(typeof(ValidCommandApp), typeof(ThrowingApp));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Manifests);
    }

    [Fact]
    public void Assembly_scan_finds_classes_marked_with_the_attribute_only()
    {
        InspectionResult result = AppDefinitionInspector.Inspect(Fixtures);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.Equal("org.hackeros.tests.marked", Assert.Single(result.Manifests).Manifest.Id);
    }

    [Fact]
    public void Sdk_minimum_version_comes_from_the_compiled_reference()
    {
        Version referenced = Fixtures.GetReferencedAssemblies().Single(name => name.Name == "HackerOs.App.Abstractions").Version!;

        Assert.Equal($"{referenced.Major}.{referenced.Minor}.0", AppDefinitionInspector.SdkMinimumVersionOf(Fixtures));
    }

    [Fact]
    public void Diagnostic_uses_msbuild_canonical_error_format() =>
        Assert.Equal(
            "/src/App.csproj : error HOSAPP0005: [Ns.App] message",
            new GeneratorDiagnostic("HOSAPP0005", "Ns.App", "message").ToMsBuildLine("/src/App.csproj"));
}
