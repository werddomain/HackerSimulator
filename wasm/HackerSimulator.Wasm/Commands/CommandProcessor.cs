using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Commands
{
    public class CommandProcessor
    {
        private readonly Dictionary<string, ICommandModule> _commands = new();

        public void RegisterCommand(ICommandModule command)
        {
            _commands[command.Name.ToLowerInvariant()] = command;
        }

        public async Task<int> Execute(string commandLine, CommandContext context)
        {
            commandLine = commandLine.Trim();
            if (string.IsNullOrEmpty(commandLine))
                return 0;

            if (commandLine.Contains("|"))
                return await ExecutePipeline(commandLine, context);

            var parts = SplitArgs(commandLine);
            var name = parts.First();
            var args = parts.Skip(1).ToArray();

            if (!_commands.TryGetValue(name.ToLowerInvariant(), out var command))
            {
                context.Stderr.WriteLine($"Command '{name}' not found.");
                return 1;
            }

            return await command.Execute(args, context);
        }

        private async Task<int> ExecutePipeline(string commandLine, CommandContext context)
        {
            var segments = commandLine.Split('|');
            string? input = null;
            int exitCode = 0;

            foreach (var segment in segments.Select(s => s.Trim()))
            {
                using var output = new StringWriter();
                var localContext = new CommandContext
                {
                    Stdin = input != null ? new StringReader(input) : context.Stdin,
                    Stdout = output,
                    Stderr = context.Stderr,
                    Env = context.Env
                };

                exitCode = await Execute(segment, localContext);
                if (exitCode != 0)
                    break;

                output.Flush();
                input = output.ToString();
            }

            if (input != null)
                context.Stdout.Write(input);

            return exitCode;
        }

        private static IEnumerable<string> SplitArgs(string commandLine)
        {
            var inQuotes = false;
            var current = string.Empty;
            foreach (var ch in commandLine)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        yield return current;
                        current = string.Empty;
                    }
                }
                else
                {
                    current += ch;
                }
            }
            if (current.Length > 0)
                yield return current;
        }
    }
}
