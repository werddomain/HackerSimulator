namespace HackerOs.AppSdk.Components;

/// <summary>
/// Names of the embeddable component contracts the OS knows (ADR 0063). An app registers a
/// component for a contract in its manifest (<c>AppDefinition.Components</c>); another app embeds
/// it through <c>HackEmbeddedComponent</c> without referencing the provider's assembly, as a KDE
/// app embeds Konsole's KPart.
/// </summary>
/// <remarks>
/// A contract name is stable: it names an interface of this SDK that the embedded component hands
/// back to its host (the controller), and the parameters the host passes to it.
/// </remarks>
public static class HackComponentContracts
{
    /// <summary>
    /// A terminal emulator running its own <c>hsh</c>, controlled through <see cref="IHackTerminal"/>
    /// and configured with <see cref="HackTerminalOptions"/>. Provided by the Terminal app.
    /// </summary>
    public const string Terminal = "hackeros.terminal";
}
