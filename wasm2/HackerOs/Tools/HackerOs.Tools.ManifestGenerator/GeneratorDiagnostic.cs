namespace HackerOs.Tools.ManifestGenerator;

/// <summary>
/// One build-breaking manifest problem with a stable <c>HOSAPPnnnn</c> code (ADR 0047 §2). The
/// <see cref="Subject"/> names the offending <c>[AppManifest]</c> class (full type name) or file.
/// </summary>
/// <param name="Code">Stable code, <c>HOSAPP0001</c> to <c>HOSAPP0099</c>.</param>
/// <param name="Subject">Full name of the class, or path of the file, the problem is about.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record GeneratorDiagnostic(string Code, string Subject, string Message)
{
    /// <summary>
    /// Formats the diagnostic in MSBuild's canonical error format so the calling <c>Exec</c> task
    /// reports it as a real build error attributed to <paramref name="origin"/>.
    /// </summary>
    /// <param name="origin">Project or file path shown as the error origin.</param>
    /// <returns>One line, e.g. <c>/x/App.csproj : error HOSAPP0005: [Ns.MyApp] ...</c>.</returns>
    public string ToMsBuildLine(string origin) => $"{origin} : error {Code}: [{Subject}] {Message}";
}

/// <summary>Catalog of the stable diagnostic codes emitted by the generator.</summary>
public static class GeneratorCodes
{
    /// <summary>The app project contains no <c>[AppManifest]</c> class.</summary>
    public const string NoDefinition = "HOSAPP0001";

    /// <summary>The <c>[AppManifest]</c> class is not public.</summary>
    public const string NotPublic = "HOSAPP0002";

    /// <summary>The <c>[AppManifest]</c> class is abstract.</summary>
    public const string Abstract = "HOSAPP0003";

    /// <summary>The <c>[AppManifest]</c> class does not derive from <c>AppDefinition</c>.</summary>
    public const string NotAnAppDefinition = "HOSAPP0004";

    /// <summary>The <c>[AppManifest]</c> class has no public parameterless constructor.</summary>
    public const string NoParameterlessConstructor = "HOSAPP0005";

    /// <summary>The constructor or a getter threw while the manifest was being produced.</summary>
    public const string DefinitionThrew = "HOSAPP0006";

    /// <summary>An entry-point type is missing or lives in another assembly than the definition.</summary>
    public const string EntryPointAssembly = "HOSAPP0007";

    /// <summary>Two instantiations of the definition produced different manifests.</summary>
    public const string NonDeterministic = "HOSAPP0008";

    /// <summary><c>AppManifestValidator</c> rejected the produced manifest.</summary>
    public const string InvalidManifest = "HOSAPP0009";

    /// <summary>The entry-point type breaks the <c>AppEntryPointDiscovery</c> rules for its kind.</summary>
    public const string EntryPointKind = "HOSAPP0010";

    /// <summary>Two <c>[AppManifest]</c> classes of one assembly declare the same app id.</summary>
    public const string DuplicateIdInAssembly = "HOSAPP0011";

    /// <summary>A manifest file read back during aggregation is missing or not strict schema-v1 JSON.</summary>
    public const string UnreadableManifest = "HOSAPP0020";

    /// <summary>The aggregated catalog is invalid (duplicate id, missing or cyclic dependency...).</summary>
    public const string InvalidCatalog = "HOSAPP0021";

    /// <summary>The tool was called with invalid arguments.</summary>
    public const string InvalidArguments = "HOSAPP0090";

    /// <summary>The compiled app assembly could not be loaded or its types enumerated.</summary>
    public const string AssemblyLoadFailed = "HOSAPP0091";
}
