import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { FixedPathAlias, SymlinkAlias } from '../../core/filesystem';

/**
 * Command to add a new path alias to the filesystem
 */
export class AddAliasCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'addalias';
  }
  
  public get description(): string {
    return 'Add a new filesystem path alias';
  }
    public get usage(): string {
    return `Usage: addalias [--symlink] <alias> <targetPath>

Create a new filesystem path alias that points to an existing target path.

Description:
  The 'addalias' command creates a path alias in the filesystem, providing
  a shortcut to access a specific directory. There are two types of aliases:
  - Standard alias (default): A permanent reference to a fixed path
  - Symlink alias: A symbolic link that follows changes to the target path

Options:
  --symlink    Create a symbolic link alias instead of a fixed path alias

Arguments:
  alias        The name of the new alias to create (without slashes)
  targetPath   The existing path that the alias should point to

Examples:
  addalias projects /home/user/Documents/projects
    Creates a fixed path alias 'projects' pointing to '/home/user/Documents/projects'
  
  addalias --symlink myproject /home/user/projects/current
    Creates a symbolic link 'myproject' that points to '/home/user/projects/current'

Note:
  The target path must already exist in the filesystem before creating an alias.`;
  }
  
  async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      let isSymlink = false;
      let aliasName: string;
      let targetPath: string;
      
      const cmdArgs = args.args || [];
      
      // Check if the --symlink flag is present
      if (cmdArgs[0] === "--symlink") {
        isSymlink = true;
        cmdArgs.shift(); // Remove the flag from args
      }
      
      if (cmdArgs.length !== 2) {
        context.stderr.writeLine("Error: Invalid arguments");
        context.stdout.writeLine(`Usage: ${this.usage}`);
        context.stdout.writeLine("Examples:");
        context.stdout.writeLine("  addalias projects /home/user/Documents/projects");
        context.stdout.writeLine("  addalias --symlink myproject /home/user/projects/current");
        return 1;
      }
      
      [aliasName, targetPath] = cmdArgs;
      
      // Validate the target path exists
      const targetExists = await this.os.getFileSystem().exists(targetPath);
      if (!targetExists) {
        context.stderr.writeLine(`Error: Target path does not exist: ${targetPath}`);
        return 1;
      }
      
      // Register the appropriate type of alias
      if (isSymlink) {
        this.os.getFileSystem().registerSymlink(aliasName, targetPath);
        context.stdout.writeLine(`Symlink created: ${aliasName} -> ${targetPath}`);
      } else {
        this.os.getFileSystem().registerFixedAlias(aliasName, targetPath);
        context.stdout.writeLine(`Alias created: ${aliasName} -> ${targetPath}`);
      }
      
      return 0;
    } catch (error) {
      context.stderr.writeLine(`Error adding alias: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
}
