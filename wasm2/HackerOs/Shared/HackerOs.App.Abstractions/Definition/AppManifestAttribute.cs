namespace HackerOs.App.Abstractions;

/// <summary>
/// Marks the single <see cref="AppDefinition"/> subclass that describes one application, per
/// <c>docs/adr/0047-app-definition-and-generated-manifests.md</c>. The build-time manifest generator
/// (<c>Tools/HackerOs.Tools.ManifestGenerator</c>) discovers every marked class in a compiled app
/// assembly, instantiates it and writes the canonical <c>manifest.json</c> from it. The class is never
/// instantiated at runtime: hosts only read the generated JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AppManifestAttribute : Attribute;
