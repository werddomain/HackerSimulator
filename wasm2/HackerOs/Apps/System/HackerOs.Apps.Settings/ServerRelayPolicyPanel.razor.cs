using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Shows the connected server's network relay policy (ADR 0102 §5) inside Settings &gt; Server
/// Connection: allowed ports per protocol, private networks, quotas, the apps the server grants
/// the relay to, and a warning per setting that weakens the defaults.
/// </summary>
/// <remarks>
/// Uses only <see cref="HackServerConnection.GetRelayPolicyAsync(IAppExecutionContext, CancellationToken)"/>
/// (capability <c>server.connection.manage</c>, which Settings declares). Rendered only while the
/// device is connected; a server that cannot be reached shows its error instead of the table.
/// </remarks>
public sealed partial class ServerRelayPolicyPanel
{
    private HackServerRelayPolicy? _policy;
    private string? _error;
    private bool _isLoading = true;

    private IAppExecutionContext Context => AppContext
        ?? throw new InvalidOperationException("This panel must be rendered inside the Settings window.");

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _policy = await HackServerConnection.GetRelayPolicyAsync(Context, Context.CancellationToken);
        }
        catch (HackServerConnectionException exception)
        {
            _error = exception.Message;
        }
        catch (Exception exception) when (exception is NotSupportedException or UnauthorizedAccessException)
        {
            // A host without the relay client, or a Settings build without the capability.
            _policy = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Formats a port list; an empty list means every port.</summary>
    private string Ports(IReadOnlyList<int> ports) =>
        _policy is { AllowAllPorts: true } || ports.Count == 0 ? "All ports" : string.Join(", ", ports);

    private static string Quotas(HackServerRelayPolicy policy) =>
        policy.QuotasEnabled
            ? $"{policy.MaxConcurrentRequests} requests, {policy.OpenStreams}/{policy.MaxStreams} sockets open, " +
              $"bodies up to {policy.MaxResponseBytes / (1024 * 1024)} MiB, idle sockets closed after {policy.StreamIdleTimeout.TotalSeconds:0} s"
            : "Disabled";
}
