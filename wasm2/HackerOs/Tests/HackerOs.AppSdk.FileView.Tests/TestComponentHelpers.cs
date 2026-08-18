using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.AppSdk.FileView.Tests;

/// <summary>Small shared helpers for tests driving components through <see cref="TestComponentRenderer"/>.</summary>
internal static class TestComponentHelpers
{
    internal static IServiceProvider EmptyServices() => new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// Runs a synchronous mutation (selection, rename, expand) on the renderer's dispatcher — required for
    /// any call path that reaches <c>StateHasChanged</c>, exactly as production event handlers already do
    /// via Blazor's own dispatcher-bound event dispatch.
    /// </summary>
    internal static Task OnDispatcherAsync(TestComponentRenderer renderer, Action action) =>
        renderer.RunAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });

    /// <summary>
    /// A fake <c>FileView.razor.js</c> module wired only for <c>focusItem</c> — enough for
    /// <see cref="FileView.MoveActiveItemAsync"/> (<c>FV-011</c>'s roving-tabindex arrow-key navigation) to
    /// succeed without a real browser.
    /// </summary>
    internal static (FakeJSRuntime Runtime, FakeJSObjectReference Module) NewFocusJsRuntime()
    {
        FakeJSObjectReference module = new();
        module.Handlers["focusItem"] = _ => true;
        FakeJSRuntime runtime = new();
        runtime.Handlers["import"] = _ => module;
        return (runtime, module);
    }
}
