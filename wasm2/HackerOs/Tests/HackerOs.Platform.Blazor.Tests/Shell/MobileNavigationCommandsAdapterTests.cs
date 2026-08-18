using HackerOs.Platform.Blazor.Shell;
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
        MobileNavigationCommandsAdapter adapter = new(runtime);

        adapter.RequestHome();

        WindowRuntimeState after = Assert.Single(runtime.Windows);
        Assert.Equal(WindowVisualState.Minimized, after.VisualState);
    }

    [Fact]
    public void RequestHome_is_a_no_op_when_nothing_is_visible()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 375, 667));
        MobileNavigationCommandsAdapter adapter = new(runtime);

        adapter.RequestHome();

        Assert.Empty(runtime.Windows);
    }

    [Fact]
    public void RequestBack_and_RequestRecent_do_not_throw()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 375, 667));
        MobileNavigationCommandsAdapter adapter = new(runtime);

        adapter.RequestBack();
        adapter.RequestRecent();
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
