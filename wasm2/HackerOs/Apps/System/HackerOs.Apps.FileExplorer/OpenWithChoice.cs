using HackerOs.AppSdk.Apps;

namespace HackerOs.Apps.FileExplorer;

/// <summary>What the "Open with" dialog returns: the app, and whether to remember it (ADR 0121, section 4).</summary>
/// <param name="App">Chosen app.</param>
/// <param name="AlwaysUse">
/// Whether the user ticked "Always use this app": the explorer then writes it as their default for
/// the file's extension, in <c>~/.config/hackeros/file-associations.json</c>.
/// </param>
internal sealed record OpenWithChoice(HackAppInfo App, bool AlwaysUse);
