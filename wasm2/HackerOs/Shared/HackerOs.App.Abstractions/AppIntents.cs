namespace HackerOs.App.Abstractions;

/// <summary>
/// Identifies a versioned intent that crosses an application boundary.
/// </summary>
public interface IAppIntent
{
    /// <summary>Gets the stable namespaced intent identifier.</summary>
    string IntentId { get; }
}

/// <summary>
/// Defines stable identifiers for intents provided by the ecosystem.
/// </summary>
public static class AppIntentIds
{
    /// <summary>Launch an application directly.</summary>
    public const string LaunchApp = "org.hackeros.intent.launch-app.v1";

    /// <summary>Open or edit a virtual file through association resolution.</summary>
    public const string OpenFile = "org.hackeros.intent.open-file.v1";

    /// <summary>Reveal a virtual file in its containing folder.</summary>
    public const string RevealFile = "org.hackeros.intent.reveal-file.v1";

    /// <summary>Execute a command through an available terminal session.</summary>
    public const string ExecuteCommand = "org.hackeros.intent.execute-command.v1";

    /// <summary>Show an app's settings or status surface.</summary>
    public const string ShowAppSettings = "org.hackeros.intent.show-app-settings.v1";
}

/// <summary>
/// Requests direct launch of an enabled application.
/// </summary>
/// <param name="TargetAppId">Immutable target application identifier.</param>
/// <param name="Arguments">Opaque command-line-style arguments supplied to the target.</param>
public sealed record LaunchAppIntent(string TargetAppId, IReadOnlyList<string> Arguments) : IAppIntent
{
    /// <inheritdoc />
    public string IntentId => AppIntentIds.LaunchApp;
}

/// <summary>
/// Identifies the operation requested for a virtual file.
/// </summary>
public enum FileIntentAction
{
    /// <summary>Open the file using its default read/view behavior.</summary>
    Open,

    /// <summary>Edit the file using a compatible application.</summary>
    Edit
}

/// <summary>
/// Requests association-based opening or editing of a virtual file.
/// </summary>
/// <param name="Path">Canonical virtual filesystem path.</param>
/// <param name="Action">Requested file operation.</param>
/// <param name="MediaType">Detected media type, when known.</param>
/// <param name="PreferredAppId">Explicit preferred handler, when supplied by the caller.</param>
public sealed record OpenFileIntent(
    VirtualPath Path,
    FileIntentAction Action,
    string? MediaType = null,
    string? PreferredAppId = null) : IAppIntent
{
    /// <inheritdoc />
    public string IntentId => AppIntentIds.OpenFile;
}

/// <summary>
/// Requests that a file manager reveal a virtual file.
/// </summary>
/// <param name="Path">Canonical path to reveal.</param>
public sealed record RevealFileIntent(VirtualPath Path) : IAppIntent
{
    /// <inheritdoc />
    public string IntentId => AppIntentIds.RevealFile;
}

/// <summary>
/// Requests command execution through the shell rather than a concrete terminal app.
/// </summary>
/// <param name="CommandLine">Command line to parse and execute.</param>
/// <param name="WorkingDirectory">Optional initial virtual working directory.</param>
public sealed record ExecuteCommandIntent(
    string CommandLine,
    VirtualPath? WorkingDirectory = null) : IAppIntent
{
    /// <inheritdoc />
    public string IntentId => AppIntentIds.ExecuteCommand;
}

/// <summary>
/// Requests an application's settings or service-status UI.
/// </summary>
/// <param name="TargetAppId">Application whose settings should be shown.</param>
public sealed record ShowAppSettingsIntent(string TargetAppId) : IAppIntent
{
    /// <inheritdoc />
    public string IntentId => AppIntentIds.ShowAppSettings;
}

/// <summary>
/// Wraps an intent with authenticated caller and correlation information.
/// </summary>
/// <param name="RequestId">Unique request identifier used for tracing and results.</param>
/// <param name="CallerAppId">Application that created the request.</param>
/// <param name="UserId">Authenticated user on whose behalf it runs.</param>
/// <param name="Intent">Typed intent payload.</param>
public sealed record AppIntentRequest(
    Guid RequestId,
    string CallerAppId,
    string UserId,
    IAppIntent Intent);