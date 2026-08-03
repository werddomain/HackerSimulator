using HackerOs.Server.Contracts.Versioning;
using HackerOs.Server.Services;

namespace HackerOs.Server.Endpoints;

/// <summary>
/// Versioning endpoints — P5-SRV-001.
/// GET  /api/version         → current and supported API versions.
/// POST /api/version/check   → compatibility check for a specific client.
/// </summary>
public static class VersionEndpoints
{
    // Current server software version.
    private const string ServerVersion = "1.0.0";

    // Minimum IndexedDB schema version the server will accept.
    private const int MinCompatiblePwaSchema = 2;

    private static readonly ApiVersionResponse CurrentVersionInfo = new(
        ServerVersion,
        CurrentApiVersion: "1.0",
        SupportedVersions:
        [
            new("1.0", "current", null, MinCompatiblePwaSchema, 99)
        ],
        MinCompatiblePwaSchema);

    /// <summary>Registers version route group on the application.</summary>
    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/version").WithTags("Versioning");

        group.MapGet("/", GetVersionAsync)
            .WithName("GetApiVersion")
            .WithSummary("Returns supported API versions and PWA compatibility window.")
            .AllowAnonymous();

        group.MapPost("/check", CheckCompatibilityAsync)
            .WithName("CheckCompatibility")
            .WithSummary("Checks whether a specific client PWA schema and API version are compatible.")
            .AllowAnonymous();

        return app;
    }

    private static IResult GetVersionAsync() =>
        Results.Json(CurrentVersionInfo, VersioningContractsJsonContext.Default.ApiVersionResponse);

    private static IResult CheckCompatibilityAsync(CompatibilityCheckRequest request)
    {
        bool schemaOk = request.ClientPwaSchemaVersion >= MinCompatiblePwaSchema;
        var requestedVersion = CurrentVersionInfo.SupportedVersions
            .FirstOrDefault(v => v.Version == request.DesiredApiVersion);

        if (requestedVersion is null)
        {
            return Results.Json(
                new CompatibilityCheckResponse(false, "Requested API version is not supported.", false, false),
                VersioningContractsJsonContext.Default.CompatibilityCheckResponse);
        }

        bool sunset = requestedVersion.Status == "sunset";
        bool compatible = schemaOk && !sunset;

        string? reason = !schemaOk
            ? $"PWA schema {request.ClientPwaSchemaVersion} is below the minimum required schema {MinCompatiblePwaSchema}."
            : sunset
                ? $"API version {request.DesiredApiVersion} has been sunset."
                : null;

        return Results.Json(
            new CompatibilityCheckResponse(compatible, reason, !schemaOk, sunset),
            VersioningContractsJsonContext.Default.CompatibilityCheckResponse);
    }
}
