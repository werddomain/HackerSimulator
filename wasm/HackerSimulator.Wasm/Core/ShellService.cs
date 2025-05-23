using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using HackerSimulator.Wasm.Commands;

namespace HackerSimulator.Wasm.Core
{
    public class ShellService
    {
        private readonly IServiceProvider _provider;
        private readonly KernelService _kernel;
        private readonly Dictionary<string, Type> _processes = new();
        private readonly CommandProcessor _processor = new();

        public T CreateObject<T>(ProcessBase? scope = null)
        {
            // scope parameter reserved for future use
            return ActivatorUtilities.CreateInstance<T>(_provider);
        }

        public ShellService(IServiceProvider provider, KernelService kernel)
        {
            _provider = provider;
            _kernel = kernel;
            DiscoverProcesses();
            RegisterBuiltInCommands();
            DiscoverCommands();
        }

        private void DiscoverProcesses()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(ProcessBase).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    _processes[type.Name.ToLowerInvariant()] = type;
                }
            }
        }

        private void RegisterBuiltInCommands()
        {
            RegisterCommand(new EchoCommand(this, _kernel));
            RegisterCommand(new UpperCommand(this, _kernel));
        }

        private void DiscoverCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(ICommandModule).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    if (ActivatorUtilities.CreateInstance(_provider, type) is ICommandModule cmd)
                    {
                        RegisterCommand(cmd);
                    }
                }
            }
        }

        public void RegisterCommand(ICommandModule command)
        {
            _processor.RegisterCommand(command);
        }

        public Task<int> ExecuteCommand(string commandLine, CommandContext context)
        {
            return _processor.Execute(commandLine, context);
        }

        public async Task Run(string name, string[] args, ProcessBase? sender = null)
        {
            if (!_processes.TryGetValue(name.ToLowerInvariant(), out var type))
            {
                Console.WriteLine($"Process '{name}' not found.");
                return;
            }

            // If the target process is a command and sender isn't a terminal,
            // launch it inside a new terminal instance.
            if (typeof(CommandBase).IsAssignableFrom(type) && sender is not Processes.TerminalProcess)
            {
                var terminalArgs = new string[] { name }.Concat(args).ToArray();
                await Run("terminal", terminalArgs, sender);
                return;
            }

            var process = (ProcessBase)ActivatorUtilities.CreateInstance(_provider, type);
            await process.StartAsync(args);
        }

        public IEnumerable<ICommandModule> GetCommands() => _processor.GetCommands();
        public ICommandModule? GetCommand(string name) => _processor.GetCommand(name);
    }
}
