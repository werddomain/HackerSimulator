using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace HackerSimulator.Wasm.Core
{
    public class ShellService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _processes = new();

        public ShellService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            RegisterProcessesFromAssembly(Assembly.GetExecutingAssembly());
        }

        private void RegisterProcessesFromAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => typeof(Process).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<ProcessAttribute>();
                if (attr != null)
                {
                    _processes[attr.Name.ToLowerInvariant()] = type;
                }
            }
        }

        public async Task Run(string name, string[] args)
        {
            if (_processes.TryGetValue(name.ToLowerInvariant(), out var type))
            {
                using var scope = _serviceProvider.CreateScope();
                var process = (Process)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
                await process.Execute(args, Stream.Null, Stream.Null, Stream.Null);
            }
        }
    }
}
