using System.IO;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    public abstract class Process : Executable
    {
        protected virtual Task Run(string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            return Task.CompletedTask;
        }

        public override Task Execute(string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            return Run(args, stdin, stdout, stderr);
        }
    }
}
