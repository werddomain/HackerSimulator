using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Blazor.Shell;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Tests.Shell;

/// <summary>Tests for the MOB-009/MOB-010 scaffold's <see cref="MobileNavigationCommandsAdapter"/>.</summary>
public sealed class MobileNavigationCommandsAdapterTests
{
    [Fact]
    public void RequestHome_minimizes_the_focused_window_without_closing_it()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 375, 667));
        WindowRuntimeState window = CreateState(1);
        runtime.Apply(new CreateWindowCommand(window));
        MobileNavigationCommandsAdapter adapter = new(runtime, new AppBackHandlerRegistry());

        adapter.RequestHome();

        WindowRuntimeState after = Assert.Single(runtime.Windows);
        Assert.Equal(WindowVisualState.Minimized, after.VisualState);
    }

    [Fact]
    public void RequestHome_is_a_no_op_when_nothing_is_visible()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 375, 667));
        MobileNavigationCommandsAdapter adapter = new(runtime, new AppBackHandlerRegistry());

        adapter.RequestHome();

        Assert.Empty(runtime.Windows);
    }

    [Fact]
    public void RequestBack_and_RequestRecent_do_not_throw_when_nothing_is_visible()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 375, 667));
        MobileNavigationCommandsAdapter adapter = new(runtime, new AppBackHandlerRegistry());

        adapter.RequestBack();
        adapter.RequestRecent();
    }

    [Fact]
    public async Task RequestBack_invokes_the_primary_windows_registered_back_handler()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 375, 667));
        WindowRuntimeState window = CreateState(1);
        runtime.Apply(new CreateWindowCommand(window));
        AppBackHandlerRegistry backHandlers = new();
        TaskCompletionSource<AppBackRequest> invoked = new();
        using IDisposable registration = backHandlers.Register(window.Id, new RecordingHandler(invoked));
        MobileNavigationCommandsAdapter adapter = new(runtime, backHandlers);

        adapter.RequestBack();

        AppBackRequest request = await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(AppBackSource.SystemNavigationBar, request.Source);
    }

    private sealed class RecordingHandler(TaskCompletionSource<AppBackRequest> invoked) : IAppBackHandler
    {
        public bool CanNavigateBack => true;

#pragma warning disable CS0067 // Required by IAppBackHandler; unused by this test stub.
        public event Action? CanNavigateBackChanged;
#pragma warning restore CS0067

        public ValueTask<AppBackResult> NavigateBackAsync(AppBackRequest request, CancellationToken cancellationToken = default)
        {
            invoked.SetResult(request);
            return ValueTask.FromResult(AppBackResult.Handled);
        }
    }

    private static WindowRuntimeState CreateState(int seed) =>
        new(
            WindowId.FromGuid(Guid.Parse($"50000000-0000-0000-0000-{seed:D12}")),
            $"org.hackeros.test{seed}",
            WindowOwnerId.FromGuid(Guid.Parse($"60000000-0000-0000-0000-{seed:D12}")),
            $"Test {seed}",
            null,
            new WindowBounds(0, 0, 320, 480),
            null,
            0,
            WindowVisualState.Normal,
            new WindowConstraints(true, 320, 240),
            WindowModality.Modeless,
            null,
            isFocused: true);
}
