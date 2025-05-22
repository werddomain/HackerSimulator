using System.Collections.Generic;
using System.IO;

namespace HackerSimulator.Wasm.Commands
{
    public class CommandContext
    {
        public TextReader Stdin { get; set; } = TextReader.Null;
        public TextWriter Stdout { get; set; } = TextWriter.Null;
        public TextWriter Stderr { get; set; } = TextWriter.Null;
        public Dictionary<string, string> Env { get; set; } = new();
    }
}
