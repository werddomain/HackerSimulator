using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core;
using HackerOs.Tests.Support;

namespace HackerOs.SdkCompatibility.Tests;

/// <summary>
/// ADR 0098 §8, ADR 0114, ADR 0132 §1: an app compiled against an earlier SDK (the <c>1.0.0</c> and
/// <c>1.1.0</c> packages kept in <c>Tests/fixtures/sdk-compat</c>) is admitted by the current catalog, binds to the current SDK
/// assemblies and runs. Its Chromium counterpart, which boots a host with the package, is in
/// <c>HackerOs.Templates.E2E.Tests</c>.
/// </summary>
public sealed class SdkCompatibilityTests
{
    /// <summary>The fixture packages, one per earlier SDK version that has one (1.0.0, 1.1.0).</summary>
    public static TheoryData<string> Packages()
    {
        TheoryData<string> data = [];
        foreach (string path in FixturePackage.All())
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Fact]
    public void The_SDK_1_0_0_fixture_is_present() =>
        Assert.Contains("Contoso.Sdk1Compat.1.0.0.nupkg", FixturePackage.All().Select(Path.GetFileName));

    [Fact]
    public void The_SDK_1_1_0_fixture_is_present_and_needs_SDK_1_1()
    {
        FixturePackage fixture = FixturePackage.Open("Contoso.Sdk11Compat.1.1.0.nupkg");
        Assert.All(fixture.Manifests, manifest => Assert.Equal("1.1.0", manifest.SdkCompatibility!.MinimumVersion));
        Assert.False(fixture.Manifests.Single(item => item.Id == "com.contoso.sdk11-window").Window!.RememberGeometry);
    }

