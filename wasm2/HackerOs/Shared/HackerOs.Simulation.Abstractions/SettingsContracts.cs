using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Simulation.Abstractions;

/// <summary>
/// Defines and protects one canonical settings document.
/// </summary>
/// <param name="Path">Virtual filesystem path exposing the document.</param>
/// <param name="Key">Structured repository identity and ownership partition.</param>
/// <param name="InitialContent">Initial serialized content for a clean OS profile.</param>
/// <param name="MediaType">Media type returned by file and settings APIs.</param>
/// <param name="ReadCapability">Exact capability required to read the document.</param>
/// <param name="WriteCapability">Exact capability required to write the document.</param>
/// <param name="MinimumReadAuthority">Minimum acting authority required to read.</param>
/// <param name="MinimumWriteAuthority">Minimum acting authority required to write.</param>
/// <param name="Validator">Whole-document syntax and schema validator.</param>
public sealed record SettingsDocumentDefinition(
    VirtualPath Path,
    SettingsDocumentKey Key,
    string InitialContent,
    string MediaType,
    string ReadCapability,
    string WriteCapability,
    AppAuthority MinimumReadAuthority,
    AppAuthority MinimumWriteAuthority,
    ISettingsDocumentValidator Validator);

/// <summary>
/// Represents one immutable revision of a settings document.
/// </summary>
/// <param name="Path">Virtual filesystem path of the document.</param>
/// <param name="Content">Canonical serialized content.</param>
/// <param name="MediaType">Document media type.</param>
/// <param name="Revision">Monotonically increasing revision used for conflict detection.</param>
public sealed record SettingsDocumentSnapshot(
    VirtualPath Path,
    string Content,
    string MediaType,
    long Revision);

/// <summary>
/// Describes why a settings document could not be read.
/// </summary>
public enum SettingsReadStatus
{
    /// <summary>The document was read successfully.</summary>
    Success,

    /// <summary>No settings document is registered at the requested path.</summary>
    NotFound,

    /// <summary>The acting app/user lacks capability or authority.</summary>
    Denied
}

/// <summary>
/// Contains the result of reading a settings document.
/// </summary>
/// <param name="Status">Read outcome.</param>
/// <param name="Document">Document snapshot when successful.</param>
/// <param name="ErrorCode">Stable error code when unsuccessful.</param>
public sealed record SettingsReadResult(
    SettingsReadStatus Status,
    SettingsDocumentSnapshot? Document = null,
    string? ErrorCode = null);

/// <summary>
/// Requests atomic replacement of a canonical settings document.
/// </summary>
/// <param name="Path">Virtual settings path to replace.</param>
/// <param name="Content">Complete candidate document.</param>
/// <param name="ExpectedRevision">Revision read by the editor before making changes.</param>
public sealed record SettingsWriteRequest(
    VirtualPath Path,
    string Content,
    long ExpectedRevision);

/// <summary>
/// Describes the outcome of writing a settings document.
/// </summary>
public enum SettingsWriteStatus
{
    /// <summary>The complete validated document was committed atomically.</summary>
    Success,

    /// <summary>No settings document is registered at the requested path.</summary>
    NotFound,

    /// <summary>The acting app/user lacks capability or authority.</summary>
    Denied,

    /// <summary>The candidate failed syntax or schema validation.</summary>
    Invalid,

    /// <summary>The expected revision is stale.</summary>
    Conflict
}

/// <summary>
/// Contains the result of an atomic settings write.
/// </summary>
/// <param name="Status">Write outcome.</param>
/// <param name="Document">Committed document snapshot when successful.</param>
/// <param name="Errors">Validation or authorization errors.</param>
public sealed record SettingsWriteResult(
    SettingsWriteStatus Status,
    SettingsDocumentSnapshot? Document = null,
    IReadOnlyList<string>? Errors = null);

/// <summary>
/// Validates a complete candidate settings document before commit.
/// </summary>
public interface ISettingsDocumentValidator
{
    /// <summary>Returns every syntax or schema failure in deterministic order.</summary>
    /// <param name="content">Complete serialized candidate document.</param>
    /// <returns>An empty list when the document is valid.</returns>
    IReadOnlyList<string> Validate(string content);
}

/// <summary>
/// Provides authorized access to canonical settings documents.
/// </summary>
public interface ISettingsDocumentService
{
    /// <summary>Reads the current revision after capability and authority checks.</summary>
    /// <param name="path">Virtual settings path.</param>
    /// <param name="context">Trusted operation identity and capability grants.</param>
    /// <param name="cancellationToken">Signals operation cancellation.</param>
    /// <returns>The authorized read result.</returns>
    ValueTask<SettingsReadResult> ReadAsync(
        VirtualPath path,
        AppOperationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically validates and replaces one settings document.</summary>
    /// <param name="request">Complete candidate and expected revision.</param>
    /// <param name="context">Trusted operation identity and capability grants.</param>
    /// <param name="cancellationToken">Signals operation cancellation.</param>
    /// <returns>The authorized write result.</returns>
    ValueTask<SettingsWriteResult> WriteAsync(
        SettingsWriteRequest request,
        AppOperationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exposes canonical settings documents through virtual filesystem operations.
/// </summary>
public interface ISettingsFileProjection
{
    /// <summary>Reads a projected settings file through the canonical settings service.</summary>
    ValueTask<SettingsReadResult> ReadFileAsync(
        VirtualPath path,
        AppOperationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Writes a projected settings file through validation and authorization.</summary>
    ValueTask<SettingsWriteResult> WriteFileAsync(
        SettingsWriteRequest request,
        AppOperationContext context,
        CancellationToken cancellationToken = default);
}