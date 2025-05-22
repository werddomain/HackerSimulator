using System.IO;
using System.Threading.Tasks;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Processes
{
    [Process("calculator")]
    public class CalculatorProcess : Process
    {
        private readonly WindowManager _windows;

        public CalculatorProcess(WindowManager windows)
        {
            _windows = windows;
        }

        public override string Name => "calculator";

        protected override Task Run(string[] args, Stream stdin, Stream stdout, Stream stderr)
        {
            _windows.OpenWindow(typeof(Pages.Calculator), "Calculator");
            return Task.CompletedTask;
        }
    }
}
