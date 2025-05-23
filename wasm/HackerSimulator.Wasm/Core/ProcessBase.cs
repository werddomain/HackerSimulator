using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace HackerSimulator.Wasm.Core
{
    public abstract class ProcessBase : ComponentBase, IExecutable
    {
        protected ProcessBase(string name) => Name = name;
        protected ProcessBase(string name, KernelService kernel) : this(name)
        {
            Kernel = kernel;
        }

        [Inject]
        protected KernelService Kernel { get; set; } = default!;

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; }
        public ProcessState State { get; private set; } = ProcessState.Created;

        private Task? _runningTask;

        public Task StartAsync(string[] args, CancellationToken token = default)
        {
            if (Kernel is null)
                throw new InvalidOperationException("Kernel service not configured.");

            State = ProcessState.Running;

            var info = Kernel.RegisterProcess(this);
            var cts = info.CancellationTokenSource;
            if (token != default)
                cts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);

            _runningTask = Task.Run(async () =>
            {
                try
                {
                    await RunAsync(args, cts.Token);
                }
                finally
                {
                    State = ProcessState.Exited;
                    Kernel.UnregisterProcess(Id);
                }
            }, cts.Token);

            info.AttachTask(_runningTask);
            return _runningTask;
        }

        public Task? RunningTask => _runningTask;

        protected abstract Task RunAsync(string[] args, CancellationToken token);
    }

    public enum ProcessState
    {
        Created,
        Running,
        Exited
    }
}
