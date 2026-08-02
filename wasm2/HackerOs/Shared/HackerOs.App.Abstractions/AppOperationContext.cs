namespace HackerOs.App.Abstractions;

/// <summary>
/// Captures the trusted identity and grants applied to one platform operation.
/// </summary>
/// <remarks>
/// Platform code creates this context. App-facing gateways are pre-scoped and do
/// not accept caller-created contexts as proof of authorization.
/// </remarks>
public sealed record AppOperationContext
{
    /// <summary>Gets the application on whose behalf the operation runs.</summary>
    public required string AppId { get; init; }

    /// <summary>Gets the authenticated acting user.</summary>
    public required string UserId { get; init; }

    /// <summary>Gets the acting user's authority.</summary>
    public required AppAuthority UserAuthority { get; init; }

    /// <summary>Gets exact capabilities granted by trusted OS policy.</summary>
    public required IReadOnlySet<string> GrantedCapabilities { get; init; }

    /// <summary>
    /// Gets whether this is an explicit, audited OS-owned operation rather than user-driven app UI.
    /// </summary>
    public bool IsSystemOperation { get; init; }

    /// <summary>Gets the authority used for this operation.</summary>
    public AppAuthority EffectiveAuthority =>
        IsSystemOperation ? AppAuthority.System : UserAuthority;

    /// <summary>Determines whether an exact capability was granted.</summary>
    /// <param name="capability">Capability required by the operation.</param>
    /// <returns><see langword="true"/> when trusted policy granted it exactly.</returns>
    public bool HasCapability(string capability) =>
        AppCapabilityPolicy.IsGranted(GrantedCapabilities, capability);
}