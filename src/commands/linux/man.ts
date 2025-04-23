import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

/**
 * man command - Display manual pages for commands
 */
export class ManCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
    public get name(): string {
    return 'man';
  }
  
  public get description(): string {
    return 'Display the manual page for a command';
  }
  
  public get usage(): string {
    return 'man [command_name]\n' +
           'Display the manual page for the specified command.\n\n' +
           'The man command formats and displays the reference manuals for the shell commands.\n' +
           'Each page includes NAME, SYNOPSIS, DESCRIPTION sections and may include additional\n' +
           'information like OPTIONS and EXAMPLES for certain commands.\n\n' +
           'Examples:\n' +
           '  man ls        Display the manual page for the ls command\n' +
           '  man grep      Display the manual page for the grep command\n' +
           '  man man       Display this manual page\n\n' +
           'If no command is specified, man will prompt for one.';
  }
  
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    // Check if a command is specified
    if (args.args.length === 0) {
      context.stdout.writeLine('What manual page do you want?\nFor example, try \'man man\'.');
      return 1;
    }
    
    const commandName = args.args[0];
    
    // Get all available commands
    const allCommands = this.os.getCommandProcessor().getAllCommands();
    
    // Find the specified command
    const command = allCommands.find(cmd => cmd.name === commandName);
    
    if (!command) {
      context.stdout.writeLine(`No manual entry for ${commandName}`);
      return 1;
    }
    
    // Format the manual page and write to stdout
    const manPage = this.formatManPage(command);
    context.stdout.writeLine(manPage);
    
    return 0;
  }
  
  /**
   * Format a command as a manual page
   */
  private formatManPage(command: CommandModule): string {
    const header = `${command.name.toUpperCase()}(1)                User Commands                ${command.name.toUpperCase()}(1)`;
    const name = `NAME\n       ${command.name} - ${command.description}`;
    const synopsis = `SYNOPSIS\n       ${command.usage}`;
    const description = `DESCRIPTION\n       ${this.expandDescription(command)}`;
    const footer = `HackerOS                         April 2025                        ${command.name.toUpperCase()}(1)`;
    
    return [header, '', name, '', synopsis, '', description, '', footer].join('\n');
  }
    /**
   * Expand command description with more detailed information when available
   */
  private expandDescription(command: CommandModule): string {
    // This is where you would add more detailed descriptions for each command
    
    // Special case for the 'man' command itself
    if (command.name === 'man') {
      return `The man command displays manual pages for commands available in the system.
       
       When you run 'man command_name', it displays information about that command,
       including its description, usage syntax, and available options.`;
    }
    
    // Documentation for find command
    if (command.name === 'find') {
      return `The find command searches for files in a directory hierarchy.
       
       OPTIONS:
         -name PATTERN      Search for files with names matching the given pattern.
                           Wildcards like * and ? are supported.
         -type TYPE         Search for files of a specific type:
                           'f' for regular files
                           'd' for directories
         -maxdepth N        Descend at most N levels of directories.

       EXAMPLES:
         find . -name "*.txt"               Find all .txt files in current directory and subdirectories
         find /home -type d                 Find all directories under /home
         find /var/log -name "*.log" -type f  Find all log files in /var/log
         find . -maxdepth 1                 List only files in the current directory (no recursion)`;
    }
    
    // Documentation for chmod command
    if (command.name === 'chmod') {
      return `The chmod command changes the file mode bits (permissions) of files.
       
       MODES:
         Modes can be specified in either numeric (octal) or symbolic notation.
         
         Octal notation:
           chmod 755 file         Sets permissions to rwxr-xr-x
           chmod 644 file         Sets permissions to rw-r--r--
         
         Symbolic notation:
           u: user who owns the file
           g: group that owns the file
           o: other users
           a: all users (same as ugo)
           
           +: add permission
           -: remove permission
           =: set exact permission
           
           r: read permission
           w: write permission
           x: execute permission
           
       OPTIONS:
         -R, -r, --recursive    Change permissions recursively
         -v, --verbose          Display a message for each file processed
       
       EXAMPLES:
         chmod u+x script.sh             Make script executable for the owner
         chmod go-w file.txt             Remove write permission for group and others
         chmod a=r file.txt              Set read-only permission for everyone
         chmod -R u+rwX,go+rX directory  Recursively set permissions on a directory`;
    }
    
    // Documentation for diff command
    if (command.name === 'diff') {
      return `The diff command compares files line by line.
       
       OPTIONS:
         -i, --ignore-case        Ignore case differences
         -w, --ignore-all-space   Ignore all white space
         -B, --ignore-blank-lines Ignore changes where lines are all blank
         -q, --brief              Output only whether files differ
         -u[N]                    Output N lines of unified context (default 3)
       
       OUTPUT FORMAT:
         Normal format (default):
           n1,n2cN1,N2   Lines n1 to n2 in first file changed to lines N1 to N2 in second file
           n1,n2dN1      Lines n1 to n2 in first file deleted (absent from second file)
           n1aN1,N2      Lines N1 to N2 in second file added (absent from first file)
         
         Unified format (-u):
           Shows context lines and indicates changes with +/- prefixes
           + lines are added in the second file
           - lines are removed from the first file
           
       EXAMPLES:
         diff file1.txt file2.txt         Compare two files
         diff -i file1.txt file2.txt      Compare ignoring case differences
         diff -u file1.txt file2.txt      Output in unified format
         diff -q directory1 directory2    Check if directories contain the same files`;
    }
    
    // Documentation for grep command
    if (command.name === 'grep') {
      return `The grep command searches for patterns in files.
       
       OPTIONS:
         -i, --ignore-case       Ignore case distinctions
         -v, --invert-match      Select non-matching lines
         -n, --line-number       Print line number with output lines
         -c, --count             Print only a count of matching lines
         -o, --only-matching     Show only the part of a line matching the pattern
         -r, -R, --recursive     Read all files under directories recursively
       
       PATTERN:
         The pattern is a regular expression by default.
         
       EXAMPLES:
         grep "error" log.txt               Find lines containing "error" in log.txt
         grep -i "warning" *.log            Find "warning" (case insensitive) in all .log files
         grep -r "TODO" .                   Find "TODO" recursively in current directory
         grep -v "^#" config.txt            Show lines that don't start with #
         grep -n "function" *.js            Show line numbers for matches in JS files
         grep -c "ERROR" logs/*.log         Count ERROR occurrences in log files`;
    }
    
    return command.description;
  }
}
