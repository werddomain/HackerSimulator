import { Terminal as xTerm } from 'xterm';
import { OS } from '../core/os';
import { CommandArgs, CommandModule } from './command-processor';

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
 * Interface for command execution context
 */
export interface CommandContext {
  os: OS;
  cwd: string;
  stdin: CommandInputStream;
  stdout: CommandOutputStream;
  stderr: CommandOutputStream;
  env: Record<string, string>;
  terminal?: any; // Reference to the terminal object
}

/**
 * Enhanced command module interface with streaming support
 */
export interface EnhancedCommandModule extends CommandModule {
  // New method with context and streams
  execute(args: CommandArgs, context: CommandContext): Promise<number>;
}

/**
 * Default implementation of CommandOutputStream that collects output in a string
 */
export class StringOutputStream implements CommandOutputStream {
  private buffer: string = '';
  
  public write(text: string): void {
    this.buffer += text;
  }
  
  public writeLine(text: string): void {
    this.buffer += text + '\n';
  }
  
  public clear(): void {
    this.buffer = '';
  }
  
  public toString(): string {
    return this.buffer;
  }
}

/**
 * Default implementation of CommandInputStream that reads from a string
 */
export class StringInputStream implements CommandInputStream {
  private buffer: string;
  private position: number = 0;
  
  constructor(input: string = '') {
    this.buffer = input;
  }
  
  public async read(): Promise<string> {
    if (this.position >= this.buffer.length) {
      return '';
    }
    
    const result = this.buffer.slice(this.position);
    this.position = this.buffer.length;
    return result;
  }
  
  public async readLine(): Promise<string> {
    if (this.position >= this.buffer.length) {
      return '';
    }
    
    const newlineIndex = this.buffer.indexOf('\n', this.position);
    if (newlineIndex === -1) {
      const result = this.buffer.slice(this.position);
      this.position = this.buffer.length;
      return result;
    }
    
    const result = this.buffer.slice(this.position, newlineIndex);
    this.position = newlineIndex + 1;
    return result;
  }
  
  public async readChar(): Promise<string> {
    if (this.position >= this.buffer.length) {
      return '';
    }
    
    const result = this.buffer[this.position];
    this.position++;
    return result;
  }
}

/**
 * Terminal implementation of CommandOutputStream that writes to a terminal
 */
export class TerminalOutputStream implements CommandOutputStream {
  private terminal: xTerm; // Terminal instance
  
  constructor(terminal: xTerm) {
    this.terminal = terminal;
  }
  
  public write(text: string): void {
    this.terminal.write(text);
  }
  
  public writeLine(text: string): void {
    this.terminal.writeln(text);
  }
  
  public clear(): void {
    this.terminal.clear();
  }
}

/**
 * Terminal implementation of CommandInputStream that reads from a terminal
 */
export class TerminalInputStream implements CommandInputStream {
  private terminal: xTerm; // Terminal instance
  private inputQueue: string[] = [];
  private pendingResolvers: ((value: string) => void)[] = [];
  
  constructor(terminal: xTerm) {
    this.terminal = terminal;
    
    // Set up input handling
    this.terminal.onData((input: string) => {
      if (this.pendingResolvers.length > 0) {
        // If we have pending resolvers, resolve the oldest one
        const resolver = this.pendingResolvers.shift()!;
        resolver(input);
      } else {
        // Otherwise, queue the input
        this.inputQueue.push(input);
      }
    });
  }
  
  public async read(): Promise<string> {
    if (this.inputQueue.length > 0) {
      return this.inputQueue.shift()!;
    }
    
    return new Promise<string>(resolve => {
      this.pendingResolvers.push(resolve);
    });
  }
  
  public async readLine(): Promise<string> {
    // In a real terminal, this might wait for Enter key
    return this.read();
  }
  
  public async readChar(): Promise<string> {
    const input = await this.read();
    return input.length > 0 ? input[0] : '';
  }
}

/**
 * Enhanced Command Processor class with streaming support
 */
export class EnhancedCommandProcessor {
  private os: OS;
  private commands: Map<string, CommandModule> = new Map();
  private enhancedCommands: Map<string, EnhancedCommandModule> = new Map();
  private aliasMap: Map<string, string> = new Map();
  private currentDirectory: string = '/home/user'; // Default starting directory

  constructor(os: OS) {
    this.os = os;
  }
  
  /**
   * Register a command module
   */
  public registerCommand(command: CommandModule | EnhancedCommandModule): void {
    this.commands.set(command.name, command);
    
    // Check if it's an enhanced command
    if ('execute' in command && typeof command.execute === 'function') {
      this.enhancedCommands.set(command.name, command as EnhancedCommandModule);
    }
  }

