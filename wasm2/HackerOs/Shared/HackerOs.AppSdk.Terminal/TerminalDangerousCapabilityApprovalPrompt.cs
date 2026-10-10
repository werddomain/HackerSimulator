using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Core.Policy;

namespace HackerOs.AppSdk.Terminal;

/// <summary>Kind of a line the text-mode approval writes to the terminal.</summary>
public enum TerminalApprovalLineKind
{
    /// <summary>Warning banner and capability list.</summary>
    Warning,

    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>Failure (wrong password, request cancelled).</summary>
    Error
}

/// <summary>
/// Text mode of the dangerous-capability approval (ADR 0049 section 5) for a third-party command
/// launched from the terminal: a warning banner and the capability list are printed in the
/// terminal itself, then the administrator name and password are asked like <c>sudo</c> does.
/// </summary>
/// <remarks>
/// Host commands are system apps and never reach this prompt. The prompt decides nothing:
/// credentials go to <see cref="DangerousCapabilityApprovalRequest.ApproveAsync"/>, which verifies
/// them, writes <c>/etc/hackeros/app-permissions.config</c> and logs to <c>/var/log/auth.log</c>.
/// </remarks>
public sealed class TerminalDangerousCapabilityApprovalPrompt : IDangerousCapabilityApprovalPrompt
{
    private readonly IDialogService _dialogs;
    private readonly IAppExecutionContext _context;
    private readonly Func<string, TerminalApprovalLineKind, Task> _writeLine;

    /// <summary>Creates the prompt for one terminal.</summary>
    /// <param name="dialogs">Dialog service used for the choice and the credential prompt, as <c>sudo</c> does.</param>
    /// <param name="context">Execution context of the terminal app hosting the shell.</param>
    /// <param name="writeLine">Appends one line to the terminal output.</param>
    public TerminalDangerousCapabilityApprovalPrompt(
        IDialogService dialogs,
        IAppExecutionContext context,
        Func<string, TerminalApprovalLineKind, Task> writeLine)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
    }

    /// <inheritdoc />
    public async Task<DangerousCapabilityApprovalOutcome> RequestAsync(
        DangerousCapabilityApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AppManifest app = request.Manifest;

        await _writeLine("*** AUTORISATION ADMINISTRATEUR REQUISE ***", TerminalApprovalLineKind.Warning);
        await _writeLine(
            $"La commande non système '{app.Terminal?.Name ?? app.Name}' ({app.Id}, éditeur {app.PublisherId}, version {app.Version}) demande :",
            TerminalApprovalLineKind.Warning);
        foreach (CapabilityDescription capability in request.Capabilities)
        {
            await _writeLine($"  - {capability.Title} [{capability.Capability}]", TerminalApprovalLineKind.Warning);
            if (!string.IsNullOrEmpty(capability.RiskDescription))
            {
                await _writeLine($"    {capability.RiskDescription}", TerminalApprovalLineKind.Warning);
            }
        }

        await _writeLine("Cette décision sera inscrite dans /var/log/auth.log.", TerminalApprovalLineKind.Info);

        MessageBoxDialogResult choice = await _dialogs.MessageBoxAsync(_context, new MessageBoxDialogRequest
        {
            Title = "Autorisation administrateur requise",
            Content = $"Autoriser '{app.Name}' à utiliser ces capacités ?\n"
                + "Oui : un administrateur saisit son nom et son mot de passe.\n"
                + "Non : refuser pour vous (on ne vous le redemandera plus).\n"
                + "Annuler : décider au prochain lancement.",
            DialogType = MessageBoxType.YesNoCancel
        }, cancellationToken);

        if (choice.Result == MessageBoxResult.No)
        {
            await _writeLine("Capacités refusées pour vous.", TerminalApprovalLineKind.Info);
            return DangerousCapabilityApprovalOutcome.Refused;
        }

        if (choice.Result != MessageBoxResult.Yes)
        {
            await _writeLine("Aucune décision : la commande démarre sans ces capacités.", TerminalApprovalLineKind.Info);
            return DangerousCapabilityApprovalOutcome.Dismissed;
        }

        while (true)
        {
            CredentialPromptDialogResult credentials = await _dialogs.CredentialPromptAsync(_context, new CredentialPromptDialogRequest
            {
                Title = "Autorisation administrateur requise",
                Description = $"Approuver les capacités dangereuses de '{app.Name}' pour toute la machine."
            }, cancellationToken);
            if (credentials.Status != CredentialPromptStatus.Submitted)
            {
                await _writeLine("Aucune décision : la commande démarre sans ces capacités.", TerminalApprovalLineKind.Info);
                return DangerousCapabilityApprovalOutcome.Dismissed;
            }

            DangerousCapabilityApprovalAttempt attempt = await request.ApproveAsync(
                credentials.LoginName ?? string.Empty, credentials.Password ?? string.Empty, cancellationToken);
            if (attempt.Succeeded)
            {
                await _writeLine("Capacités autorisées par un administrateur.", TerminalApprovalLineKind.Info);
                return DangerousCapabilityApprovalOutcome.Approved;
            }

            if (attempt.RemainingAttempts <= 0)
            {
                await _writeLine(
                    $"{DangerousCapabilityApprovalRequest.MaxAttempts} essais incorrects : demande annulée.",
                    TerminalApprovalLineKind.Error);
                return DangerousCapabilityApprovalOutcome.Dismissed;
            }

            await _writeLine("Désolé, réessayez.", TerminalApprovalLineKind.Error);
        }
    }
}
