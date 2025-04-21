import { OS } from '../core/os';
import { TerminalApp } from '../apps/terminal';
import { Terminal } from 'xterm';

/**
 * Interface for command arguments
 */
export interface CommandArgs {
  args: string[];
  [key: string]: any;
}

/**
 * Interface for command I/O streams
 */
export interface CommandOutputStream {
  write(text: string): void;
  writeLine(text: string): void;
  clear(): void;
}

export interface CommandInputStream {
  read(): Promise<string>;
  readLine(): Promise<string>;
  readChar(): Promise<string>;
}

/**
 * Interface for directory change event handler
 */
export type DirectoryChangeHandler = (newPath: string) => void;

/**
 * Interface for command execution context
 */
export interface CommandContext {
  os: OS;
  get xTerm(): Terminal
  /**
   * Get the current working directory
   */
  get cwd(): string;
  
  /**
   * Set the current working directory and trigger any associated events
   */
  set cwd(newPath: string);
  
  stdin: CommandInputStream;
  stdout: CommandOutputStream;
  stderr: CommandOutputStream;
  env: Record<string, string>;
  
  /**
   * Reference to the terminal app that created this context
   */
  terminalApp?: TerminalApp; // Using any to avoid circular dependencies
}

/**
 * Interface for command modules
 */
export interface CommandModule {
  name: string;
  description: string;
  usage: string;
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  execute(args: CommandArgs, context: CommandContext): Promise<number>;
  exec?(_args: CommandArgs): Promise<string>;
}

/**
 * Helper class for migrating commands from old exec interface to new execute interface
 */
export class ExecuteMigrator {
  /**
   * Execute a command's legacy exec function within the new CommandContext interface
   
   * @param commandInstance The command instance (this)
   * @param args Command arguments
   * @param context Command execution context
   * @returns Promise resolving to exit code
   */
  public static async execute(
    
    commandInstance: CommandModule,
    args: CommandArgs, 
    context: CommandContext
  ): Promise<number> {
    try {
      var execFn: (args: CommandArgs) => Promise<string> = commandInstance.exec!;
      // Call the legacy exec function with the proper this binding
      const result = await execFn.call(commandInstance, args);
      
      // Write the result to stdout
      if (result) {
        context.stdout.writeLine(result);
      }
      
      // Return success exit code
      return 0;
    } catch (error) {
      // Write error to stderr
      context.stderr.writeLine(`Error: ${error instanceof Error ? error.message : String(error)}`);
      
      // Return error exit code
      return 1;
    }
  }
}

/**
 * Command Processor class for handling terminal commands
 */
export class CommandProcessor {
  private os: OS;
  private commands: Map<string, CommandModule> = new Map();
  private aliasMap: Map<string, string> = new Map();
  constructor(os: OS) {
    this.os = os;
  }

  /**
   * Register a command module
   */
  public registerCommand(command: CommandModule): void {
    this.commands.set(command.name, command);
  }

  /**
   * Register a command alias
   */
  public registerAlias(alias: string, commandName: string): void {
    this.aliasMap.set(alias, commandName);
  }
  /**
   * Process a command string
   */
  public async processCommand(commandStr: string, context: CommandContext ): Promise<string> {
    // Trim whitespace
    commandStr = commandStr.trim();
    
    // Skip empty commands
    if (!commandStr) {
      return '';
    }
    
    // Parse the command string
    const { command, args, options } = this.parseCommandString(commandStr);
    
    // Look up the command
    let commandModule = this.commands.get(command);
    
    // Check aliases if command not found
    if (!commandModule && this.aliasMap.has(command)) {
      const actualCommand = this.aliasMap.get(command);
      if (actualCommand) {
        commandModule = this.commands.get(actualCommand);
      }
    }
      // Check if command exists
    if (!commandModule) {
      return `Command not found: ${command}. Try 'help' for a list of commands.`;
    }
    
    try {
      // Create context for command execution
      
      // Execute the command with new interface
      const exitCode = await commandModule.execute({
        args,
        ...options
      }, context);
      
      // For backward compatibility, return success message
      return exitCode === 0 ? 'Command executed successfully' : `Command failed with exit code ${exitCode}`;
    } catch (error) {
      console.error(`Error executing command '${command}':`, error);
      return `Error: ${error instanceof Error ? error.message : String(error)}`;
    }
  }
  /**
   * Parse a command string into command, args, and options
   */
  private parseCommandString(commandStr: string): { command: string, args: string[], options: Record<string, any> } {
    // Parse the command line with respect to quotes
    const parsedArgs = this.parseCommandArgs(commandStr);
    
    // First argument is the command name
    const command = parsedArgs.length > 0 ? parsedArgs[0] : '';
    const args: string[] = [];
    const options: Record<string, any> = {};
    
    // Process arguments and options (starting from index 1 to skip the command)
    for (let i = 1; i < parsedArgs.length; i++) {
      const part = parsedArgs[i];
      
      // Handle options (starting with -)
      if (part.startsWith('-')) {
        // Handle long options (--option)
        if (part.startsWith('--')) {
          const optionName = part.slice(2);
          
          // Check if there's a value (--option=value)
          if (optionName.includes('=')) {
            const [name, value] = optionName.split('=');
            options[name] = value;
          } else {
            // Boolean option (--option)
            options[optionName] = true;
          }
        }
        // Handle short options (-o)
        else {
          const optionName = part.slice(1);
          
          // Check if next part is a value
          if (i + 1 < parsedArgs.length && !parsedArgs[i + 1].startsWith('-')) {
            options[optionName] = parsedArgs[i + 1];
            i++; // Skip the value
          } else {
            // Boolean option (-o)
            options[optionName] = true;
          }
        }
      }
      // Regular argument
      else {
        args.push(part);
      }
    }
    
    return { command, args, options };
  }
  
  /**
   * Parse command arguments, respecting quotes
   * @param command The command string to parse
   * @returns Array of parsed arguments
   */
  private parseCommandArgs(command: string): string[] {
    const args: string[] = [];
    let currentArg = '';
    let inQuotes = false;
    let quoteChar = '';
    let escapeNext = false;
    
    // Process each character in the command
    for (let i = 0; i < command.length; i++) {
      const char = command[i];
      
      // Handle escape character
      if (escapeNext) {
        currentArg += char;
        escapeNext = false;
        continue;
      }
      
      // Check for escape character
      if (char === '\\') {
        escapeNext = true;
        continue;
      }
      
      // Handle quotes (both single and double)
      if ((char === '"' || char === "'") && (!inQuotes || char === quoteChar)) {
        if (inQuotes) {
          // Closing quote
          inQuotes = false;
        } else {
          // Opening quote
          inQuotes = true;
          quoteChar = char;
        }
        continue;
      }
      
      // Handle spaces
      if (char === ' ' && !inQuotes) {
        // If we have a current argument, add it to args and reset
        if (currentArg || currentArg === '0') {
          args.push(currentArg);
          currentArg = '';
        }
        continue;
      }
      
      // Add character to current argument
      currentArg += char;
    }
    
    // Add any remaining argument
    if (currentArg || currentArg === '0') {
      args.push(currentArg);
    }
    
    return args;
  }
  /**
   * Get all available commands
   */
  public getAllCommands(): CommandModule[] {
    return Array.from(this.commands.values());
  }
}
