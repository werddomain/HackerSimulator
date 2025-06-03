using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.Applications;
using HackerOs.OS.Shell.Commands;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands.Applications;

/// <summary>
/// Command to install applications from packages
/// </summary>
[Command("install", "Install an application from a package")]
public class InstallCommand : CommandBase
{
    private readonly IApplicationInstaller _appInstaller;
    private readonly IUserManager _userManager;

    public InstallCommand(IApplicationInstaller appInstaller, IUserManager userManager)
    {
        _appInstaller = appInstaller;
        _userManager = userManager;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Count < 1)
        {
            await WriteErrorAsync("Usage: install <packagePath> [--verbose]");
            await WriteErrorAsync("Example: install /home/user/downloads/myapp.hapkg");
            return 1;
        }

        string packagePath = context.Arguments[0];
        bool verbose = context.Arguments.Contains("--verbose");

        await WriteLineAsync($"Installing package from {packagePath}...");

        var userSession = await _userManager.GetSessionByUserIdAsync(context.CurrentSession.User.UserId);
        if (userSession == null)
        {
            await WriteErrorAsync("Error: Could not find valid user session");
            return 1;
        }

        var manifest = await _appInstaller.InstallApplicationAsync(packagePath, userSession);
        if (manifest == null)
        {
            await WriteErrorAsync($"Failed to install application from package: {packagePath}");
            return 1;
        }

        await WriteLineAsync($"Successfully installed: {manifest.Name} v{manifest.Version}");
        
        if (verbose)
        {
            await WriteLineAsync();
            await WriteLineAsync("Application Details:");
            await WriteLineAsync($"ID: {manifest.Id}");
            await WriteLineAsync($"Author: {manifest.Author}");
            await WriteLineAsync($"Description: {manifest.Description}");
            await WriteLineAsync($"Categories: {string.Join(", ", manifest.Categories)}");
            
            if (manifest.SupportedFileTypes.Count > 0)
            {
                await WriteLineAsync($"Supported file types: {string.Join(", ", manifest.SupportedFileTypes)}");
            }
        }

        return 0;
    }

    public override Task<IEnumerable<string>> GetCompletionCandidatesAsync(string[] args, int argPosition)
    {
        // If completing the first argument (package path), suggest package files
        if (argPosition == 0)
        {
            return Task.FromResult<IEnumerable<string>>(new[] { "*.hapkg", "*.zip" });
        }

        // Otherwise, suggest command flags
        if (argPosition > 0)
        {
            return Task.FromResult<IEnumerable<string>>(new[] { "--verbose" });
        }

        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }
}

/// <summary>
/// Command to uninstall applications
/// </summary>
[Command("uninstall", "Uninstall an application")]
public class UninstallCommand : CommandBase
{
    private readonly IApplicationInstaller _appInstaller;
    private readonly IApplicationManager _appManager;
    private readonly IUserManager _userManager;

    public UninstallCommand(
        IApplicationInstaller appInstaller, 
        IApplicationManager appManager,
        IUserManager userManager)
    {
        _appInstaller = appInstaller;
        _appManager = appManager;
        _userManager = userManager;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Count < 1)
        {
            await WriteErrorAsync("Usage: uninstall <applicationId> [--keep-data] [--force]");
            await WriteErrorAsync("Example: uninstall com.example.myapp");
            await WriteErrorAsync("Use 'list-apps' to see installed applications");
            return 1;
        }

        string applicationId = context.Arguments[0];
        bool keepData = context.Arguments.Contains("--keep-data");
        bool force = context.Arguments.Contains("--force");

        // Check if application exists
        var app = _appManager.GetApplication(applicationId);
        if (app == null)
        {
            await WriteErrorAsync($"Application not found: {applicationId}");
            return 1;
        }

        await WriteLineAsync($"Uninstalling application: {app.Name} (ID: {app.Id})");

        if (force)
        {
            await WriteLineAsync("Forcing uninstallation...");
        }

        var userSession = await _userManager.GetSessionByUserIdAsync(context.CurrentSession.User.UserId);
        if (userSession == null)
        {
            await WriteErrorAsync("Error: Could not find valid user session");
            return 1;
        }

        bool success = await _appInstaller.UninstallApplicationAsync(applicationId, userSession, keepData);
        if (!success)
        {
            await WriteErrorAsync($"Failed to uninstall application: {app.Name}");
            return 1;
        }

        await WriteLineAsync($"Successfully uninstalled: {app.Name}");
        
