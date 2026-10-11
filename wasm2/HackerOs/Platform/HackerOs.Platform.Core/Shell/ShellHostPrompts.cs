using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk.Shell;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Turns the terminal's <see cref="HackCredentialPrompt"/> into the three questions the
/// interpreter asks (ADR 0056, section 7): <c>sudo</c>'s ticket, the one-shot approval after a
/// permission denial (ADR 0042) and the text-mode approval of dangerous capabilities (ADR 0049,
/// section 5). The terminal reads; this class verifies through the OS.
/// </summary>
internal sealed class ShellHostPrompts : IDangerousCapabilityApprovalPrompt
{
    private readonly HackCredentialPrompt _prompt;
    private readonly IElevationService _elevation;

    /// <summary>Creates the adapter for one hosted shell.</summary>
    /// <param name="prompt">The terminal's prompt.</param>
    /// <param name="elevation">Verifies the credentials and mints tickets (ADR 0042).</param>
    public ShellHostPrompts(HackCredentialPrompt prompt, IElevationService elevation)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
    }

    /// <summary>
    /// <c>sudo</c>'s credential window (ADR 0051, section 4): an administrator's credentials mint
    /// a single-use <c>SudoRoot</c> ticket. A wrong password prints "Sorry, try again." as
    /// <c>sudo</c> does, then the interpreter reports the failure.
    /// </summary>
    public async Task<ShellElevation?> RequestSudoAsync(string description, CancellationToken cancellationToken)
    {
        HackCredential? credential = await _prompt.ReadCredentialAsync(
            new HackCredentialRequest("Administrator Authorization Required", description), cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return null;
        }

        ElevationRequestResult result = await _elevation
            .RequestSudoAsync(credential.LoginName, credential.Password, cancellationToken).ConfigureAwait(false);
        if (result is { Success: true, Token: Guid token })
        {
            return new ShellElevation(token, ElevationScope.SudoRoot);
        }

        await _prompt.WriteLineAsync("Sorry, try again.", HackPromptLineKind.Error, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// ADR 0042: offers an administrator's one-shot approval when a lone command failed on a
    /// permission denial; the interpreter reruns it with the ticket.
    /// </summary>
    public async Task<ShellElevation?> RequestPermissionApprovalAsync(ShellPermissionDenial denial, CancellationToken cancellationToken)
    {
        FileSystemError denied = denial.Error;
        HackCredential? credential = await _prompt.ReadCredentialAsync(
            new HackCredentialRequest(
                "Permission Denied — Request Approval?",
                $"'{denial.CommandName}' was denied {denied.Code} on '{denied.Path}'. Approve this one operation?"),
            cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return null;
        }

        ElevationRequestResult result = await _elevation.RequestCapabilityApprovalAsync(
            credential.LoginName, credential.Password, denied.RequiredCapability!, denied.Path, cancellationToken).ConfigureAwait(false);
        if (result is { Success: true, Token: Guid token })
        {
            return new ShellElevation(token, ElevationScope.CapabilityApproval);
        }

        await _prompt.WriteLineAsync("Sorry, try again.", HackPromptLineKind.Error, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Text mode of the dangerous-capability approval (ADR 0049, section 5) for a third-party
    /// command: a warning banner and the capability list in the terminal, a yes / no / cancel
    /// choice, then an administrator's name and password, like <c>sudo</c>. The prompt decides
    /// nothing: credentials go to <see cref="DangerousCapabilityApprovalRequest.ApproveAsync"/>,
    /// which verifies them, writes <c>/etc/hackeros/app-permissions.config</c> and logs to
    /// <c>/var/log/auth.log</c>.
    /// </summary>
    public async Task<DangerousCapabilityApprovalOutcome> RequestAsync(
        DangerousCapabilityApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AppManifest app = request.Manifest;

        await WriteAsync("*** AUTORISATION ADMINISTRATEUR REQUISE ***", HackPromptLineKind.Warning).ConfigureAwait(false);
        await WriteAsync(
            $"La commande non système '{app.Terminal?.Name ?? app.Name}' ({app.Id}, éditeur {app.PublisherId}, version {app.Version}) demande :",
            HackPromptLineKind.Warning).ConfigureAwait(false);
        foreach (CapabilityDescription capability in request.Capabilities)
        {
            await WriteAsync($"  - {capability.Title} [{capability.Capability}]", HackPromptLineKind.Warning).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(capability.RiskDescription))
            {
                await WriteAsync($"    {capability.RiskDescription}", HackPromptLineKind.Warning).ConfigureAwait(false);
            }
        }

        await WriteAsync("Cette décision sera inscrite dans /var/log/auth.log.", HackPromptLineKind.Info).ConfigureAwait(false);

        HackConfirmation choice = await _prompt.ConfirmAsync(new HackConfirmationRequest(
            "Autorisation administrateur requise",
            $"Autoriser '{app.Name}' à utiliser ces capacités ?\n"
                + "Oui : un administrateur saisit son nom et son mot de passe.\n"
                + "Non : refuser pour vous (on ne vous le redemandera plus).\n"
                + "Annuler : décider au prochain lancement."), cancellationToken).ConfigureAwait(false);

        if (choice == HackConfirmation.No)
        {
            await WriteAsync("Capacités refusées pour vous.", HackPromptLineKind.Info).ConfigureAwait(false);
            return DangerousCapabilityApprovalOutcome.Refused;
        }

        if (choice != HackConfirmation.Yes)
        {
            await WriteAsync("Aucune décision : la commande démarre sans ces capacités.", HackPromptLineKind.Info).ConfigureAwait(false);
            return DangerousCapabilityApprovalOutcome.Dismissed;
        }

        while (true)
        {
            HackCredential? credential = await _prompt.ReadCredentialAsync(new HackCredentialRequest(
                "Autorisation administrateur requise",
                $"Approuver les capacités dangereuses de '{app.Name}' pour toute la machine."), cancellationToken).ConfigureAwait(false);
            if (credential is null)
            {
                await WriteAsync("Aucune décision : la commande démarre sans ces capacités.", HackPromptLineKind.Info).ConfigureAwait(false);
                return DangerousCapabilityApprovalOutcome.Dismissed;
            }

            DangerousCapabilityApprovalAttempt attempt = await request.ApproveAsync(
                credential.LoginName, credential.Password, cancellationToken).ConfigureAwait(false);
            if (attempt.Succeeded)
            {
                await WriteAsync("Capacités autorisées par un administrateur.", HackPromptLineKind.Info).ConfigureAwait(false);
                return DangerousCapabilityApprovalOutcome.Approved;
            }

            if (attempt.RemainingAttempts <= 0)
            {
                await WriteAsync(
                    $"{DangerousCapabilityApprovalRequest.MaxAttempts} essais incorrects : demande annulée.",
                    HackPromptLineKind.Error).ConfigureAwait(false);
                return DangerousCapabilityApprovalOutcome.Dismissed;
            }

            await WriteAsync("Désolé, réessayez.", HackPromptLineKind.Error).ConfigureAwait(false);
        }

        Task WriteAsync(string text, HackPromptLineKind kind) => _prompt.WriteLineAsync(text, kind, cancellationToken);
    }
}
