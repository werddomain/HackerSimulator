using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using HackerOs.OS.Applications;
using HackerOs.OS.Shell;
using HackerOs.OS.User;
using HackerOs.OS.HSystem.Text;

namespace HackerOs.OS.Shell.Commands.Applications;

/// <summary>
/// Command to install applications from packages
/// </summary>
public class InstallCommand : CommandBase
{
    private readonly IApplicationInstaller _appInstaller;

    public override string Name => "install";
    public override string Description => "Install an application from a package";
    public override string Usage => "install <packagePath> [--verbose]";

    public InstallCommand(IApplicationInstaller appInstaller, IUserManager userManager)
    {
        _appInstaller = appInstaller;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {        if (args.Length < 1)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Usage: install <packagePath> [--verbose]\n"));
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Example: install /home/user/downloads/myapp.hapkg\n"));
            return 1;
        }
        string packagePath = args[0];
        bool verbose = args.Contains("--verbose");

        await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Installing package from {packagePath}...\n"));
        var userSession = context.UserSession;
        if (userSession == null)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Error: Could not find valid user session\n"));
            return 1;
        }
        var manifest = await _appInstaller.InstallApplicationAsync(packagePath, userSession);
        if (manifest == null)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes($"Failed to install application from package: {packagePath}\n"));
            return 1;
        }

        await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Successfully installed: {manifest.Name} v{manifest.Version}\n"));
          if (verbose)
        {
            await stdout.WriteAsync(Encoding.UTF8.GetBytes("\n"));
            await stdout.WriteAsync(Encoding.UTF8.GetBytes("Application Details:\n"));
            await stdout.WriteAsync(Encoding.UTF8.GetBytes($"ID: {manifest.Id}\n"));
            await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Author: {manifest.Author}\n"));
            await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Description: {manifest.Description}\n"));
            await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Categories: {string.Join(", ", manifest.Categories)}\n"));
            
            if (manifest.SupportedFileTypes.Count > 0)
            {
                await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Supported file types: {string.Join(", ", manifest.SupportedFileTypes)}\n"));            }
        }

        return 0;
    }

    public override async Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // If completing the first argument (package path), suggest package files
        if (args.Length == 0)
        {
            return new[] { "*.hapkg", "*.zip" };
        }

        // Otherwise, suggest command flags
        if (args.Length > 0)
        {
            return new[] { "--verbose" };
        }

        return Array.Empty<string>();
    }
}

/// <summary>
/// Command to uninstall applications
/// </summary>
public class UninstallCommand : CommandBase
{
    private readonly IApplicationInstaller _appInstaller;
    private readonly IApplicationManager _appManager;

    public override string Name => "uninstall";
    public override string Description => "Uninstall an application";
    public override string Usage => "uninstall <applicationName> [--force]";

    public UninstallCommand(
        IApplicationInstaller appInstaller, 
        IApplicationManager appManager,
        IUserManager userManager)
    {
        _appInstaller = appInstaller;
        _appManager = appManager;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        if (args.Length < 1)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Usage: uninstall <applicationId> [--keep-data] [--force]\n"));
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Example: uninstall com.example.myapp\n"));
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Use 'list-apps' to see installed applications\n"));
            return 1;
    }
        string applicationId = args[0];
        bool keepData = args.Contains("--keep-data");
        bool force = args.Contains("--force");

        // Check if application exists
        var app = _appManager.GetApplication(applicationId);
        if (app == null)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes($"Application not found: {applicationId}\n"));
            return 1;
        }

        await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Uninstalling application: {app.Name} (ID: {app.Id})\n"));

        if (force)
        {
            await stdout.WriteAsync(Encoding.UTF8.GetBytes("Forcing uninstallation...\n"));
        }
        var userSession = context.UserSession;
        if (userSession == null)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes("Error: Could not find valid user session\n"));
            return 1;
        }

        bool success = await _appInstaller.UninstallApplicationAsync(applicationId, userSession, keepData);
        if (!success)
        {
            await stderr.WriteAsync(Encoding.UTF8.GetBytes($"Failed to uninstall application: {app.Name}\n"));
            return 1;
        }

        await stdout.WriteAsync(Encoding.UTF8.GetBytes($"Successfully uninstalled: {app.Name}\n"));
        
        if (keepData)
        {
            await stdout.WriteAsync(Encoding.UTF8.GetBytes("User data was preserved.\n"));
        }
        else
        {
            await stdout.WriteAsync(Encoding.UTF8.GetBytes("User data was removed.\n"));
        }

        return 0;
    }

    public override async Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // If completing the first argument (application ID), suggest installed apps
        if (args.Length == 0)
        {
            var apps = _appManager.GetAvailableApplications();
            return apps.Select(a => a.Id);
        }        // Otherwise, suggest command flags
        if (args.Length > 0)
        {
            return new[] { "--keep-data", "--force" };
        }

        return Array.Empty<string>();
    }
}

