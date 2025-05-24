using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Commands.Linux
{
    public class PwdCommand : CommandBase
    {
        public PwdCommand(ShellService shell, KernelService kernel) : base("pwd", shell, kernel) { }

        public override string Description => "Print working directory";
        public override string Usage => "pwd";

        protected override Task ExecuteInternal(string[] args, CommandContext context)
        {
            if (context.Env.TryGetValue("PWD", out var cwd))
                context.Stdout.WriteLine(cwd);
            else
                context.Stdout.WriteLine("/");
            return Task.CompletedTask;
        }
    }
}
