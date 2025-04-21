import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * launch command - Launch a GUI application from the terminal
 */
export class LaunchCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'launch';
  }
  
  public get description(): string {
    return 'Launch a GUI application from the terminal';
  }
  
  public get usage(): string {
    return 'launch [--list] [application-name] [arguments...]';
  }
  
  /**
   * Execute the launch command
   * @param args Command arguments
   * @param context Command execution context
   * @returns Exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Check if we should list available applications
    if (args.args.length === 0 || (args.options && args.options.list)) {
      this.listAvailableApps(context);
      return 0; // Success exit code
    }

    const appName = args.args[0];
    const appArgs = args.args.slice(1);
    
    try {
      // Get app manager
      const appManager = this.os.getAppManager();
      
      // Get all registered apps
      const allApps = appManager.getAllApps();
      
      // Find the app with the given name (case-insensitive)
      const appInfo = allApps.find(app => 
        app.name.toLowerCase() === appName.toLowerCase() || 
        app.id.toLowerCase() === appName.toLowerCase()
      );
      
      if (!appInfo) {
        context.stderr.writeLine(`launch: application '${appName}' not found`);
        context.stderr.writeLine('Use launch --list to see available applications');
        return 1; // Error exit code
      }
      
      if (!appInfo.launchable) {
        context.stderr.writeLine(`launch: application '${appName}' is not launchable`);
        return 1; // Error exit code
      }
      
      // Launch the application
      const instanceId = appManager.launchApp(appInfo.id, appArgs);
      
      if (!instanceId) {
        context.stderr.writeLine(`launch: failed to start application '${appName}'`);
        return 1; // Error exit code
      }
      
      context.stdout.writeLine(`Application '${appInfo.name}' started successfully`);
      return 0; // Success exit code
    } catch (error) {
      context.stderr.writeLine(`launch: error: ${error instanceof Error ? error.message : String(error)}`);
      return 1; // Error exit code
    }
  }
  
  /**
   * List all available applications
   */
  private listAvailableApps(context: CommandContext): void {
    // Get app manager
    const appManager = this.os.getAppManager();
    
    // Get all registered apps
    const allApps = appManager.getAllApps();
    
    // Filter to only show launchable apps
    const launchableApps = allApps.filter(app => app.launchable);
    
    if (launchableApps.length === 0) {
      context.stdout.writeLine('No launchable applications found');
      return;
    }
    
    context.stdout.writeLine('Available applications:');
    context.stdout.writeLine('');
    
    // Find the longest app name for formatting
    const maxNameLength = Math.max(...launchableApps.map(app => app.name.length));
    
    // Sort by name and display
    launchableApps
      .sort((a, b) => a.name.localeCompare(b.name))
      .forEach(app => {
        const padding = ' '.repeat(maxNameLength - app.name.length + 2);
        context.stdout.writeLine(`  ${app.icon} ${app.name}${padding}${app.description}`);
      });
    
    context.stdout.writeLine('');
    context.stdout.writeLine('Usage: launch <application-name> [arguments...]');
  }
}
