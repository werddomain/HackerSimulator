using System;
using System.Collections.Generic;

namespace HackerSimulator.Wasm.Core
{
    public record WindowRecord(Guid Id, Type ComponentType, string Title);

    public class WindowManager
    {
        private readonly List<WindowRecord> _windows = new();
        public IReadOnlyList<WindowRecord> Windows => _windows;
        public event Action? WindowsChanged;

        public void OpenWindow(Type component, string title)
        {
            _windows.Add(new WindowRecord(Guid.NewGuid(), component, title));
            WindowsChanged?.Invoke();
        }

        public void CloseWindow(Guid id)
        {
            _windows.RemoveAll(w => w.Id == id);
            WindowsChanged?.Invoke();
        }
    }
}
