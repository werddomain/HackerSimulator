using System;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Windows;

namespace HackerSimulator.Wasm.Dialogs
{
    public abstract class Dialog<TResult> : WindowBase
    {
        private TaskCompletionSource<TResult?> _tcs = new();
        private ProcessBase? _parent;

        protected virtual TResult? DefaultResult => default;

        public Task<TResult?> ShowDialog(ProcessBase? parentProcess = null)
        {
            _parent = parentProcess;
            if (_parent is WindowBase pw)
            {
                pw.Lock();
                pw.OnClosed += ParentClosed;
            }

            OnClosed += DialogClosed;
            _ = Kernel.RunProcess(this, Array.Empty<string>());
            return _tcs.Task;
        }

        private void ParentClosed(object? sender, EventArgs e)
        {
            base.Close();
        }

        private void DialogClosed(object? sender, EventArgs e)
        {
            if (_parent is WindowBase pw)
            {
                pw.Unlock();
                pw.OnClosed -= ParentClosed;
            }
            if (!_tcs.Task.IsCompleted)
                _tcs.TrySetResult(DefaultResult);
        }

        protected void CloseDialog(TResult? result)
        {
            base.Close();
            _tcs.TrySetResult(result);
        }
    }
}
