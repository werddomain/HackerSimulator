using System;
using System.Threading;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Commands;

namespace HackerSimulator.Wasm.Processes
{
    public class TerminalProcess : ProcessBase
    {
        private readonly ShellService _shell;

        public TerminalProcess(ShellService shell) : base("terminal")
        {
            _shell = shell;
        }

        protected override async Task RunAsync(string[] args, CancellationToken token)
        {

            Console.WriteLine("Terminal started");

            if (args.Length > 0)
            {
                var line = string.Join(" ", args);
                var ctx = new CommandContext
                {
                    Stdin = Console.In,
                    Stdout = Console.Out,
                    Stderr = Console.Error
                };

                await _shell.ExecuteCommand(line, ctx);
                return;
            }

            while (!token.IsCancellationRequested)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                var context = new CommandContext
                {
                    Stdin = Console.In,
                    Stdout = Console.Out,
                    Stderr = Console.Error
                };

                await _shell.ExecuteCommand(line, context);
            }

        }
    }
}
