using System.Text.Json.Serialization;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Identifies the lifecycle and hosting model used by an application.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppKind>))]
public enum AppKind
{
    /// <summary>An interactive Blazor application hosted by the window manager.</summary>
    [JsonStringEnumMemberName("window")]
    Window,

    /// <summary>A command executable hosted by a terminal session.</summary>
    [JsonStringEnumMemberName("terminal")]
    Terminal,

    /// <summary>A non-visual application that runs for the active OS session.</summary>
    [JsonStringEnumMemberName("service")]
    Service
}