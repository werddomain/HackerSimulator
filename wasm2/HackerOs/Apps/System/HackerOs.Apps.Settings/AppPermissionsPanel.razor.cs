using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Settings &gt; Applications &gt; Permissions (ADR 0049 section 7): the dangerous-capability policy
/// and, for every app declaring a dangerous capability, each capability's state (system, allowed,
/// refused by you, refused by the administrator, blocked by the policy, pending).
/// </summary>
/// <remarks>
/// Machine changes ask for an administrator's name and password with the credential prompt of
/// ADR 0042; <see cref="AppPermissionAdministration"/> verifies them, writes the file as an audited
/// system operation and logs to <c>/var/log/auth.log</c>. Withdrawing one's own refusal needs no
/// administrator. Every change applies at the app's next launch.
/// </remarks>
public sealed partial class AppPermissionsPanel
{
    private AppPermissionAdministration? _administration;
    private IReadOnlyList<AppPermissionOverview> _overview = [];
    private DangerousCapabilityPolicy _policy;
    private string? _message;
    private bool _isBusy;

    /// <summary>Gets or sets the Settings window's execution context (dialogs are scoped to it).</summary>
    [Parameter, EditorRequired]
    public IAppExecutionContext AppContext { get; set; } = default!;

    /// <summary>Gets or sets the dialog service used for the administrator credential prompt.</summary>
    [Parameter, EditorRequired]
    public IDialogService Dialogs { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    [Inject]
    private ISessionService SessionService { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        // Optional so the Settings app still renders on a host without ADR 0049 services.
        _administration = Services.GetService<AppPermissionAdministration>();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_administration is null)
        {
            return;
        }

        _policy = await _administration.GetPolicyAsync(AppContext.CancellationToken);
        _overview = await _administration.GetOverviewAsync(AppContext.CancellationToken);
    }

    private async Task ChangePolicyAsync(DangerousCapabilityPolicy policy)
    {
        if (_administration is null || policy == _policy || SessionService.CurrentPrincipal is not { } principal)
        {
            return;
        }

        await RunAdministratorActionAsync(
            policy == DangerousCapabilityPolicy.Block
                ? "Bloquer toutes les capacités dangereuses des apps non système."
                : "Laisser un administrateur approuver les capacités dangereuses au lancement.",
            (login, password) => _administration.SetPolicyAsync(policy, login, password, principal, AppContext.CancellationToken),
            policy == DangerousCapabilityPolicy.Block ? "Politique : toujours bloquer." : "Politique : demander à un administrateur.");
    }

    private async Task SetMachineDecisionAsync(
        AppPermissionOverview app, DangerousCapabilityStatus capability, AppPermissionDecision? decision)
    {
        if (_administration is null || SessionService.CurrentPrincipal is not { } principal)
        {
            return;
        }

        string action = decision switch
        {
            AppPermissionDecision.Allow => "Autoriser",
            AppPermissionDecision.Deny => "Révoquer",
            _ => "Redemander au lancement"
        };
        await RunAdministratorActionAsync(
            $"{action} « {capability.Description.Title} » pour '{app.Manifest.Name}' sur toute la machine.",
            (login, password) => _administration.SetMachineDecisionAsync(
                app.Manifest.Id, capability.Capability, decision, login, password, principal, AppContext.CancellationToken),
            $"{app.Manifest.Name} : décision enregistrée, effective au prochain lancement.");
    }

    private async Task RemoveOwnDenialAsync(AppPermissionOverview app, DangerousCapabilityStatus capability)
    {
        if (_administration is null)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await _administration.RemoveOwnDenialAsync(app.Manifest.Id, capability.Capability, AppContext.CancellationToken);
            _message = $"{app.Manifest.Name} : votre refus est retiré, la question reviendra au prochain lancement.";
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _message = $"Impossible de modifier votre fichier : {exception.Message}";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task RunAdministratorActionAsync(
        string description,
        Func<string, string, Task<AppPermissionAdministrationResult>> action,
        string successMessage)
    {
        _isBusy = true;
        _message = null;
        try
        {
            CredentialPromptDialogResult prompt = await Dialogs.CredentialPromptAsync(AppContext, new CredentialPromptDialogRequest
            {
                Title = "Autorisation administrateur requise",
                Description = description
            });
            if (prompt.Status != CredentialPromptStatus.Submitted)
            {
                return;
            }

            AppPermissionAdministrationResult result = await action(prompt.LoginName ?? string.Empty, prompt.Password ?? string.Empty);
            _message = result.Succeeded
                ? successMessage
                : result.FailureReason == "not-administrator"
                    ? "Ce compte n'est pas administrateur : rien n'a été modifié."
                    : "Nom ou mot de passe incorrect : rien n'a été modifié.";
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _message = $"La décision n'a pas pu être enregistrée : {exception.Message}";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private string PolicyClass(DangerousCapabilityPolicy policy) =>
        policy == _policy ? "policy-option selected" : "policy-option";

    private static string StateLabel(DangerousCapabilityState state) => state switch
    {
        DangerousCapabilityState.System => "Système",
        DangerousCapabilityState.Allowed => "Autorisée",
        DangerousCapabilityState.DeniedByUser => "Refusée par vous",
        DangerousCapabilityState.DeniedByAdministrator => "Refusée par l'administrateur",
        DangerousCapabilityState.BlockedByPolicy => "Bloquée par la politique",
        _ => "En attente"
    };

    private static string StateClass(DangerousCapabilityState state) => state switch
    {
        DangerousCapabilityState.System or DangerousCapabilityState.Allowed => "capability-state granted",
        DangerousCapabilityState.Pending => "capability-state pending",
        _ => "capability-state denied"
    };
}
