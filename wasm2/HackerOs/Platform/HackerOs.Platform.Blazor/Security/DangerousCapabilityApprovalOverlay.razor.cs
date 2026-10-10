using HackerOs.Platform.Core.Policy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.Platform.Blazor.Security;

/// <summary>
/// Secure-desktop overlay asking an administrator to approve the dangerous capabilities of a
/// non-system app at launch (ADR 0049 section 6), modelled on the Windows UAC secure desktop.
/// </summary>
/// <remarks>
/// <para>
/// The shell renders exactly one instance above every window and the taskbar. It attaches itself
/// to <see cref="DangerousCapabilityApprovalBroker"/> so launches can reach it, and shows the
/// broker's current request: app identity, each capability with its title and risk sentence, an
/// administrator name (prefilled with the session user only when they are an administrator) and a
/// password.
/// </para>
/// <para>
/// The overlay decides nothing: <em>Autoriser</em> submits the credentials to
/// <see cref="DangerousCapabilityApprovalRequest.ApproveAsync"/>, which verifies them, writes the
/// machine file and logs. After the third failure the request closes as dismissed.
/// <em>Refuser</em> ends it as refused; Escape closes it without a choice.
/// </para>
/// </remarks>
public sealed partial class DangerousCapabilityApprovalOverlay
{
    private DangerousCapabilityApprovalBroker.PendingApproval? _current;
    private IDisposable? _attachment;
    private ElementReference _loginInput;
    private ElementReference _passwordInput;
    private string _login = string.Empty;
    private string _password = string.Empty;
    private string? _error;
    private bool _isVerifying;
    private bool _focusPending;

    /// <summary>Gets or sets the broker queuing approval requests.</summary>
    [Inject]
    public DangerousCapabilityApprovalBroker Broker { get; set; } = default!;

    /// <summary>Gets whether <em>Autoriser</em> is enabled: a name and a password are both required.</summary>
    private bool CanApprove => !_isVerifying && !string.IsNullOrWhiteSpace(_login) && !string.IsNullOrEmpty(_password);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _attachment = Broker.Attach();
        Broker.Changed += OnBrokerChanged;
        ShowCurrent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusPending || _current is null)
        {
            return;
        }

        _focusPending = false;
        try
        {
            // Take the keyboard as soon as the overlay appears, like a secure desktop.
            await (string.IsNullOrEmpty(_login) ? _loginInput : _passwordInput).FocusAsync();
        }
        catch (InvalidOperationException)
        {
            // Element not attached (prerendering or test renderer): focus is a convenience only.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Broker.Changed -= OnBrokerChanged;
        _attachment?.Dispose();
    }

    private void OnBrokerChanged() => _ = InvokeAsync(() =>
    {
        ShowCurrent();
        StateHasChanged();
    });

    private void ShowCurrent()
    {
        DangerousCapabilityApprovalBroker.PendingApproval? next = Broker.Current;
        if (ReferenceEquals(next, _current))
        {
            return;
        }

        _current = next;
        _login = next?.Request.SuggestedAdministratorLogin ?? string.Empty;
        _password = string.Empty;
        _error = null;
        _isVerifying = false;
        _focusPending = next is not null;
    }

    private async Task ApproveAsync()
    {
        if (_current is not { } pending || !CanApprove)
        {
            return;
        }

        _isVerifying = true;
        _error = null;
        string password = _password;
        _password = string.Empty;
        DangerousCapabilityApprovalAttempt attempt;
        try
        {
            attempt = await pending.Request.ApproveAsync(_login.Trim(), password, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _isVerifying = false;
            _error = $"La décision n'a pas pu être enregistrée : {exception.Message}";
            return;
        }

        _isVerifying = false;
        if (attempt.Succeeded)
        {
            pending.Complete(DangerousCapabilityApprovalOutcome.Approved);
            return;
        }

        if (attempt.RemainingAttempts <= 0)
        {
            // Three failures cancel the request: nothing is written (ADR 0049 section 5).
            pending.Complete(DangerousCapabilityApprovalOutcome.Dismissed);
            return;
        }

        _error = attempt.FailureReason switch
        {
            "not-administrator" => $"Ce compte n'est pas administrateur. Essais restants : {attempt.RemainingAttempts}.",
            "user-disabled" => $"Ce compte est désactivé. Essais restants : {attempt.RemainingAttempts}.",
            _ => $"Nom ou mot de passe incorrect. Essais restants : {attempt.RemainingAttempts}."
        };
        _focusPending = true;
    }

    private void Refuse() => _current?.Complete(DangerousCapabilityApprovalOutcome.Refused);

    private Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && !_isVerifying)
        {
            // Escape closes without a choice: the question comes back at the next launch.
            _current?.Complete(DangerousCapabilityApprovalOutcome.Dismissed);
        }

        return Task.CompletedTask;
    }
}
