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

        /// <summary>
        /// Raised when a window is opened and registered with the manager.
        /// </summary>
        public event Action<WindowBase>? WindowOpened;

        /// <summary>
        /// Raised when a window is closed and unregistered from the manager.
        /// </summary>
        public event Action<WindowBase>? WindowClosed;

        /// <summary>
        /// Raised whenever the active window changes. Emits <c>null</c> when no
        /// window is active.
        /// </summary>
        public event Action<WindowBase?>? ActiveWindowChanged;

        public IReadOnlyList<WindowBase> Windows => _windows;

        internal void Register(WindowBase window)
        {
            if (!_windows.Contains(window))
            {
                _windows.Add(window);
                WindowOpened?.Invoke(window);
            }

            Activate(window);
        }

        internal void Unregister(WindowBase window)
        {
            _windows.Remove(window);
            if (_active == window)
            {
                _active = null;
                ActiveWindowChanged?.Invoke(null);
            }

            WindowClosed?.Invoke(window);
        }

        public void Activate(WindowBase window)
        {
            if (_active == window)
                return;

            _active?.InvokeActivated(false);
            _active = window;
            _active.InvokeActivated(true);
            ActiveWindowChanged?.Invoke(_active);
        }
    }
}
