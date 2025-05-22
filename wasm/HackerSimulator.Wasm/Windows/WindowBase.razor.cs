using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Windows
{
    public partial class WindowBase : IDisposable
    {
        [Inject] private WindowManagerService Manager { get; set; } = default!;

        public WindowBase() : base("window")
        {
        }

        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public string Title { get; set; } = "Window";
        [Parameter] public string? Icon { get; set; }
        [Parameter] public bool Resizable { get; set; } = true;

        public bool Visible { get; set; } = true;

        public double X { get; set; } = 100;
        public double Y { get; set; } = 100;
        public double Width { get; set; } = 400;
        public double Height { get; set; } = 300;

        public WindowState State { get; private set; } = WindowState.Normal;

        internal bool IsActive { get; private set; }

        private bool _dragging;
        private double _startX;
        private double _startY;
        private double _startLeft;
        private double _startTop;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Manager.Register(this);
        }

        public void Activate()
        {
            Manager.Activate(this);
        }

        internal void InvokeActivated(bool active)
        {
            IsActive = active;
            StateHasChanged();
        }

        private void HandleClick()
        {
            if (!IsActive)
                Activate();
        }

        private void StartDrag(MouseEventArgs e)
        {
            _dragging = true;
            _startX = e.ClientX;
            _startY = e.ClientY;
            _startLeft = X;
            _startTop = Y;
            Activate();
        }

        private void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            X = _startLeft + (e.ClientX - _startX);
            Y = _startTop + (e.ClientY - _startY);
        }

        private void StopDrag()
        {
            _dragging = false;
        }

        private void ToggleMaximize()
        {
            if (State == WindowState.Maximized)
                Restore();
            else
                Maximize();
        }

        public void Maximize()
        {
            State = WindowState.Maximized;
            X = 0;
            Y = 0;
            Width = 10000; // full width (handled via style)
            Height = 10000;
        }

        public void Restore()
        {
            State = WindowState.Normal;
            Width = 400;
            Height = 300;
        }

        public void Minimize()
        {
            State = WindowState.Minimized;
            Visible = false;
        }

        public void Close()
        {
            var args = new CancelEventArgs();
            OnClose?.Invoke(this, args);
            if (args.Cancel)
                return;
            OnClosed?.Invoke(this, EventArgs.Empty);
            Visible = false;
            Manager.Unregister(this);
        }

        public event EventHandler<CancelEventArgs>? OnClose;
        public event EventHandler? OnClosed;

        string Style => $"left:{X}px;top:{Y}px;width:{Width}px;height:{Height}px;" +
                         (Resizable ? "resize:both;overflow:hidden;" : "") +
                         (Visible ? "" : "display:none;");

        public void Dispose()
        {
            Manager.Unregister(this);
        }

        protected override Task RunAsync(string[] args, CancellationToken token)
        {
            // Windows have no background execution logic by default
            return Task.CompletedTask;
        }
    }
}
