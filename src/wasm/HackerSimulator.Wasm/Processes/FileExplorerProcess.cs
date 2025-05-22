using System.IO;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Processes
{
    [Process("explorer")]
    public class FileExplorerProcess : Process
    {
        private readonly WindowManager _windows;

        public FileExplorerProcess(WindowManager windows)
        {
            _windows = windows;
        }

        public override string Name => "explorer";

        protected override Task Run(string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            _windows.OpenWindow(typeof(Pages.FileExplorer), "File Explorer");
            return Task.CompletedTask;
        }
    }
}
