using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Settings &gt; Applications &gt; Permissions (ADR 0049 section 7): the dangerous-capability policy
/// and, for every app declaring a dangerous capability, each capability's state (system, allowed,
/// refused by you, refused by the administrator, blocked by the policy, pending).
/// </summary>
/// <remarks>
/// Uses only <see cref="HackPermissionAdministration"/> (ADR 0056 section 9, capability
/// <c>permissions.manage</c>). A machine change makes the OS ask an administrator for their password
/// in its own secure window (ADR 0055 section 5); this panel never sees it. Withdrawing one's own
/// refusal needs no administrator. Every change applies at the app's next launch.
/// </remarks>
public sealed partial class AppPermissionsPanel
{
    private IReadOnlyList<HackAppPermissionInfo> _overview = [];
    private HackDangerousCapabilityPolicy _policy;
    private bool _isAvailable = true;
    private string? _message;
    private bool _isBusy;

    /// <summary>Gets or sets the Settings window's execution context.</summary>
    [Parameter, EditorRequired]
    public IAppExecutionContext AppContext { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is NotSupportedException or UnauthorizedAccessException)
        {
            // A host without the administration services, or a Settings build without the capability.
            _isAvailable = false;
        }
    }

    private async Task RefreshAsync()
    {
        _policy = await HackPermissionAdministration.GetPolicyAsync(AppContext, AppContext.CancellationToken);
        _overview = await HackPermissionAdministration.GetOverviewAsync(AppContext, AppContext.CancellationToken);
    }

    private Task ChangePolicyAsync(HackDangerousCapabilityPolicy policy) =>
        policy == _policy
            ? Task.CompletedTask
            : RunAsync(
                () => HackPermissionAdministration.SetPolicyAsync(AppContext, policy, AppContext.CancellationToken),
                policy == HackDangerousCapabilityPolicy.Block ? "Politique : toujours bloquer." : "Politique : demander à un administrateur.");

    private Task SetMachineDecisionAsync(
        HackAppPermissionInfo app, HackCapabilityPermissionInfo capability, HackPermissionDecision decision) =>
        RunAsync(
            () => HackPermissionAdministration.SetMachineDecisionAsync(
                AppContext, app.AppId, capability.Capability, decision, AppContext.CancellationToken),
            $"{app.AppName} : décision enregistrée, effective au prochain lancement.");

    private Task RemoveOwnDenialAsync(HackAppPermissionInfo app, HackCapabilityPermissionInfo capability) =>
        RunAsync(
            () => HackPermissionAdministration.RemoveOwnDenialAsync(
                AppContext, app.AppId, capability.Capability, AppContext.CancellationToken),
            $"{app.AppName} : votre refus est retiré, la question reviendra au prochain lancement.");

    private async Task RunAsync(Func<Task> action, string successMessage)
    {
        _isBusy = true;
        _message = null;
        try
        {
            await action();
            _message = successMessage;
            await RefreshAsync();
        }
        catch (UnauthorizedAccessException)
        {
            _message = "Aucun administrateur n'a confirmé : rien n'a été modifié.";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            _message = $"La décision n'a pas pu être enregistrée : {exception.Message}";
        }
        finally
        {
            _isBusy = false;
        }
    }

    private string PolicyClass(HackDangerousCapabilityPolicy policy) =>
        policy == _policy ? "policy-option selected" : "policy-option";

    private static string StateLabel(HackPermissionState state) => state switch
    {
        HackPermissionState.System => "Système",
        HackPermissionState.Allowed => "Autorisée",
        HackPermissionState.DeniedByUser => "Refusée par vous",
        HackPermissionState.DeniedByAdministrator => "Refusée par l'administrateur",
        HackPermissionState.BlockedByPolicy => "Bloquée par la politique",
        _ => "En attente"
    };

    private static string StateClass(HackPermissionState state) => state switch
    {
        HackPermissionState.System or HackPermissionState.Allowed => "capability-state granted",
        HackPermissionState.Pending => "capability-state pending",
        _ => "capability-state denied"
    };
}
