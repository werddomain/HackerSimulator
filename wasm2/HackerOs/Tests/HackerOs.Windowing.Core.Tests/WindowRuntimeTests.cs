using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Windowing.Core;

namespace HackerOs.Windowing.Core.Tests;

public sealed class WindowRuntimeTests
{
    [Fact]
    public void Create_focus_and_minimize_keep_one_frontmost_visible_focus()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState first = CreateState(1);
        WindowRuntimeState second = CreateState(2);

        runtime.Apply(new CreateWindowCommand(first));
        runtime.Apply(new CreateWindowCommand(second));
        IReadOnlyList<WindowEvent> events = runtime.Apply(new MinimizeWindowCommand(second.Id));

        Assert.Equal(2, runtime.Windows.Select(window => window.ZOrder).Distinct().Count());
        Assert.Equal(first.Id, Assert.Single(runtime.Windows, window => window.IsFocused).Id);
        Assert.Equal(WindowVisualState.Minimized, runtime.Windows.Single(window => window.Id == second.Id).VisualState);
        Assert.Collection(
            events,
            item => Assert.IsType<WindowVisualStateChangedEvent>(item),
            item => Assert.Equal(first.Id, Assert.IsType<WindowFocusedEvent>(item).WindowId));
    }

    [Fact]
    public void Maximize_restore_and_viewport_change_preserve_restore_geometry()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState initial = CreateState(1);
        runtime.Apply(new CreateWindowCommand(initial));

        runtime.Apply(new MaximizeWindowCommand(initial.Id));
        runtime.Apply(new ChangeViewportCommand(new WindowBounds(0, 40, 1024, 728)));
        WindowRuntimeState maximized = Assert.Single(runtime.Windows);
        Assert.Equal(new WindowBounds(0, 40, 1024, 728), maximized.Bounds);
        WindowBounds constrainedRestoreBounds = new(20, 40, 640, 480);
        Assert.Equal(constrainedRestoreBounds, maximized.RestoreBounds);

        runtime.Apply(new RestoreWindowCommand(initial.Id));
        WindowRuntimeState restored = Assert.Single(runtime.Windows);
        Assert.Equal(constrainedRestoreBounds, restored.Bounds);
        Assert.Null(restored.RestoreBounds);
    }

    [Fact]
    public void Resize_enforces_constraints_and_normal_state()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState initial = CreateState(1);
        runtime.Apply(new CreateWindowCommand(initial));

        runtime.Apply(new ResizeWindowCommand(initial.Id, new WindowBounds(20, 20, 200, 200)));
        Assert.Equal(320, Assert.Single(runtime.Windows).Bounds.Width);
        runtime.Apply(new MaximizeWindowCommand(initial.Id));
        Assert.Throws<InvalidOperationException>(() => runtime.Apply(new MoveWindowCommand(initial.Id, 50, 50)));
    }

    [Fact]
    public void Move_resize_and_mobile_viewport_keep_window_inside_work_area()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState initial = CreateState(1);
        runtime.Apply(new CreateWindowCommand(initial));

        runtime.Apply(new MoveWindowCommand(initial.Id, 1200, 700));
        Assert.Equal(new WindowBounds(640, 240, 640, 480), Assert.Single(runtime.Windows).Bounds);

        runtime.Apply(new ChangeViewportCommand(new WindowBounds(0, 0, 375, 600)));
        Assert.Equal(new WindowBounds(0, 120, 375, 480), Assert.Single(runtime.Windows).Bounds);
    }

    [Fact]
    public void Close_request_is_idempotent_and_force_close_refocuses()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState first = CreateState(1);
        WindowRuntimeState second = CreateState(2);
        runtime.Apply(new CreateWindowCommand(first));
        runtime.Apply(new CreateWindowCommand(second));

        Assert.Single(runtime.Apply(new RequestWindowCloseCommand(second.Id)));
        Assert.Empty(runtime.Apply(new RequestWindowCloseCommand(second.Id)));
        IReadOnlyList<WindowEvent> events = runtime.Apply(new ForceWindowCloseCommand(second.Id));

        Assert.Equal(first.Id, Assert.Single(runtime.Windows).Id);
        Assert.True(runtime.Windows[0].IsFocused);
        Assert.False(Assert.IsType<WindowClosedEvent>(events[0]).WasForced);
        Assert.Equal(first.Id, Assert.IsType<WindowFocusedEvent>(events[1]).WindowId);
    }

    [Fact]
    public void Cancel_close_clears_pending_close_without_removing_window()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState window = CreateState(1);
        runtime.Apply(new CreateWindowCommand(window));
        runtime.Apply(new RequestWindowCloseCommand(window.Id));

        Assert.IsType<WindowCloseCancelledEvent>(Assert.Single(runtime.Apply(new CancelWindowCloseCommand(window.Id))));
        Assert.Empty(runtime.Apply(new CancelWindowCloseCommand(window.Id)));
        Assert.Single(runtime.Windows);
        Assert.True(Assert.IsType<WindowClosedEvent>(runtime.Apply(new ForceWindowCloseCommand(window.Id))[0]).WasForced);
    }

    [Fact]
    public void Owner_modal_window_blocks_owner_and_returns_focus_when_closed()
    {
        WindowRuntime runtime = new(new WindowBounds(0, 0, 1280, 720));
        WindowRuntimeState owner = CreateState(1);
        runtime.Apply(new CreateWindowCommand(owner));
        WindowRuntimeState modal = CreateState(2, WindowModality.OwnerModal, owner.Id);
        runtime.Apply(new CreateWindowCommand(modal));

        Assert.True(runtime.IsInteractionBlocked(owner.Id));
        Assert.Throws<InvalidOperationException>(() => runtime.Apply(new FocusWindowCommand(owner.Id)));

        runtime.Apply(new ForceWindowCloseCommand(modal.Id));
        Assert.False(runtime.IsInteractionBlocked(owner.Id));
        Assert.True(Assert.Single(runtime.Windows).IsFocused);
    }

    private static WindowRuntimeState CreateState(
        int seed,
        WindowModality modality = WindowModality.Modeless,
        WindowId? ownerId = null) =>
        new(
            WindowId.FromGuid(Guid.Parse($"10000000-0000-0000-0000-{seed:D12}")),
            $"org.hackeros.test{seed}",
            ProcessId.FromInt64(seed),
            AppInstanceId.FromGuid(Guid.Parse($"20000000-0000-0000-0000-{seed:D12}")),
            $"Test {seed}",
            null,
            new WindowBounds(20 * seed, 30 * seed, 640, 480),
            null,
            0,
            WindowVisualState.Normal,
            new WindowConstraints(true, 320, 240, 1200, 900),
            modality,
            ownerId);
}