import { Laugh } from 'lucide';
import { OS } from '../core/os';
import { NanoEditor } from './app/nano-editor';
import { CommandModule } from './command-processor';

// Import all commands
import { CatCommand } from './linux/cat';
import { CdCommand } from './linux/cd';
import { ClearCommand } from './linux/clear';
import { CpCommand } from './linux/cp';
import { CurlCommand } from './linux/curl';
import { EchoCommand } from './linux/echo';
import { HelpCommand } from './linux/help';
import { KillCommand } from './linux/kill';
import { LsCommand } from './linux/ls';
import { ManCommand } from './linux/man';
import { MkdirCommand } from './linux/mkdir';
import { MvCommand } from './linux/mv';

import { NmapCommand } from './linux/nmap';
import { PingCommand } from './linux/ping';
import { PsCommand } from './linux/ps';
import { PwdCommand } from './linux/pwd';
import { RmCommand } from './linux/rm';
import { TouchCommand } from './linux/touch';
import { LaunchCommand } from './linux/launch';

/**
 * CommandRegistry class for registering and retrieving command modules
 */
export class CommandRegistry {
  private static instance: CommandRegistry;
  private os: OS;
  private commands: Map<string, CommandModule> = new Map();
  private aliases: Map<string, string> = new Map();

  private constructor(os: OS) {
    this.os = os;
  }

  /**
   * Get the singleton instance of CommandRegistry
   */
  public static getInstance(os: OS): CommandRegistry {
    if (!CommandRegistry.instance) {
      CommandRegistry.instance = new CommandRegistry(os);
    }
    return CommandRegistry.instance;
  }

  /**
   * Register a command module
   */
  public registerCommand(command: CommandModule): void {
    this.commands.set(command.name, command);
    this.os.getCommandProcessor().registerCommand(command);
  }

  /**
   * Register a command alias
   */
  public registerAlias(alias: string, commandName: string): void {
    this.aliases.set(alias, commandName);
    this.os.getCommandProcessor().registerAlias(alias, commandName);
  }

  /**
   * Register all built-in commands
   */
  public registerBuiltInCommands(): void {
    // Register navigation commands
    this.registerCommand(new LsCommand(this.os));
    this.registerCommand(new PwdCommand(this.os));

    // Register file operation commands
    this.registerCommand(new CatCommand(this.os));
    this.registerCommand(new CpCommand(this.os));
    this.registerCommand(new MvCommand(this.os));
    this.registerCommand(new RmCommand(this.os));
    this.registerCommand(new MkdirCommand(this.os));
    this.registerCommand(new TouchCommand(this.os));
    this.registerCommand(new CdCommand(this.os));

    // Register system commands
    this.registerCommand(new EchoCommand());
    this.registerCommand(new PsCommand(this.os));
    this.registerCommand(new KillCommand(this.os));
    this.registerCommand(new ClearCommand());
    // Register network commands
    this.registerCommand(new PingCommand(this.os));
    this.registerCommand(new CurlCommand(this.os)); this.registerCommand(new NmapCommand(this.os));

    // Register help and utility commands
    this.registerCommand(new HelpCommand(this.os));
    this.registerCommand(new ManCommand(this.os));
    this.registerCommand(new NanoEditor(this.os));
    this.registerCommand(new LaunchCommand(this.os));

    // Register common aliases
    this.registerAlias('dir', 'ls');
    this.registerAlias('ll', 'ls -l');
    this.registerAlias('la', 'ls -la');
    this.registerAlias('cls', 'clear');
    this.registerAlias('?', 'help');
    this.registerAlias('h', 'help');
    this.registerAlias("start", "launch");
  }

  /**
   * Dynamically load a command from a string
   * This allows creating and loading commands at runtime
   */
  public async loadCommandFromString(name: string, code: string): Promise<boolean> {
    try {
      // Create a dynamic module from the string
      const moduleFunc = new Function('OS', 'CommandModule', 'CommandArgs', `
        ${code}
        return new ${name}Command(OS);
      `);

      // Execute the function to get the command instance
      const command = moduleFunc(this.os, {}, {}) as CommandModule;

      // Register the command
      this.registerCommand(command);
      return true;
    } catch (error) {
      console.error(`Error loading dynamic command ${name}:`, error);
      return false;
    }
  }

  /**
   * Get all registered commands
   */
  public getAllCommands(): CommandModule[] {
    return Array.from(this.commands.values());
  }

  /**
   * Get a command by name
   */
  public getCommand(name: string): CommandModule | undefined {
    return this.commands.get(name);
  }

  /**
   * Check if a command exists
   */
  public hasCommand(name: string): boolean {
    return this.commands.has(name) || this.aliases.has(name);
  }
}
