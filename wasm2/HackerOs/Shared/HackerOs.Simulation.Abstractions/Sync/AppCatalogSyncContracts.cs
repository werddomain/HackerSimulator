using System.Text.Json.Serialization;

namespace HackerOs.Simulation.Abstractions.Sync;

// =============================================================================
// AppCatalog Domain Sync Payload — ADR 0033
//
// Only the per-app enablement flag syncs, never the manifest itself — the manifest
// is a build artifact that differs per device's own build, not user data (ADR 0025:
// "enablement flags are device-local opinion").
// =============================================================================

/// <summary>Syncs one app's device-local enablement flag (ADR 0033).</summary>
/// <param name="AppId">Exact app ID the flag applies to.</param>
/// <param name="IsEnabled">Whether the app is enabled.</param>
public sealed record AppCatalogSyncPayload(string AppId, bool IsEnabled);

/// <summary>Source-generated JSON context for the AppCatalog sync payload.</summary>
[JsonSerializable(typeof(AppCatalogSyncPayload))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
public sealed partial class AppCatalogSyncContractsJsonContext : JsonSerializerContext { }
