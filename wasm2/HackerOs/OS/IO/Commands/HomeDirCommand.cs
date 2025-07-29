using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO.Commands
{
    /// <summary>
    /// Command-line utility for managing home directories
    /// </summary>
    public class HomeDirCommand
    {
        private readonly HomeDirectoryService _homeDirectoryService;
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger _logger;
        
        /// <summary>
        /// Initializes a new instance of the HomeDirCommand
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="logger">The logger</param>
        public HomeDirCommand(IVirtualFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _homeDirectoryService = new HomeDirectoryService(fileSystem, logger as ILogger<HomeDirectoryService>);
        }
        
        /// <summary>
        /// Executes the homedir command
        /// </summary>
        /// <param name="args">Command arguments</param>
        /// <param name="currentUser">The current user</param>
        /// <returns>The command result</returns>
        public async Task<string> ExecuteAsync(string[] args, User currentUser)
        {
            if (args.Length == 0)
            {
                return GetHelpText();
            }
            
            // Initialize the service
            await _homeDirectoryService.InitializeAsync();
            
            string subCommand = args[0].ToLower();
            
            switch (subCommand)
            {
                case "create":
                    return await HandleCreateCommand(args, currentUser);
                
                case "reset":
                    return await HandleResetCommand(args, currentUser);
                
                case "migrate":
                    return await HandleMigrateCommand(args, currentUser);
                
                case "quota":
                    return await HandleQuotaCommand(args, currentUser);
                
                case "umask":
                    return await HandleUmaskCommand(args, currentUser);
                
                case "size":
                    return await HandleSizeCommand(args, currentUser);
                
                case "templates":
                    return await HandleTemplatesCommand(args, currentUser);
                
                case "perms":
                case "permissions":
                    return await HandlePermissionsCommand(args, currentUser);
                
                case "help":
                default:
                    return GetHelpText();
            }
        }
        
        /// <summary>
        /// Handles the 'create' subcommand
        /// </summary>
        private async Task<string> HandleCreateCommand(string[] args, User currentUser)
        {
            // Check if user is admin
            if (!currentUser.IsAdmin)
            {
                return "Error: Only administrators can create home directories.";
            }
            
            if (args.Length < 2)
            {
                return "Usage: homedir create <username> [template]";
            }
            
            string username = args[1];
            string templateName = args.Length >= 3 ? args[2] : null;
            
            // Get the user
            var userManager = UserManagerFactory.CreateEnhanced(_fileSystem, _logger);
            var user = await userManager.GetUserAsync(username);
            
            if (user == null)
            {
                return $"Error: User '{username}' does not exist.";
            }
            
            // Create home directory
            bool success = await userManager.HomeDirectoryService.CreateHomeDirectoryAsync(user, templateName);
            
            if (success)
            {
                return $"Home directory created successfully for user '{username}'.";
            }
            else
            {
                return $"Error: Failed to create home directory for user '{username}'.";
            }
        }
        
        /// <summary>
        /// Handles the 'reset' subcommand
        /// </summary>
        private async Task<string> HandleResetCommand(string[] args, User currentUser)
        {
            if (args.Length < 2)
            {
                return "Usage: homedir reset <username> [--no-preserve-data]";
            }
            
            string username = args[1];
            bool preserveData = true;
            
            // Check for --no-preserve-data flag
            if (args.Length >= 3 && args[2] == "--no-preserve-data")
            {
                preserveData = false;
            }
            
            // Check permissions
            if (!currentUser.IsAdmin && currentUser.Username != username)
            {
                return "Error: You can only reset your own home directory unless you're an administrator.";
            }
            
            // Reset home directory
            bool success = await _homeDirectoryService.ResetHomeDirectoryAsync(username, preserveData);
            
            if (success)
            {
                return $"Home directory for user '{username}' reset successfully.";
            }
            else
            {
                return $"Error: Failed to reset home directory for user '{username}'.";
            }
        }
        
        /// <summary>
        /// Handles the 'migrate' subcommand
        /// </summary>
        private async Task<string> HandleMigrateCommand(string[] args, User currentUser)
        {
            if (args.Length < 3)
            {
                return "Usage: homedir migrate <username> <template> [--no-preserve-data]";
            }
            
            string username = args[1];
            string templateName = args[2];
            bool preserveData = true;
            
            // Check for --no-preserve-data flag
            if (args.Length >= 4 && args[3] == "--no-preserve-data")
            {
                preserveData = false;
            }
            
            // Check permissions
            if (!currentUser.IsAdmin && currentUser.Username != username)
            {
                return "Error: You can only migrate your own home directory unless you're an administrator.";
            }
            
            // Migrate to new template
            bool success = await _homeDirectoryService.MigrateUserToTemplateAsync(username, templateName, preserveData);
            
            if (success)
            {
                return $"User '{username}' migrated successfully to template '{templateName}'.";
            }
            else
            {
                return $"Error: Failed to migrate user '{username}' to template '{templateName}'.";
            }
        }
        
        /// <summary>
        /// Handles the 'quota' subcommand
        /// </summary>
        private async Task<string> HandleQuotaCommand(string[] args, User currentUser)
        {
            if (args.Length < 2)
            {
                // List all quotas (admin only)
                if (currentUser.IsAdmin)
                {
                    return await GetAllQuotasReport();
                }
                else
                {
                    // Show current user's quota
                    return await GetUserQuotaReport(currentUser.Username);
                }
            }
            
            string subSubCommand = args[1].ToLower();
            
            switch (subSubCommand)
            {
                case "set":
                    // Set quota (admin only)
                    if (!currentUser.IsAdmin)
                    {
                        return "Error: Only administrators can set quotas.";
                    }
                    
                    if (args.Length < 5)
                    {
                        return "Usage: homedir quota set <username> <soft-limit-mb> <hard-limit-mb>";
                    }
                    
                    string username = args[2];
                    
                    if (!long.TryParse(args[3], out long softLimitMB) || softLimitMB < 0)
                    {
                        return "Error: Soft limit must be a non-negative number.";
                    }
                    
                    if (!long.TryParse(args[4], out long hardLimitMB) || hardLimitMB < softLimitMB)
                    {
                        return "Error: Hard limit must be greater than or equal to soft limit.";
                    }
                    
                    // Convert MB to bytes
                    long softLimit = softLimitMB * 1024 * 1024;
                    long hardLimit = hardLimitMB * 1024 * 1024;
                    
                    bool success = await _homeDirectoryService.SetUserQuotaAsync(username, softLimit, hardLimit);
                    
                    if (success)
                    {
                        return $"Quota set successfully for user '{username}'.";
                    }
                    else
                    {
                        return $"Error: Failed to set quota for user '{username}'.";
                    }
                
                case "report":
                case "list":
                    // List all quotas (admin only)
                    if (currentUser.IsAdmin)
                    {
                        return await GetAllQuotasReport();
                    }
                    else
                    {
                        return "Error: Only administrators can view all quotas.";
                    }
                
                case "check":
                    // Check specific user's quota
                    if (args.Length < 3)
                    {
                        return "Usage: homedir quota check <username>";
                    }
                    
                    username = args[2];
                    
                    // Check permissions
                    if (!currentUser.IsAdmin && currentUser.Username != username)
                    {
                        return "Error: You can only check your own quota unless you're an administrator.";
                    }
                    
                    return await GetUserQuotaReport(username);
                
                default:
                    // Assume it's a username
                    username = args[1];
                    
                    // Check permissions
                    if (!currentUser.IsAdmin && currentUser.Username != username)
                    {
                        return "Error: You can only check your own quota unless you're an administrator.";
                    }
                    
                    return await GetUserQuotaReport(username);
            }
        }
        
        /// <summary>
        /// Handles the 'umask' subcommand
        /// </summary>
        private async Task<string> HandleUmaskCommand(string[] args, User currentUser)
        {
            if (args.Length < 2)
            {
                // Show current user's umask
                int umask = _homeDirectoryService.UmaskManager.GetUmask(currentUser.Username);
                return $"Current umask: {umask:000} ({UmaskManager.GetUmaskDescription(umask)})";
            }
            
            string subSubCommand = args[1].ToLower();
            
            switch (subSubCommand)
            {
                case "set":
                    if (args.Length < 3)
                    {
                        return "Usage: homedir umask set <umask-value> [username]";
                    }
                    
                    if (!int.TryParse(args[2], out int umaskValue) || umaskValue < 0 || umaskValue > 0777)
                    {
                        return "Error: Umask must be a valid octal number between 0 and 777.";
                    }
                    
                    string username = args.Length >= 4 ? args[3] : currentUser.Username;
                    
                    // Check permissions
                    if (!currentUser.IsAdmin && currentUser.Username != username)
                    {
                        return "Error: You can only set your own umask unless you're an administrator.";
                    }
                    
                    bool success = await _homeDirectoryService.SetUserUmaskAsync(username, umaskValue);
                    
                    if (success)
                    {
                        return $"Umask set successfully for user '{username}' to {umaskValue:000}.";
                    }
                    else
                    {
                        return $"Error: Failed to set umask for user '{username}'.";
                    }
                
                case "default":
                    // Set or show default umask
                    if (!currentUser.IsAdmin)
                    {
                        return "Error: Only administrators can manage default umask.";
                    }
                    
                    if (args.Length < 3)
                    {
                        // Show default umask
                        int defaultUmask = _homeDirectoryService.UmaskManager.GetDefaultUmask();
                        return $"Default umask: {defaultUmask:000} ({UmaskManager.GetUmaskDescription(defaultUmask)})";
                    }
                    
                    if (!int.TryParse(args[2], out umaskValue) || umaskValue < 0 || umaskValue > 0777)
                    {
                        return "Error: Umask must be a valid octal number between 0 and 777.";
                    }
                    
                    success = await _homeDirectoryService.UmaskManager.SetDefaultUmaskAsync(umaskValue);
                    
                    if (success)
                    {
                        return $"Default umask set successfully to {umaskValue:000}.";
                    }
                    else
                    {
                        return $"Error: Failed to set default umask.";
                    }
                
                default:
                    // Assume it's a username
                    username = args[1];
                    
                    // Check permissions
                    if (!currentUser.IsAdmin && currentUser.Username != username)
                    {
                        return "Error: You can only check your own umask unless you're an administrator.";
                    }
                    
                    umask = _homeDirectoryService.UmaskManager.GetUmask(username);
                    return $"Umask for user '{username}': {umask:000} ({UmaskManager.GetUmaskDescription(umask)})";
            }
        }
        
        /// <summary>
        /// Handles the 'size' subcommand
        /// </summary>
        private async Task<string> HandleSizeCommand(string[] args, User currentUser)
        {
            string username = args.Length >= 2 ? args[1] : currentUser.Username;
            
            // Check permissions
            if (!currentUser.IsAdmin && currentUser.Username != username)
            {
                return "Error: You can only check your own home directory size unless you're an administrator.";
            }
            
            long size = await _homeDirectoryService.GetHomeDirectorySizeAsync(username);
            
            if (size < 0)
            {
                return $"Error: Failed to calculate home directory size for user '{username}'.";
            }
            
            return FormatSize(username, size);
        }
        
        /// <summary>
        /// Handles the 'templates' subcommand
        /// </summary>
        private async Task<string> HandleTemplatesCommand(string[] args, User currentUser)
        {
            // Get all templates
            var templates = new List<HomeDirectoryTemplate>();
            
            // Add standard templates
            templates.Add(_homeDirectoryService.TemplateManager.GetTemplate("default"));
            templates.Add(_homeDirectoryService.TemplateManager.GetTemplate("admin"));
            templates.Add(_homeDirectoryService.TemplateManager.GetTemplate("developer"));
            
            // Build report
            string report = "Available Home Directory Templates:\n\n";
            
            foreach (var template in templates)
            {
                report += $"Template: {template.Name}\n";
                report += $"Description: {template.Description}\n";
                report += $"User Type: {template.UserType}\n";
                report += $"Admin: {(template.IsForAdmin ? "Yes" : "No")}\n";
                report += $"Directories: {template.Directories.Count}\n";
                report += $"Config Files: {template.ConfigFiles.Count}\n\n";
            }
            
            return report;
        }
        
        /// <summary>
        /// Handles the 'permissions' subcommand
        /// </summary>
        private async Task<string> HandlePermissionsCommand(string[] args, User currentUser)
        {
            if (args.Length < 2)
            {
                // Show available permission presets
                return GetPermissionPresetsHelp();
            }
            
            string subSubCommand = args[1].ToLower();
            
            switch (subSubCommand)
            {
                case "apply":
                    if (args.Length < 3)
                    {
                        return "Usage: homedir perms apply <username>";
                    }
                    
                    string username = args[2];
                    
                    // Check permissions
                    if (!currentUser.IsAdmin && currentUser.Username != username)
                    {
                        return "Error: You can only apply permissions to your own home directory unless you're an administrator.";
                    }
                    
                    bool success = await _homeDirectoryService.ApplyStandardPermissionsAsync(username);
                    
                    if (success)
                    {
                        return $"Standard permissions applied successfully to home directory for user '{username}'.";
                    }
                    else
                    {
                        return $"Error: Failed to apply standard permissions to home directory for user '{username}'.";
                    }
                
                case "list":
                    // Show available permission presets
                    return GetPermissionPresetsHelp();
                
                default:
                    return "Unknown permission command. Use 'homedir help' for usage information.";
            }
        }
        
        /// <summary>
        /// Gets a report of all user quotas
        /// </summary>
        private async Task<string> GetAllQuotasReport()
        {
            var reports = await _homeDirectoryService.QuotaManager.GetQuotaReportAsync();
            
            if (reports.Count == 0)
            {
                return "No quotas defined.";
            }
            
            string report = "User Quota Report:\n\n";
            report += "Username      Soft Limit      Hard Limit      Usage          Status\n";
            report += "-------------------------------------------------------------------------\n";
            
            foreach (var quota in reports)
            {
                string status = quota.Status switch
                {
                    QuotaStatus.BelowLimit => "OK",
                    QuotaStatus.AboveSoftLimit => "WARNING",
                    QuotaStatus.AboveHardLimit => "CRITICAL",
                    _ => "ERROR"
                };
                
                report += $"{quota.Username,-14}{FormatBytes(quota.SoftLimit),-16}{FormatBytes(quota.HardLimit),-16}{FormatBytes(quota.CurrentUsage),-16}{status}\n";
            }
            
            return report;
        }
        
        /// <summary>
        /// Gets a report of a user's quota
        /// </summary>
        private async Task<string> GetUserQuotaReport(string username)
        {
            var quota = _homeDirectoryService.QuotaManager.GetQuota(username);
            
            if (quota == null)
            {
                return $"No quota defined for user '{username}'.";
            }
            
            // Update usage
            await _homeDirectoryService.QuotaManager.UpdateUsageAsync(username);
            
            // Get updated quota
            quota = _homeDirectoryService.QuotaManager.GetQuota(username);
            
            if (quota == null)
            {
                return $"Error: Failed to get quota for user '{username}'.";
            }
            
            // Check status
            var status = await _homeDirectoryService.QuotaManager.CheckQuotaAsync(username);
            
            string statusStr = status switch
            {
                QuotaStatus.BelowLimit => "OK",
                QuotaStatus.AboveSoftLimit => "WARNING: Above soft limit",
                QuotaStatus.AboveHardLimit => "CRITICAL: Above hard limit",
                _ => "ERROR"
            };
            
            string report = $"Quota for user '{username}':\n\n";
            report += $"Soft Limit:   {FormatBytes(quota.SoftLimit)}\n";
            report += $"Hard Limit:   {FormatBytes(quota.HardLimit)}\n";
            report += $"Current Usage: {FormatBytes(quota.CurrentUsage)}\n";
            
            // Calculate percentages
            double softPercentage = quota.SoftLimit > 0 ? (double)quota.CurrentUsage / quota.SoftLimit * 100 : 0;
            double hardPercentage = quota.HardLimit > 0 ? (double)quota.CurrentUsage / quota.HardLimit * 100 : 0;
            
            report += $"Soft Limit Usage: {softPercentage:F1}%\n";
            report += $"Hard Limit Usage: {hardPercentage:F1}%\n";
            report += $"Status: {statusStr}\n";
            
            return report;
        }
        
        /// <summary>
        /// Formats a directory size with username
        /// </summary>
        private string FormatSize(string username, long size)
        {
            return $"Home directory size for user '{username}': {FormatBytes(size)}";
        }
        
        /// <summary>
        /// Formats a byte count in a human-readable format
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            double number = bytes;
            
            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:F2} {suffixes[counter]}";
        }
        
        /// <summary>
        /// Gets help text for permission presets
        /// </summary>
        private string GetPermissionPresetsHelp()
        {
            string help = "Permission Presets:\n\n";
            
            help += "Private (700):       Owner can read/write/execute, others have no access\n";
            help += "Protected (750):     Owner can read/write/execute, group can read/execute, others have no access\n";
            help += "Shared (770):        Owner and group can read/write/execute, others have no access\n";
            help += "Standard (755):      Owner can read/write/execute, others can read/execute\n";
            help += "Public (775):        Owner and group can read/write/execute, others can read/execute\n";
            help += "World Writable (777): Everyone can read/write/execute\n";
            help += "Sticky (1777):       Everyone can read/write/execute, but only owner can delete their files\n";
            help += "SetGID (2775):       Files created in this directory inherit its group\n\n";
            
            help += "Usage: homedir perms apply <username>\n";
            help += "  Applies standard permission presets to a user's home directory\n";
            
            return help;
        }
        
        /// <summary>
        /// Gets the help text for the command
        /// </summary>
        private string GetHelpText()
        {
            string help = "Home Directory Management Utility\n\n";
            
            help += "Usage: homedir <command> [arguments]\n\n";
            
            help += "Commands:\n";
            help += "  create <username> [template]      Create a home directory for a user\n";
            help += "  reset <username> [--no-preserve-data]  Reset a home directory to defaults\n";
            help += "  migrate <username> <template> [--no-preserve-data]  Migrate a user to a different template\n";
            help += "  quota [command/username]          Manage disk quotas\n";
            help += "    set <username> <soft-mb> <hard-mb>  Set quota for a user\n";
            help += "    check <username>                Check quota for a user\n";
            help += "    report                          Show quota report (admin only)\n";
            help += "  umask [command/username]          Manage umask settings\n";
            help += "    set <umask-value> [username]    Set umask for a user\n";
            help += "    default [umask-value]           Show or set default umask\n";
            help += "  size [username]                   Show home directory size\n";
            help += "  templates                         List available templates\n";
            help += "  perms|permissions [command]       Manage directory permissions\n";
            help += "    apply <username>                Apply standard permissions\n";
            help += "    list                            Show available permission presets\n";
            help += "  help                              Show this help text\n";
            
            return help;
        }
    }
}
