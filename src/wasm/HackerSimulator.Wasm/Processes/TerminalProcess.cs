using System.IO;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Processes
{
    [Process("terminal")]
    public class TerminalProcess : Process
    {
        private readonly WindowManager _windows;

        public TerminalProcess(WindowManager windows)
        {
            _windows = windows;
        }

        public override string Name => "terminal";

        protected override Task Run(string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            _windows.OpenWindow(typeof(Pages.Terminal), "Terminal");
            return Task.CompletedTask;
        }
    }
}
