using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;

namespace HackerOs.Platform.Core.Policy;

/// <summary>
/// Seeds a clean/new profile with exactly the capability grants a manifest declares.
/// </summary>
/// <remarks>
/// A clean profile grants only capabilities the app manifest explicitly requests, sourced as
/// <see cref="CapabilityGrantSource.BuildProfile"/>. Seeding never infers additional access from
/// app kind, acting authority, or related capabilities: an app that does not declare a capability
/// remains denied by default, even for a System-authority acting principal, until an explicit
/// grant exists.
/// </remarks>
public static class CleanProfileCapabilityGrantSeeder
{
    /// <summary>
    /// Grants exactly the capabilities declared by one validated manifest to one user, and no more.
    /// </summary>
    /// <param name="repository">Target capability grant repository.</param>
    /// <param name="manifest">Validated app manifest whose declared capabilities become default grants.</param>
    /// <param name="userId">Exact user ID receiving the default grants.</param>
    /// <returns>The mutation result recorded for each declared capability, in manifest order.</returns>
    public static IReadOnlyList<CapabilityGrantMutationResult> SeedDeclaredCapabilities(
        ICapabilityGrantRepository repository,
        AppManifest manifest,
        string userId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        List<CapabilityGrantMutationResult> results = [];
        foreach (string capability in manifest.Capabilities)
        {
            results.Add(repository.Grant(
                manifest.Id,
                userId,
                capability,
                CapabilityGrantSource.BuildProfile,
                AppAuthority.System));
        }

        return results;
    }
}
