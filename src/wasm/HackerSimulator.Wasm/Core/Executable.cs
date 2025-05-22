using System.IO;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    public abstract class Executable
    {
        public abstract string Name { get; }
        public abstract Task Execute(string[] args, Stream stdin, Stream stdout, Stream stderr);
    }
}