  /**
   * Register a command alias
   */
  public registerAlias(alias: string, commandName: string): void {
    this.aliasMap.set(alias, commandName);
  }

  /**
   * Process a command string with context and streams
   */
  public async processCommandWithContext(
    commandStr: string, 
    context: CommandContext
  ): Promise<number> {
    // Trim whitespace
    commandStr = commandStr.trim();
    
    // Skip empty commands
    if (!commandStr) {
      return 0;
    }
    
    // Check for command piping
    if (commandStr.includes('|')) {
      return this.processPipedCommands(commandStr, context);
    }
    
    // Parse the command string
    const { command, args, options } = this.parseCommandString(commandStr);
    
    // Handle special case of cd (needs to be handled by the shell)
    if (command === 'cd') {
      const result = this.handleCdCommand(args, context);
      context.stdout.writeLine(result);
      return 0;
    }
    
    // Look up the command
    let commandName = command;
    let commandModule = this.commands.get(commandName);
    
    // Check aliases if command not found
    if (!commandModule && this.aliasMap.has(commandName)) {
      commandName = this.aliasMap.get(commandName) || commandName;
      commandModule = this.commands.get(commandName);
    }
    
    // Check if command exists
    if (!commandModule) {
      context.stderr.writeLine(`Command not found: ${command}. Try 'help' for a list of commands.`);
      return 127; // Command not found exit code
    }
    
    try {
      const commandArgs = {
        args,
        ...options
      };
      
      // Check if it's an enhanced command with execute method
      const enhancedCommand = this.enhancedCommands.get(commandName);
      if (enhancedCommand) {
        // Use the enhanced execute method with context
        return await enhancedCommand.execute(commandArgs, context);
      } else if (commandModule.exec) {
        // Fall back to legacy exec method
        const result = await commandModule.exec(commandArgs);
        if (result) {
          context.stdout.writeLine(result);
        }
        return 0;
      } else {
        context.stderr.writeLine(`Command ${command} has no execution method.`);
        return 1;
      }
    } catch (error) {
      console.error(`Error executing command '${command}':`, error);
      context.stderr.writeLine(`Error: ${error instanceof Error ? error.message : String(error)}`);
      return 1;
    }
  }
  
//   /**
//    * Create a default command context
//    */  
//   public createDefaultContext(
//     terminal?: Terminal,
//     inputStr?: string
//   ): CommandContext {
//     let stdin: CommandInputStream;
//     let stdout: CommandOutputStream;
//     let stderr: CommandOutputStream;
    
//     if (terminal) {
//       stdin = new TerminalInputStream(terminal);
//       stdout = new TerminalOutputStream(terminal);
//       stderr = new TerminalOutputStream(terminal);
//     } else {
//       stdin = new StringInputStream(inputStr || '');
//       stdout = new StringOutputStream();
//       stderr = new StringOutputStream();
//     }
//       return {
//       os: this.os,
//       cwd: this.getCurrentWorkingDirectory(),
//       stdin,
//       stdout,
//       stderr,
//       env: this.getEnvironmentVariables(),
//       terminal
//     };
//   }
  
  /**
   * Get environment variables
   */  private getEnvironmentVariables(): Record<string, string> {
    // This could be expanded to include more environment variables
    return {
      'HOME': '/home/user',
      'USER': 'user',
      'PATH': '/bin:/usr/bin:/usr/local/bin',
      'PWD': this.getCurrentWorkingDirectory(),
      'SHELL': '/bin/bash'
    };
  }
    /**
   * Get the current working directory
   */
  private getCurrentWorkingDirectory(): string {
    return this.currentDirectory;
  }
  
  /**
   * Change the current working directory
   */
  private changeWorkingDirectory(path: string): boolean {
    try {
      // Check if the directory exists
      const fs = this.os.getFileSystem();
      
      // Handle absolute vs relative paths
      let newPath: string;
      if (path.startsWith('/')) {
        // Absolute path
        newPath = path;
      } else {
        // Relative path - resolve against current directory
        if (this.currentDirectory.endsWith('/')) {
          newPath = this.currentDirectory + path;
        } else {
          newPath = this.currentDirectory + '/' + path;
        }
      }
      
      // Special case for "cd .."
      if (path === '..') {
        const parts = this.currentDirectory.split('/').filter(p => p);
        if (parts.length > 0) {
          parts.pop();
          newPath = '/' + parts.join('/');
        } else {
          newPath = '/';
        }
      }
      
      // Check if the directory exists by attempting to list it
      fs.readDirectory(newPath)
        .then(() => {
          // If we get here, the directory exists
          this.currentDirectory = newPath;
          return true;
        })
        .catch(() => {
          return false;
        });
      
      // For now, assume success if we didn't throw an error
      this.currentDirectory = newPath;
      return true;
    } catch (error) {
      console.error('Error changing directory:', error);
      return false;
    }
  }

