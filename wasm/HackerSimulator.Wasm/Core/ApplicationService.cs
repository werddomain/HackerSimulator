using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Discovers window based applications and exposes basic metadata
    /// like command name, display name and icon.
    /// </summary>
    public class ApplicationService
    {
        public record AppInfo(string Command, string Name, string Icon);

        private readonly List<AppInfo> _apps = new();

        public ApplicationService()
        {
            DiscoverApplications();
        }

        private void DiscoverApplications()
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var type in asm.GetTypes())
            {
                if (type.IsAbstract) continue;
                if (!typeof(Windows.WindowBase).IsAssignableFrom(type)) continue;

                var cmd = type.Name.ToLowerInvariant();
                var name = ToDisplayName(type.Name);
                var iconAttr = type.GetCustomAttribute<AppIconAttribute>();
                var icon = iconAttr?.Icon ?? "fa:window-maximize";

                _apps.Add(new AppInfo(cmd, name, icon));
            }

            _apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static string ToDisplayName(string typeName)
        {
            if (typeName.EndsWith("App"))
                typeName = typeName.Substring(0, typeName.Length - 3);
            var name = System.Text.RegularExpressions.Regex.Replace(typeName, "([a-z])([A-Z])", "$1 $2");
            return name;
        }

        public IReadOnlyList<AppInfo> GetApps() => _apps;

        public AppInfo? GetApp(string command) => _apps.FirstOrDefault(a => a.Command == command);
    }
}
