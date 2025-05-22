using System.Collections.Generic;

namespace HackerSimulator.Wasm.Windows
{
    /// <summary>
    /// Simple service that keeps track of open windows and activation state.
    /// </summary>
    public class WindowManagerService
    {
        private readonly List<WindowBase> _windows = new();
        private WindowBase? _active;

        public IReadOnlyList<WindowBase> Windows => _windows;

        internal void Register(WindowBase window)
        {
            if (!_windows.Contains(window))
                _windows.Add(window);

            Activate(window);
        }

        internal void Unregister(WindowBase window)
        {
            _windows.Remove(window);
            if (_active == window)
                _active = null;
        }

        public void Activate(WindowBase window)
        {
            if (_active == window)
                return;

            _active?.InvokeActivated(false);
            _active = window;
            _active.InvokeActivated(true);
        }
    }
}
