using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace HackerSimulator.Wasm.Core
{
    public class ShellService
    {
        private readonly IServiceProvider _provider;
        private readonly KernelService _kernel;
        private readonly Dictionary<string, Type> _processes = new();

        public ShellService(IServiceProvider provider, KernelService kernel)
        {
            _provider = provider;
            _kernel = kernel;
            DiscoverProcesses();
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

        public async Task Run(string name, string[] args)
        {
            if (!_processes.TryGetValue(name.ToLowerInvariant(), out var type))
            {
                Console.WriteLine($"Process '{name}' not found.");
                return;
            }

            var process = (ProcessBase)ActivatorUtilities.CreateInstance(_provider, type);
            await _kernel.RunProcess(process, args);
        }
    }
}