    [Theory]
    [MemberData(nameof(Packages))]
    public void The_current_catalog_admits_its_apps(string package)
    {
        FixturePackage fixture = FixturePackage.Open(package);
        Assert.NotEmpty(fixture.Manifests);

        AppCatalogBuildResult result = AppCatalog.Build(fixture.Manifests);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.ToString())));
        Assert.Empty(result.Catalog!.Rejected);
        Assert.All(fixture.Manifests, manifest => Assert.True(
            HackerOsSdk.IsCompatible(manifest.SdkCompatibility!),
            $"{manifest.Id} needs SDK {manifest.SdkCompatibility!.MinimumVersion}, the OS has {HackerOsSdk.Version}."));
    }

    /// <summary>
    /// Every HackerOS type and member the app's metadata references still exists in the current SDK
    /// (a removed or renamed API is reported by name).
    /// </summary>
    [Theory]
    [MemberData(nameof(Packages))]
    public void Every_SDK_type_and_member_it_references_still_exists(string package)
    {
        FixturePackage fixture = FixturePackage.Open(package);
        using PEReader pe = new(new MemoryStream(fixture.Image));
        MetadataReader metadata = pe.GetMetadataReader();
        List<string> missing = [];
        int checkedReferences = 0;

        foreach (MemberReferenceHandle handle in metadata.MemberReferences)
        {
            MemberReference member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference
                || ResolveHackerOsType(metadata, (TypeReferenceHandle)member.Parent, missing) is not { } type)
            {
                continue;
            }

            string name = metadata.GetString(member.Name);
            checkedReferences++;
            if (type.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length == 0)
            {
                missing.Add($"{type.FullName}.{name}");
            }
        }

        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            ResolveHackerOsType(metadata, handle, missing);
        }

        Assert.True(checkedReferences > 0, "The fixture references no HackerOS member: it tests nothing.");
        Assert.Empty(missing.Distinct());
    }

    /// <summary>
    /// Every method of the app compiles against the current SDK: the JIT resolves each call, field
    /// and type the method body names, and throws <see cref="MissingMethodException"/>,
    /// <see cref="MissingFieldException"/> or <see cref="TypeLoadException"/> on a binary break
    /// (a changed signature, a type moved to another assembly).
    /// </summary>
    [Theory]
    [MemberData(nameof(Packages))]
    public void Every_method_binds_to_the_current_SDK(string package)
    {
        FixturePackage fixture = FixturePackage.Open(package);
        int compiled = 0;
        foreach (Type type in fixture.Assembly.GetTypes().Where(type => !type.ContainsGenericParameters))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            IEnumerable<MethodBase> methods = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
            foreach (MethodBase method in methods.Where(method => !method.ContainsGenericParameters && method.GetMethodBody() is not null))
            {
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
                compiled++;
            }
        }

        Assert.True(compiled > 0);
    }

    /// <summary>The command, started the way the OS starts a terminal app, writes a file and prints it.</summary>
    [Fact]
    public async Task The_SDK_1_0_0_command_runs_on_the_current_SDK()
    {
        (int exitCode, string stdout, string stderr, FakeAppFileSystemGateway fileSystem) =
            await RunCommandAsync("Contoso.Sdk1Compat.1.0.0.nupkg", "com.contoso.sdk1-command", ["still", "binds"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("still binds", fileSystem.ContentOf("/home/user/sdk1-compat.txt"));
        Assert.Equal($"sdk1-compat: still binds (compiled against 1.0.0, running on {HackerOsSdk.Version})", stdout);
    }

    /// <summary>
    /// The 1.1.0 command also calls what SDK 1.1 added (<c>HackUserAccounts.ValidateNewPassword</c>,
    /// ADR 0120): the rule still answers the same way on the current SDK.
    /// </summary>
    [Fact]
    public async Task The_SDK_1_1_0_command_runs_on_the_current_SDK()
    {
        (int exitCode, string stdout, string stderr, FakeAppFileSystemGateway fileSystem) =
            await RunCommandAsync("Contoso.Sdk11Compat.1.1.0.nupkg", "com.contoso.sdk11-command", ["still", "binds"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("still binds", fileSystem.ContentOf("/home/user/sdk11-compat.txt"));
        Assert.Equal(
            $"sdk11-compat: still binds [valid password] (compiled against 1.1.0, running on {HackerOsSdk.Version})",
            stdout);
    }

    /// <summary>Starts a fixture command the way the OS starts a terminal app and captures its output.</summary>
    private static async Task<(int ExitCode, string Stdout, string Stderr, FakeAppFileSystemGateway FileSystem)> RunCommandAsync(
        string package, string appId, IReadOnlyList<string> arguments)
    {
        FixturePackage fixture = FixturePackage.Open(package);
        AppManifest manifest = fixture.Manifests.Single(item => item.Id == appId);
        Type entryPoint = fixture.Assembly.GetType(manifest.EntryPoint!.Type, throwOnError: true)!;
        TerminalAppBase command = (TerminalAppBase)Activator.CreateInstance(entryPoint, manifest)!;

        FakeAppFileSystemGateway fileSystem = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        MinimalAppExecutionContext app = new(manifest, fileSystem);
        StringWriter stdout = new();
        StringWriter stderr = new();
        TerminalExecutionContext context = new(
            app, [.. arguments], TextReader.Null, stdout, stderr, "/home/user",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = "/home/user", ["USER"] = "user" });

        int exitCode;
        using (HackExecutionContext.Enter(app, "/home/user"))
        {
            exitCode = await command.ExecuteAsync(context, CancellationToken.None);
        }

        return (exitCode, stdout.ToString().TrimEnd(), stderr.ToString(), fileSystem);
    }

    /// <summary>
    /// Resolves a type reference into a HackerOS assembly (the SDK), or <see langword="null"/> for a
    /// reference into the framework or a third-party package. A HackerOS type that no longer exists is
    /// added to <paramref name="missing"/>.
    /// </summary>
    private static Type? ResolveHackerOsType(MetadataReader metadata, TypeReferenceHandle handle, List<string> missing)
    {
        TypeReference reference = metadata.GetTypeReference(handle);
        string name = metadata.GetString(reference.Name);
        string ns = metadata.GetString(reference.Namespace);
        string? assemblyName;
        switch (reference.ResolutionScope.Kind)
        {
            case HandleKind.AssemblyReference:
                assemblyName = metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name);
                break;
            case HandleKind.TypeReference:
                // A nested type: resolve its declaring type first.
                Type? declaring = ResolveHackerOsType(metadata, (TypeReferenceHandle)reference.ResolutionScope, missing);
                if (declaring is null)
                {
                    return null;
                }

                Type? nested = declaring.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
                if (nested is null)
                {
                    missing.Add($"{declaring.FullName}+{name}");
                }

                return nested;
            default:
                return null;
        }

        if (!assemblyName.StartsWith("HackerOs.", StringComparison.Ordinal))
        {
            return null;
        }

        string fullName = ns.Length == 0 ? name : $"{ns}.{name}";
        Type? type = Assembly.Load(assemblyName).GetType(fullName, throwOnError: false);
        if (type is null)
        {
            missing.Add($"{fullName}, {assemblyName}");
        }

        return type;
    }
}
