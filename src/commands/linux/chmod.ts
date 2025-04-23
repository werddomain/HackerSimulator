import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * chmod command - Change file mode bits
 */
export class ChmodCommand implements CommandModule {
    private os: OS;

    constructor(os: OS) {
        this.os = os;
    }

    public get name(): string {
        return 'chmod';
    }

    public get description(): string {
        return 'Change file mode bits';
    }
    public get usage(): string {
        return 'chmod [options] mode[,mode] file...\n' +
            '  Options:\n' +
            '    -r, -R, --recursive   Change files and directories recursively\n' +
            '    -v, --verbose         Output a diagnostic for every file processed\n\n' +
            '  Modes:\n' +
            '    Octal:   chmod 755 file      (rwxr-xr-x)\n' +
            '    Symbolic: chmod u+x file     (Adds execute for user)\n' +
            '             chmod go-w file     (Removes write for group and others)\n' +
            '             chmod a=r file      (Sets read-only for all)';
    }

    /**
     * Execute command with context and streams
     * Returns exit code (0 for success, non-zero for error)
     */
    public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
        try {
            // Check if mode and at least one file is provided
            if (args.args.length < 2) {
                context.stderr.writeLine('chmod: missing operand');
                context.stderr.writeLine('Try \'chmod --help\' for more information.');
                return 1;
            }

            // Parse options
            const recursive = args.r || args.R || args.recursive || false; // -r, -R, --recursive
            const verbose = args.v || args.verbose || false; // -v, --verbose

            // Get mode and files
            const mode = args.args[0];
            const files = args.args.slice(1);

            // Process each file
            let exitCode = 0;

            for (const file of files) {
                try {
                    const absolutePath = PathUtils.resolve(context.cwd, file);

                    // Check if file exists
                    const entry = await this.os.getFileSystem().exists(absolutePath);
                    if (!entry) {
                        context.stderr.writeLine(`chmod: cannot access '${file}': No such file or directory`);
                        exitCode = 1;
                        continue;
                    }

                    // Apply mode changes
                    if (recursive) {
                        await this.chmodRecursive(absolutePath, mode, verbose, context);
                    } else {
                        await this.changeMode(absolutePath, mode, verbose, context);
                    }
                } catch (error) {
                    context.stderr.writeLine(`chmod: '${file}': ${error instanceof Error ? error.message : String(error)}`);
                    exitCode = 1;
                }
            }

            return exitCode;
        } catch (error) {
            context.stderr.writeLine(`chmod: error: ${error instanceof Error ? error.message : String(error)}`);
            return 1;
        }
    }

    /**
     * Apply chmod recursively to a directory
     */
    private async chmodRecursive(
        path: string,
        mode: string,
        verbose: boolean,
        context: CommandContext
    ): Promise<void> {
        try {
            // Apply mode to this entry
            await this.changeMode(path, mode, verbose, context);

            // Check if it's a directory
            const stats = await this.os.getFileSystem().stat(path);

            if (stats.isDirectory) {
                // Get entries in the directory
                const entries = await this.os.getFileSystem().readDirectory(path);

                // Recursively apply chmod to each entry
                for (const entry of entries) {
                    const entryPath = PathUtils.join(path, entry.name);
                    await this.chmodRecursive(entryPath, mode, verbose, context);
                }
            }
        } catch (error) {
            throw error;
        }
    }
    /**
   * Change the mode of a file
   */
    private async changeMode(
        path: string,
        modeStr: string,
        verbose: boolean,
        context: CommandContext
    ): Promise<void> {
        try {
            // Parse the mode string
            let newPermissions: string;

            if (/^[0-7]{3,4}$/.test(modeStr)) {
                // Octal mode (e.g., 755)
                newPermissions = this.octalToPermissionString(parseInt(modeStr, 8));
            } else {
                // Symbolic mode (e.g., u+x)
                newPermissions = await this.parseSymbolicMode(path, modeStr);
            }

            // Apply the mode
            await this.os.getFileSystem().chmod(path, newPermissions);

            if (verbose) {
                context.stdout.writeLine(`mode of '${path}' changed to ${newPermissions}`);
            }
        } catch (error) {
            throw error;
        }
    }

    /**
     * Convert octal mode (e.g., 755) to permission string (e.g., -rwxr-xr-x)
     */
    private octalToPermissionString(octal: number): string {
        // Get the file type indicator (first character) - default to regular file
        const fileType = '-';

        // Parse the octal number to a permission string
        const userPerm = this.convertOctalDigitToPermString((octal >> 6) & 7);
        const groupPerm = this.convertOctalDigitToPermString((octal >> 3) & 7);
        const otherPerm = this.convertOctalDigitToPermString(octal & 7);

        return `${fileType}${userPerm}${groupPerm}${otherPerm}`;
    }

    /**
     * Convert a single octal digit (0-7) to a permission string (e.g., rwx)
     */
    private convertOctalDigitToPermString(digit: number): string {
        let result = '';
        result += (digit & 4) ? 'r' : '-';
        result += (digit & 2) ? 'w' : '-';
        result += (digit & 1) ? 'x' : '-';
        return result;
    }

    /**
     * Parse symbolic mode string
     */
    private async parseSymbolicMode(path: string, modeStr: string): Promise<string> {
        // Get current permissions
        const stats = await this.os.getFileSystem().stat(path);
        let permString = stats.permissions;
        if (!permString || permString === 'undefine' || permString == null || permString.length !== 10)
            permString = '----------'; // Default to no permissions if not available
        if (!permString)
            throw new Error(`Unable to retrieve permissions for ${path}`);

        // Parse symbolic mode string (e.g., u+x,g-w,o=r)
        const parts = modeStr.split(',');

        for (const part of parts) {
            // Match pattern like "u+x" or "go-rw"
            const match = part.match(/^([ugoa]*)([+-=])([rwxXst]*)$/);

            if (!match) {
                throw new Error(`Invalid mode: ${part}`);
            }

            const [_, who, operation, permissions] = match;

            // Create a permission string array for easier modification
            const permArray = <string[]>permString!.split('');

            // Determine which positions to modify
            const positions = [];
            if (who.includes('u') || who.includes('a') || !who) positions.push(1, 2, 3); // User permissions
            if (who.includes('g') || who.includes('a') || !who) positions.push(4, 5, 6); // Group permissions
            if (who.includes('o') || who.includes('a') || !who) positions.push(7, 8, 9); // Others permissions

            // Apply permissions based on operation
            for (const pos of positions) {
                const permType = (pos % 3 === 1) ? 'r' : (pos % 3 === 2) ? 'w' : 'x';

                if (operation === '+') {
                    // Add permission
                    if (permissions.includes(permType) ||
                        (permType === 'x' && permissions.includes('X') &&
                            (permString[0] === 'd' || permString.includes('x')))) {
                        permArray[pos] = permType;
                    }
                } else if (operation === '-') {
                    // Remove permission
                    if (permissions.includes(permType)) {
                        permArray[pos] = '-';
                    }
                } else if (operation === '=') {
                    // Set permission
                    permArray[pos] = permissions.includes(permType) ? permType : '-';
                }
            }

            // Update permission string
            permString = permArray.join('');
        }

        return permString;
    }
}