/// <summary>
/// Command to list installed applications
/// </summary>
public class ListAppsCommand : CommandBase
{
    private readonly IApplicationManager _appManager;
    private readonly IApplicationFinder _appFinder;

    public override string Name => "list-apps";
    public override string Description => "List installed applications";
    public override string Usage => "list-apps [--system] [--user] [--all]";

    public ListAppsCommand(IApplicationManager appManager, IApplicationFinder appFinder)
    {
        _appManager = appManager;
        _appFinder = appFinder;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        bool showSystem = true;
        bool showUser = true;
        string? category = null;
        string? filter = null;

        // Parse arguments
        foreach (var arg in args)
        {
            if (arg == "--no-system")
            {
                showSystem = false;
            }
            else if (arg == "--no-user")
            {
                showUser = false;
            }
            else if (arg.StartsWith("--category="))
            {
                category = arg.Substring("--category=".Length);
            }            else if (arg.StartsWith("--filter="))
            {
                filter = arg.Substring("--filter=".Length);
            }            else if (arg == "--help" || arg == "-h")
            {
                await ShowHelpAsync(stdout, cancellationToken);
                return 0;
            }
        }

        // Get all applications
        var allApps = _appManager.GetAvailableApplications();
        var apps = allApps.ToList();

        // Apply filters
        if (!showSystem)
        {
            apps = apps.Where(a => !a.IsSystemApplication).ToList();
        }
        
        if (!showUser)
        {
            apps = apps.Where(a => a.IsSystemApplication).ToList();
        }
        
        if (category != null)
        {
            apps = apps.Where(a => a.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        
        if (filter != null)
        {
            apps = apps.Where(a => 
                a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }if (apps.Count == 0)
        {
            await WriteLineAsync(stdout, "No applications found matching the specified criteria.");
            return 0;
        }

        // Get running applications
        var runningApps = _appManager.GetRunningApplications();
        var runningAppIds = new HashSet<string>(runningApps.Select(a => a.Id));        // Display applications
        await WriteLineAsync(stdout, $"Found {apps.Count} application(s):\n");
          var systemApps = apps.Where(a => a.IsSystemApplication).ToList();
        var userApps = apps.Where(a => !a.IsSystemApplication).ToList();
        
        if (systemApps.Any() && showSystem)        {
            await WriteLineAsync(stdout, "System Applications:");
            await WriteLineAsync(stdout, "--------------------");
            foreach (var app in systemApps)
            {
                string status = runningAppIds.Contains(app.Id) ? "[RUNNING]" : "";
                await WriteLineAsync(stdout, $"{app.Name} (v{app.Version}) {status}");
                await WriteLineAsync(stdout, $"  ID: {app.Id}");
                await WriteLineAsync(stdout, $"  Type: {app.Type}");
                if (app.Categories.Count > 0)
                {
                    await WriteLineAsync(stdout, $"  Categories: {string.Join(", ", app.Categories)}");
                }
                await WriteLineAsync(stdout, "");
            }
        }
          if (userApps.Any() && showUser)
        {
            await WriteLineAsync(stdout, "User Applications:");
            await WriteLineAsync(stdout, "------------------");
            foreach (var app in userApps)
            {
                string status = runningAppIds.Contains(app.Id) ? "[RUNNING]" : "";
                await WriteLineAsync(stdout, $"{app.Name} (v{app.Version}) {status}");
                await WriteLineAsync(stdout, $"  ID: {app.Id}");
                await WriteLineAsync(stdout, $"  Author: {app.Author}");
                if (app.Categories.Count > 0)
                {
                    await WriteLineAsync(stdout, $"  Categories: {string.Join(", ", app.Categories)}");
                }
                await WriteLineAsync(stdout, "");
            }
        }

        return 0;
    }
      private async Task ShowHelpAsync(Stream stdout, CancellationToken cancellationToken)
    {
        await WriteLineAsync(stdout, "Usage: list-apps [options]");
        await WriteLineAsync(stdout, "");
        await WriteLineAsync(stdout, "Options:");
        await WriteLineAsync(stdout, "  --no-system     Don't show system applications");
        await WriteLineAsync(stdout, "  --no-user       Don't show user applications");
        await WriteLineAsync(stdout, "  --category=X    Only show applications in category X");
        await WriteLineAsync(stdout, "  --filter=X      Only show applications with X in name or description");
        await WriteLineAsync(stdout, "  --help, -h      Show this help message");
    }

    public override Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        var options = new List<string>
        {
            "--no-system",
            "--no-user",
            "--category=",
            "--filter=",
            "--help",
            "-h"
        };
        
        // Add categories for completion
        var categories = new HashSet<string>();
        var apps = _appManager.GetAvailableApplications();
        
        foreach (var app in apps)
        {
            foreach (var category in app.Categories)
            {
                categories.Add($"--category={category}");
            }
        }
        
        options.AddRange(categories);
        
        return Task.FromResult<IEnumerable<string>>(options);
    }
}
