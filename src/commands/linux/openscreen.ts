import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';

export class OpenScreenCommand implements CommandModule {
  private os: OS;

  constructor(os: OS) {
    this.os = os;
  }

  public get name(): string {
    return 'openscreen';
  }

  public get description(): string {
    return 'Open a secondary monitor window';
  }

  public get usage(): string {
    return 'openscreen [monitorId]';
  }

  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    const id = args.args.length > 0 ? parseInt(args.args[0], 10) : 2;
    this.os.getMultiMonitorManager().openMonitor(id);
    context.stdout.writeLine(`Opening monitor ${id}`);
    return 0;
  }
}
