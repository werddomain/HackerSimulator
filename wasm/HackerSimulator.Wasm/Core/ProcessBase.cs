using System;
using System.Threading;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    public abstract class ProcessBase
    {
        protected ProcessBase(string name) => Name = name;

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; }
        public ProcessState State { get; private set; } = ProcessState.Created;

        public async Task StartAsync(string[] args, CancellationToken token = default)
        {
            State = ProcessState.Running;
            await RunAsync(args, token);
            State = ProcessState.Exited;
        }

        protected abstract Task RunAsync(string[] args, CancellationToken token);
    }

    public enum ProcessState
    {
        Created,
        Running,
        Exited
    }
}
