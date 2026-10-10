using System.Text.Json;
using HackerOs.App.Abstractions;

namespace HackerOs.App.Abstractions.Tests.Definition;

/// <summary>Strict format of <c>BuiltInApplications.json</c> (ADR 0047 §3).</summary>
public sealed class BuiltInApplicationsJsonSerializerTests
{
    private static AppManifest Manifest(string id) => AppDefinitionMapper.ToManifest(new NamedApp(id), "1.0.0");

    [Fact]
    public void Canonical_document_sorts_by_id_and_round_trips()
    {
        string json = BuiltInApplicationsJsonSerializer.SerializeCanonical(
        [
            new BuiltInApplication(false, Manifest("org.hackeros.tests.zeta")),
            new BuiltInApplication(true, Manifest("org.hackeros.tests.alpha"))
        ]);

        Assert.StartsWith("{\"schemaVersion\":1,\"applications\":[{\"isSystemApp\":true,\"manifest\":{\"schemaVersion\":1,\"id\":\"org.hackeros.tests.alpha\"", json, StringComparison.Ordinal);
        Assert.EndsWith("}]}\n", json, StringComparison.Ordinal);

        IReadOnlyList<BuiltInApplication> read = BuiltInApplicationsJsonSerializer.DeserializeStrict(json);
        Assert.Equal(["org.hackeros.tests.alpha", "org.hackeros.tests.zeta"], read.Select(item => item.Manifest.Id));
        Assert.Equal([true, false], read.Select(item => item.IsSystemApp));
        Assert.Equal(
            AppManifestJsonSerializer.SerializeCanonical(Manifest("org.hackeros.tests.alpha")),
            AppManifestJsonSerializer.SerializeCanonical(read[0].Manifest));
        Assert.Equal(json, BuiltInApplicationsJsonSerializer.SerializeCanonical(read));
    }

    [Fact]
    public void Empty_document_is_valid() =>
        Assert.Empty(BuiltInApplicationsJsonSerializer.DeserializeStrict(BuiltInApplicationsJsonSerializer.SerializeCanonical([])));

    [Fact]
    public void Omitted_manifest_collections_are_normalized()
    {
        const string json = """{"schemaVersion":1,"applications":[{"isSystemApp":false,"manifest":{"schemaVersion":1,"id":"org.hackeros.tests.a","name":"a","version":"1.0.0","publisherId":"pub.local","description":"a","kind":"window","entryPoint":{"assembly":"A.dll","type":"A.W"},"sdkCompatibility":{"minimumVersion":"1.0.0"},"presentation":{"category":"utilities","launchVisibility":"visible","iconAssetPaths":[]},"resources":{"baselineCpuWeight":0,"burstCpuWeight":0,"baselineMemoryWeight":0,"burstMemoryWeight":0,"baselineStorageIoWeight":0,"burstStorageIoWeight":0,"baselineNetworkIoWeight":0,"burstNetworkIoWeight":0}}}]}""";

        AppManifest manifest = Assert.Single(BuiltInApplicationsJsonSerializer.DeserializeStrict(json)).Manifest;

        Assert.NotNull(manifest.Capabilities);
        Assert.NotNull(manifest.FileHandlers);
        Assert.NotNull(manifest.DeclaredTopicPermissions);
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"applications":[]}""")]
    [InlineData("""{"applications":[]}""")]
    [InlineData("""{"schemaVersion":1}""")]
    [InlineData("""{"schemaVersion":1,"applications":[],"extra":1}""")]
    [InlineData("""{"schemaVersion":1,"schemaVersion":1,"applications":[]}""")]
    [InlineData("""{"schemaVersion":1,"applications":[{"isSystemApp":"yes","manifest":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"applications":[{"isSystemApp":true}]}""")]
    [InlineData("""{"schemaVersion":1,"applications":[{"isSystemApp":true,"manifest":{},"generated":true}]}""")]
    public void Malformed_documents_are_rejected(string json) =>
        Assert.ThrowsAny<JsonException>(() => BuiltInApplicationsJsonSerializer.DeserializeStrict(json));

    private sealed class FakeWindow;

    private sealed class NamedApp(string id) : AppDefinition
    {
        public override string Id => id;
        public override string Name => id;
        public override AppKind Kind => AppKind.Window;
        public override Type EntryPoint => typeof(FakeWindow);
    }
}
