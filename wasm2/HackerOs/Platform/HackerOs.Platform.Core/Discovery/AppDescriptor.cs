using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using HackerOs.App.Abstractions;

namespace HackerOs.Platform.Core.Discovery;

/// <summary>
/// Pairs a validated manifest with its resolved, concrete entry-point type and owning assembly,
/// per `P1-APP-001`. Constructing a descriptor never instantiates app code.
/// </summary>
/// <param name="Manifest">Validated manifest already accepted into an <see cref="AppCatalog"/>.</param>
/// <param name="EntryPointType">Concrete, non-instantiated entry-point type declared by the manifest.</param>
/// <param name="Assembly">Assembly that owns <paramref name="EntryPointType"/>.</param>
public sealed record AppDescriptor(
    AppManifest Manifest,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type EntryPointType,
    Assembly Assembly);

/// <summary>Describes one deterministic entry-point resolution failure.</summary>
/// <param name="Code">Stable machine-readable error code.</param>
/// <param name="AppId">App whose entry point failed to resolve.</param>
/// <param name="Message">Human-readable diagnostic.</param>
public sealed record AppDiscoveryError(string Code, string AppId, string Message);

/// <summary>Contains either every resolved descriptor or every deterministic discovery error.</summary>
/// <param name="Descriptors">Resolved descriptors keyed by app ID, or <see langword="null"/> on failure.</param>
/// <param name="Errors">Every discovery error found, in deterministic order.</param>
public sealed record AppDiscoveryResult(
    IReadOnlyDictionary<string, AppDescriptor>? Descriptors,
    IReadOnlyList<AppDiscoveryError> Errors)
{
    /// <summary>Gets whether every manifest in the catalog resolved to a valid entry point.</summary>
    public bool IsSuccess => Descriptors is not null && Errors.Count == 0;
}
