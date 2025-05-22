using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace HackerSimulator.Wasm.Core
{
    public abstract class ProcessBase : ComponentBase
    {
        protected ProcessBase(string name) => Name = name;

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; }
        public ProcessState State { get; private set; } = ProcessState.Created;

        private Task? _runningTask;

        public Task StartAsync(string[] args, CancellationToken token = default)
        {
            State = ProcessState.Running;
            _runningTask = Task.Run(async () =>
            {
                try
                {
                    await RunAsync(args, token);
                }
                finally
                {
                    State = ProcessState.Exited;
                }
            }, token);

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
