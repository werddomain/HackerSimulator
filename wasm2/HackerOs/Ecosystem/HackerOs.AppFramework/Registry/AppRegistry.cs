using System.Collections.Concurrent;
using System.Reflection;
using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using HackerOs.AppFramework.Abstractions;

namespace HackerOs.AppFramework.Registry;

/// <summary>
/// The heart of the developer ecosystem: discovers every component decorated with
/// <see cref="AppAttribute"/> across the registered assemblies and exposes them as
/// launchable <see cref="AppDescriptor"/> entries.
/// </summary>
/// <remarks>
/// The registry is intentionally module oriented. A host registers one or more
/// assemblies (typically <c>typeof(Program).Assembly</c> and any module libraries)
/// and every qualifying component becomes available with zero additional wiring.
/// </remarks>
public sealed class AppRegistry
{
    private readonly WindowManagerService _windowManager;
    private readonly ConcurrentDictionary<string, AppDescriptor> _apps = new();
    private static readonly MethodInfo CreateWindowGeneric =
        typeof(WindowManagerService).GetMethod(nameof(WindowManagerService.CreateWindow))
        ?? throw new InvalidOperationException(
            "WindowManagerService.CreateWindow<T> could not be located via reflection.");

    /// <summary>
    /// Creates a new registry bound to the window manager used to launch apps.
    /// </summary>
    public AppRegistry(WindowManagerService windowManager)
    {
        _windowManager = windowManager;
    }

    /// <summary>Raised whenever the set of registered applications changes.</summary>
    public event EventHandler? AppsChanged;

    /// <summary>All discovered applications, ordered for display.</summary>
    public IReadOnlyList<AppDescriptor> Apps =>
        _apps.Values
            .OrderBy(a => a.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.SortOrder)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Applications that should be shown in the launcher UI.</summary>
    public IReadOnlyList<AppDescriptor> LauncherApps =>
        Apps.Where(a => !a.HiddenFromLauncher).ToList();

    /// <summary>The distinct categories present in the launcher, in display order.</summary>
    public IReadOnlyList<string> Categories =>
        LauncherApps.Select(a => a.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Scans the supplied assemblies for components decorated with
    /// <see cref="AppAttribute"/> and registers each one.
    /// </summary>
    /// <returns>The number of newly registered applications.</returns>
    public int DiscoverFrom(params Assembly[] assemblies)
    {
        var added = 0;
        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                var descriptor = AppDescriptor.TryCreate(type);
                if (descriptor is not null && _apps.TryAdd(descriptor.Id, descriptor))
                {
                    added++;
                }
            }
        }

        if (added > 0)
        {
            AppsChanged?.Invoke(this, EventArgs.Empty);
        }

        return added;
    }

    /// <summary>Registers a single application type explicitly.</summary>
    public bool Register<TApp>() => Register(typeof(TApp));

    /// <summary>Registers a single application type explicitly.</summary>
    public bool Register(Type appType)
    {
        var descriptor = AppDescriptor.TryCreate(appType)
            ?? throw new InvalidOperationException(
                $"Type '{appType.FullName}' is not a valid application.");

        if (_apps.TryAdd(descriptor.Id, descriptor))
        {
            AppsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    /// <summary>Looks up an application by its identifier.</summary>
    public AppDescriptor? Find(string id) =>
        _apps.TryGetValue(id, out var descriptor) ? descriptor : null;

    /// <summary>
    /// Launches an application by descriptor, creating a new window on the desktop
    /// and, by extension, a new entry on the taskbar.
    /// </summary>
    public WindowInfo Launch(AppDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var parameters = new Dictionary<string, object>
        {
            ["Title"] = descriptor.Name,
            ["Name"] = descriptor.Id
        };

        var typedMethod = CreateWindowGeneric.MakeGenericMethod(descriptor.ComponentType);
        var result = typedMethod.Invoke(_windowManager, new object?[] { parameters });
        return (WindowInfo)result!;
    }

    /// <summary>Launches an application by identifier.</summary>
    public WindowInfo Launch(string id)
    {
        var descriptor = Find(id)
            ?? throw new KeyNotFoundException($"No application registered with id '{id}'.");
        return Launch(descriptor);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Be resilient to partially loadable module assemblies.
            return ex.Types.Where(t => t is not null)!.Cast<Type>();
        }
    }
}
