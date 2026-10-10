using HackerOs.AppSdk;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Re-enters one app instance's <see cref="HackExecutionContext"/> around callbacks a gateway invokes
/// on the app's behalf (ADR 0043, section 2). A clock tick or an event publication runs on the
/// publisher's flow, not the subscriber's, so without this the handler would see no — or the wrong —
/// ambient app.
/// </summary>
/// <remarks>
/// Gateways are constructed before the <see cref="IAppExecutionContext"/> that owns them, so the
/// factory binds the finished context afterwards with <see cref="Bind"/>. Until then (and for gateways
/// built outside the factory, as in tests) callbacks run unchanged.
/// </remarks>
internal sealed class AppAmbientContextBinding
{
    private IAppExecutionContext? _context;

    /// <summary>Binds the context every wrapped callback enters. Called once by the factory.</summary>
    public void Bind(IAppExecutionContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>Wraps <paramref name="callback"/> so it runs inside the bound app's ambient scope.</summary>
    public Action Wrap(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return () =>
        {
            using IDisposable? scope = Enter();
            callback();
        };
    }

    /// <summary>Wraps <paramref name="handler"/> so it runs inside the bound app's ambient scope.</summary>
    public Action<T> Wrap<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return value =>
        {
            using IDisposable? scope = Enter();
            handler(value);
        };
    }

    private IDisposable? Enter() => _context is { } context ? HackExecutionContext.Enter(context) : null;
}
