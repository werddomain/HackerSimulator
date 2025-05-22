using System;
using System.Threading;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands
{
    /// <summary>
    /// Base class for commands that can also run as standalone processes.
    /// When started outside of a TerminalProcess, they will run inside a new
    /// terminal instance.
    /// </summary>
    public abstract class Executable : ProcessBase, ICommandModule
    {
        private readonly ShellService _shell;

        protected Executable(string name, ShellService shell) : base(name)
        {
            _shell = shell;
        }

        public abstract string Description { get; }
        public abstract string Usage { get; }

        public ShellService Shell => _shell;

        /// <summary>
        /// Execute as a shell command.
        /// </summary>
        public async Task<int> Execute(string[] args, CommandContext context)
        {
            await ExecuteInternal(args, context);
            return 0;
        }

        protected abstract Task ExecuteInternal(string[] args, CommandContext context);

        /// <summary>
        /// Run as a standalone process.
        /// </summary>
        protected override async Task RunAsync(string[] args, CancellationToken token)
        {
            var context = new CommandContext
            {
                Stdin = Console.In,
                Stdout = Console.Out,
                Stderr = Console.Error
            };

            await ExecuteInternal(args, context);
        }
    }
}