  /**
   * Process command for backward compatibility
   */
  public async processCommand(commandStr: string): Promise<string> {
    // Create string-based context
    const stdout = new StringOutputStream();
    const stderr = new StringOutputStream();
      const context: CommandContext = {
      os: this.os,
      cwd: this.getCurrentWorkingDirectory(),
      stdin: new StringInputStream(),
      stdout,
      stderr,
      env: this.getEnvironmentVariables()
    };
    
    // Process the command with context
    await this.processCommandWithContext(commandStr, context);
    
    // Combine stdout and stderr
    const output = stdout.toString();
    const errors = stderr.toString();
    
    return output + (errors ? '\n' + errors : '');
  }

  /**
   * Parse a command string into command, args, and options
   */
  private parseCommandString(commandStr: string): { command: string, args: string[], options: Record<string, any> } {
    const parts = commandStr.split(' ');
    const command = parts[0];
    const args: string[] = [];
    const options: Record<string, any> = {};
    
    let i = 1;
    while (i < parts.length) {
      const part = parts[i];
      
      // Skip empty parts
      if (!part) {
        i++;
        continue;
      }
      
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
          if (i + 1 < parts.length && !parts[i + 1].startsWith('-')) {
            options[optionName] = parts[i + 1];
            i++; // Skip the value
          } else {
            // Boolean option (-o)
            for (const char of optionName) {
              options[char] = true;
            }
          }
        }
      }
      // Regular argument
      else {
        args.push(part);
      }
      
      i++;
    }
    
    return { command, args, options };
  }
  /**
   * Handle cd command (change directory)
   */
  private handleCdCommand(args: string[], context: CommandContext): string {
    const path = args[0] || context.env.HOME;
    
    try {
      if (this.changeWorkingDirectory(path)) {
        // Update PWD environment variable
        context.env.PWD = this.getCurrentWorkingDirectory();
        return ``;
      } else {
        return `cd: ${path}: No such file or directory`;
      }
    } catch (error) {
      return `cd: ${args[0]}: ${error instanceof Error ? error.message : String(error)}`;
    }
  }

  /**
   * Get all available commands
   */
  public getAllCommands(): CommandModule[] {
    return Array.from(this.commands.values());
  }
  
  /**
   * Get all enhanced commands
   */
  public getAllEnhancedCommands(): EnhancedCommandModule[] {
    return Array.from(this.enhancedCommands.values());
  }

  /**
   * Process a pipeline of commands (command1 | command2 | command3)
   */
  private async processPipedCommands(commandStr: string, context: CommandContext): Promise<number> {
    // Split the command string by pipe symbol
    const commands = commandStr.split('|').map(cmd => cmd.trim());
    
    // Need at least two commands for a pipe
    if (commands.length < 2) {
      return this.processCommandWithContext(commandStr, context);
    }
    
    let lastOutput = '';
    let lastExitCode = 0;
    
    // Process each command in the pipeline
    for (let i = 0; i < commands.length; i++) {
      const isLast = i === commands.length - 1;
      
      // Create a string output stream for intermediate commands
      const stdout = isLast ? context.stdout : new StringOutputStream();
      
      // Create input stream from previous command's output
      const stdin = i === 0 ? context.stdin : new StringInputStream(lastOutput);
      
      // Create a pipeline context for this command
      const pipeContext: CommandContext = {
        ...context,
        stdin,
        stdout,
        stderr: context.stderr // All errors go to the original stderr
      };
      
      // Process the command with the pipeline context
      lastExitCode = await this.processCommandWithContext(commands[i], pipeContext);
      
      // If the command failed and it's not the last one, we should stop
      if (lastExitCode !== 0 && !isLast) {
        context.stderr.writeLine(`Command '${commands[i]}' failed with exit code ${lastExitCode}`);
        return lastExitCode;
      }
      
      // Get the output from this command to pass to the next one
      if (!isLast) {
        lastOutput = (stdout as StringOutputStream).toString();
      }
    }
    
    return lastExitCode;
  }
}
