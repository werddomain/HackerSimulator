using Microsoft.JSInterop;

namespace HackerOs.AppSdk.FileView.Tests;

/// <summary>
/// Scriptable <see cref="IJSRuntime"/>/<see cref="IJSObjectReference"/> doubles for
/// <c>FileViewIcons</c>'s marquee-select JS interop (<c>FileViewIcons.razor.js</c>) — there is no real
/// browser/JS host in this xunit process. Each call is dispatched to a registered handler by identifier.
/// </summary>
internal sealed class FakeJSRuntime : IJSRuntime
{
    public Dictionary<string, Func<object?[]?, object?>> Handlers { get; } = [];

    public List<string> CalledIdentifiers { get; } = [];

    public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        CalledIdentifiers.Add(identifier);
        Calls.Add((identifier, args));
        if (!Handlers.TryGetValue(identifier, out Func<object?[]?, object?>? handler))
        {
            throw new InvalidOperationException($"No handler registered for '{identifier}'.");
        }

        return ValueTask.FromResult((TValue)handler(args)!);
    }
}

internal sealed class FakeJSObjectReference : IJSObjectReference
{
    public Dictionary<string, Func<object?[]?, object?>> Handlers { get; } = [];

    public List<string> CalledIdentifiers { get; } = [];

    public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

    public bool Disposed { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        CalledIdentifiers.Add(identifier);
        Calls.Add((identifier, args));
        if (!Handlers.TryGetValue(identifier, out Func<object?[]?, object?>? handler))
        {
            throw new InvalidOperationException($"No handler registered for '{identifier}'.");
        }

        return ValueTask.FromResult((TValue)handler(args)!);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