        if (keepData)
        {
            await WriteLineAsync("User data was preserved.");
        }
        else
        {
            await WriteLineAsync("User data was removed.");
        }

        return 0;
    }

    public override async Task<IEnumerable<string>> GetCompletionCandidatesAsync(string[] args, int argPosition)
    {
        // If completing the first argument (application ID), suggest installed apps
        if (argPosition == 0)
        {
            var apps = _appManager.GetAvailableApplications();
            return apps.Select(a => a.Id);
        }

        // Otherwise, suggest command flags
        if (argPosition > 0)
        {
            return new[] { "--keep-data", "--force" };
        }

        return Array.Empty<string>();
    }
}

/// <summary>
/// Command to list installed applications
/// </summary>
[Command("list-apps", "List installed applications")]
public class ListAppsCommand : CommandBase
{
    private readonly IApplicationManager _appManager;
    private readonly IApplicationFinder _appFinder;

    public ListAppsCommand(IApplicationManager appManager, IApplicationFinder appFinder)
    {
        _appManager = appManager;
        _appFinder = appFinder;
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        bool showSystem = true;
        bool showUser = true;
        string? category = null;
        string? filter = null;

        // Parse arguments
        foreach (var arg in context.Arguments)
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
            }
            else if (arg.StartsWith("--filter="))
            {
                filter = arg.Substring("--filter=".Length);
            }
            else if (arg == "--help" || arg == "-h")
            {
                await ShowHelpAsync();
                return 0;
            }
        }

        // Define query
        var query = new ApplicationQuery();
        
        if (!showSystem)
            query.ExcludeSystemApps();
            
        if (!showUser)
            query.ExcludeUserApps();
            
        if (category != null)
            query.WithCategory(category);
            
        if (filter != null)
            query.WithNameOrDescriptionContaining(filter);

        // Find applications
        var apps = _appFinder.FindApplications(query);

        if (apps.Count == 0)
        {
            await WriteLineAsync("No applications found matching the specified criteria.");
            return 0;
        }

        // Get running applications
        var runningApps = _appManager.GetRunningApplications();
        var runningAppIds = new HashSet<string>(runningApps.Select(a => a.Id));

        // Display applications
        await WriteLineAsync($"Found {apps.Count} application(s):\n");
        
        var systemApps = apps.Where(a => a.IsSystemApplication).ToList();
        var userApps = apps.Where(a => !a.IsSystemApplication).ToList();
        
        if (systemApps.Any() && showSystem)
        {
            await WriteLineAsync("System Applications:");
            await WriteLineAsync("--------------------");
            foreach (var app in systemApps)
            {
                string status = runningAppIds.Contains(app.Id) ? "[RUNNING]" : "";
                await WriteLineAsync($"{app.Name} (v{app.Version}) {status}");
                await WriteLineAsync($"  ID: {app.Id}");
                await WriteLineAsync($"  Type: {app.Type}");
                if (app.Categories.Count > 0)
                {
                    await WriteLineAsync($"  Categories: {string.Join(", ", app.Categories)}");
                }
                await WriteLineAsync();
            }
        }
        
        if (userApps.Any() && showUser)
        {
            await WriteLineAsync("User Applications:");
            await WriteLineAsync("------------------");
            foreach (var app in userApps)
            {
                string status = runningAppIds.Contains(app.Id) ? "[RUNNING]" : "";
                await WriteLineAsync($"{app.Name} (v{app.Version}) {status}");
                await WriteLineAsync($"  ID: {app.Id}");
                await WriteLineAsync($"  Author: {app.Author}");
                if (app.Categories.Count > 0)
                {
                    await WriteLineAsync($"  Categories: {string.Join(", ", app.Categories)}");
                }
                await WriteLineAsync();
            }
        }

        return 0;
    }
    
    private async Task ShowHelpAsync()
    {
        await WriteLineAsync("Usage: list-apps [options]");
        await WriteLineAsync("");
        await WriteLineAsync("Options:");
        await WriteLineAsync("  --no-system     Don't show system applications");
        await WriteLineAsync("  --no-user       Don't show user applications");
        await WriteLineAsync("  --category=X    Only show applications in category X");
        await WriteLineAsync("  --filter=X      Only show applications with X in name or description");
        await WriteLineAsync("  --help, -h      Show this help message");
    }

    public override Task<IEnumerable<string>> GetCompletionCandidatesAsync(string[] args, int argPosition)
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
