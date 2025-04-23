import { CommandModule, CommandArgs, CommandContext } from '../command-processor';
import { OS } from '../../core/os';
import { PathUtils } from '../../core/path-utils';

/**
 * diff command - Compare files line by line
 */
export class DiffCommand implements CommandModule {
  private os: OS;
  
  constructor(os: OS) {
    this.os = os;
  }
  
  public get name(): string {
    return 'diff';
  }
  
  public get description(): string {
    return 'Compare files line by line';
  }
    public get usage(): string {
    return 'diff [options] file1 file2\n' +
      '  Options:\n' +
      '    -i, --ignore-case        Ignore case differences in file contents\n' +
      '    -w, --ignore-all-space   Ignore all white space\n' +
      '    -B, --ignore-blank-lines Ignore changes where lines are all blank\n' +
      '    -q, --brief              Report only when files differ\n' +
      '    -u[N]                    Output N lines of unified context (default 3)';
  }
  
  /**
   * Execute command with context and streams
   * Returns exit code (0 for success, non-zero for error)
   */
  public async execute(args: CommandArgs, context: CommandContext): Promise<number> {
    try {
      // Check if two files are provided
      if (args.args.length !== 2) {
        context.stderr.writeLine('diff: missing operand after \'diff\'');
        context.stderr.writeLine('diff: Try \'diff --help\' for more information.');
        return 1;
      }
      
      // Parse options
      const ignoreCase = args.i || args['ignore-case'] || false; // -i, --ignore-case
      const ignoreSpaces = args.w || args['ignore-all-space'] || false; // -w, --ignore-all-space
      const ignoreBlankLines = args.B || args['ignore-blank-lines'] || false; // -B, --ignore-blank-lines
      const brief = args.q || args.brief || false; // -q, --brief
      const unifiedFormat = args.u !== undefined ? parseInt(args.u as string) || 3 : null; // -u[N]
      
      // Get file paths
      const file1 = args.args[0];
      const file2 = args.args[1];
      
      // Resolve paths
      const path1 = PathUtils.resolve(context.cwd, file1);
      const path2 = PathUtils.resolve(context.cwd, file2);
      
      // Read file contents
      let content1: string;
      let content2: string;
      
      try {
        content1 = await this.os.getFileSystem().readFile(path1);
      } catch (error) {
        context.stderr.writeLine(`diff: ${file1}: ${error instanceof Error ? error.message : String(error)}`);
        return 2;
      }
      
      try {
        content2 = await this.os.getFileSystem().readFile(path2);
      } catch (error) {
        context.stderr.writeLine(`diff: ${file2}: ${error instanceof Error ? error.message : String(error)}`);
        return 2;
      }
      
      // Split into lines
      let lines1 = content1.split('\n');
      let lines2 = content2.split('\n');
      
      // Apply preprocessing based on options
      if (ignoreCase) {
        lines1 = lines1.map(line => line.toLowerCase());
        lines2 = lines2.map(line => line.toLowerCase());
      }
      
      if (ignoreSpaces) {
        lines1 = lines1.map(line => line.replace(/\s+/g, ' ').trim());
        lines2 = lines2.map(line => line.replace(/\s+/g, ' ').trim());
      }
      
      if (ignoreBlankLines) {
        lines1 = lines1.filter(line => line.trim() !== '');
        lines2 = lines2.filter(line => line.trim() !== '');
      }
      
      // Calculate differences
      const diffs = this.findDifferences(lines1, lines2);
      
      // Check if files are identical
      if (diffs.length === 0) {
        return 0; // Exit code 0 means no differences
      }
      
      // Brief output (just report if files differ)
      if (brief) {
        context.stdout.writeLine(`Files ${file1} and ${file2} differ`);
        return 1;
      }
      
      // Format and display differences
      if (unifiedFormat !== null) {
        this.outputUnifiedDiff(file1, file2, lines1, lines2, diffs, unifiedFormat, context);
      } else {
        this.outputNormalDiff(file1, file2, lines1, lines2, diffs, context);
      }
      
      return 1; // Exit code 1 means differences were found
    } catch (error) {
      context.stderr.writeLine(`diff: error: ${error instanceof Error ? error.message : String(error)}`);
      return 2; // Exit code 2 means trouble
    }
  }
  
  /**
   * Find differences between two arrays of lines
   */
  private findDifferences(lines1: string[], lines2: string[]): Array<{
    type: 'add' | 'remove' | 'change';
    start1: number;
    end1: number;
    start2: number;
    end2: number;
  }> {
    const lcs = this.longestCommonSubsequence(lines1, lines2);
    const diffs: Array<{
      type: 'add' | 'remove' | 'change';
      start1: number;
      end1: number;
      start2: number;
      end2: number;
    }> = [];
    
    let i = 0, j = 0, k = 0;
    
    while (i < lines1.length || j < lines2.length) {
      // Skip matching parts
      if (i < lines1.length && j < lines2.length && k < lcs.length && lines1[i] === lcs[k] && lines2[j] === lcs[k]) {
        i++;
        j++;
        k++;
        continue;
      }
      
      // Track where the difference starts
      const startI = i;
      const startJ = j;
      
      // Skip lines from first file until we find a match or end
      while (i < lines1.length && (k >= lcs.length || lines1[i] !== lcs[k])) {
        i++;
      }
      
      // Skip lines from second file until we find a match or end
      while (j < lines2.length && (k >= lcs.length || lines2[j] !== lcs[k])) {
        j++;
      }
      
      // Record the difference
      if (startI < i || startJ < j) {
        if (startI < i && startJ < j) {
          // Changed lines
          diffs.push({
            type: 'change',
            start1: startI,
            end1: i - 1,
            start2: startJ,
            end2: j - 1
          });
        } else if (startI < i) {
          // Removed lines
          diffs.push({
            type: 'remove',
            start1: startI,
            end1: i - 1,
            start2: startJ,
            end2: startJ - 1
          });
        } else if (startJ < j) {
          // Added lines
          diffs.push({
            type: 'add',
            start1: startI,
            end1: startI - 1,
            start2: startJ,
            end2: j - 1
          });
        }
      }
    }
    
    return diffs;
  }
  
  /**
   * Find the longest common subsequence of two arrays
   */
  private longestCommonSubsequence(a: string[], b: string[]): string[] {
    // Create LCS table
    const m = a.length;
    const n = b.length;
    const dp: number[][] = Array(m + 1).fill(0).map(() => Array(n + 1).fill(0));
    
    // Fill the table
    for (let i = 1; i <= m; i++) {
      for (let j = 1; j <= n; j++) {
        if (a[i - 1] === b[j - 1]) {
          dp[i][j] = dp[i - 1][j - 1] + 1;
        } else {
          dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
        }
      }
    }
    
    // Reconstruct the LCS
    const lcs: string[] = [];
    let i = m, j = n;
    
    while (i > 0 && j > 0) {
      if (a[i - 1] === b[j - 1]) {
        lcs.unshift(a[i - 1]);
        i--;
        j--;
      } else if (dp[i - 1][j] > dp[i][j - 1]) {
        i--;
      } else {
        j--;
      }
    }
    
    return lcs;
  }
  
  /**
   * Output differences in normal format
   */
  private outputNormalDiff(
    file1: string,
    file2: string,
    lines1: string[],
    lines2: string[],
    diffs: Array<{
      type: 'add' | 'remove' | 'change';
      start1: number;
      end1: number;
      start2: number;
      end2: number;
    }>,
    context: CommandContext
  ): void {
    for (const diff of diffs) {
      switch (diff.type) {
        case 'add':
          context.stdout.writeLine(`${diff.start1}a${diff.start2 + 1},${diff.end2 + 1}`);
          for (let i = diff.start2; i <= diff.end2; i++) {
            context.stdout.writeLine(`> ${lines2[i]}`);
          }
          break;
          
        case 'remove':
          context.stdout.writeLine(`${diff.start1 + 1},${diff.end1 + 1}d${diff.start2}`);
          for (let i = diff.start1; i <= diff.end1; i++) {
            context.stdout.writeLine(`< ${lines1[i]}`);
          }
          break;
          
        case 'change':
          context.stdout.writeLine(`${diff.start1 + 1},${diff.end1 + 1}c${diff.start2 + 1},${diff.end2 + 1}`);
          for (let i = diff.start1; i <= diff.end1; i++) {
            context.stdout.writeLine(`< ${lines1[i]}`);
          }
          context.stdout.writeLine('---');
          for (let i = diff.start2; i <= diff.end2; i++) {
            context.stdout.writeLine(`> ${lines2[i]}`);
          }
          break;
      }
    }
  }
  
  /**
   * Output differences in unified format
   */
  private outputUnifiedDiff(
    file1: string,
    file2: string,
    lines1: string[],
    lines2: string[],
    diffs: Array<{
      type: 'add' | 'remove' | 'change';
      start1: number;
      end1: number;
      start2: number;
      end2: number;
    }>,
    context: number,
    commandContext: CommandContext
  ): void {
    // Add timestamps (not real, but simulated)
    const now = new Date();
    const timestamp = now.toISOString();
    
    // Print header
    commandContext.stdout.writeLine(`--- ${file1}\t${timestamp}`);
    commandContext.stdout.writeLine(`+++ ${file2}\t${timestamp}`);
    
    // Print hunks
    let lastPrintedLine1 = -1;
    let lastPrintedLine2 = -1;
    
    for (let diffIndex = 0; diffIndex < diffs.length; diffIndex++) {
      const diff = diffs[diffIndex];
      
      // Determine hunk boundaries with context
      let hunkStart1 = Math.max(0, diff.start1 - context);
      let hunkEnd1 = Math.min(lines1.length - 1, diff.end1 + context);
      let hunkStart2 = Math.max(0, diff.start2 - context);
      let hunkEnd2 = Math.min(lines2.length - 1, diff.end2 + context);
      
      // Merge with adjacent hunks if they overlap
      for (let i = diffIndex + 1; i < diffs.length; i++) {
        const nextDiff = diffs[i];
        const nextHunkStart1 = Math.max(0, nextDiff.start1 - context);
        
        if (nextHunkStart1 <= hunkEnd1 + 1) {
          hunkEnd1 = Math.min(lines1.length - 1, nextDiff.end1 + context);
          hunkEnd2 = Math.min(lines2.length - 1, nextDiff.end2 + context);
          diffIndex = i;
        } else {
          break;
        }
      }
      
      // Skip if this hunk was already printed
      if (hunkStart1 <= lastPrintedLine1 && hunkStart2 <= lastPrintedLine2) {
        continue;
      }
      
      // Update last printed lines
      lastPrintedLine1 = hunkEnd1;
      lastPrintedLine2 = hunkEnd2;
      
      // Calculate hunk size
      const hunkSize1 = hunkEnd1 - hunkStart1 + 1;
      const hunkSize2 = hunkEnd2 - hunkStart2 + 1;
      
      // Print hunk header
      commandContext.stdout.writeLine(`@@ -${hunkStart1 + 1},${hunkSize1} +${hunkStart2 + 1},${hunkSize2} @@`);
      
      // Determine which lines to print
      const toPrint = new Map<number, { line: string; prefix: string }>();
      
      // Add context lines before
      for (let i = hunkStart1; i < diff.start1; i++) {
        toPrint.set(i, { line: lines1[i], prefix: ' ' });
      }
      
      // Add removed lines
      for (let i = diff.start1; i <= diff.end1; i++) {
        toPrint.set(i, { line: lines1[i], prefix: '-' });
      }
      
      // Add context lines after
      for (let i = diff.end1 + 1; i <= hunkEnd1; i++) {
        if (!toPrint.has(i)) {
          toPrint.set(i, { line: lines1[i], prefix: ' ' });
        }
      }
      
      // Print added lines interleaved
      let addedIndex = diff.start2;
      
      for (let i = hunkStart1; i <= hunkEnd1; i++) {
        // Print current line from file1
        if (toPrint.has(i)) {
          const { line, prefix } = toPrint.get(i)!;
          commandContext.stdout.writeLine(`${prefix}${line}`);
        }
        
        // If we're at the diff position, print added lines
        if (i === diff.start1) {
          for (let j = diff.start2; j <= diff.end2; j++) {
            commandContext.stdout.writeLine(`+${lines2[j]}`);
            addedIndex++;
          }
        }
      }
      
      // Print any remaining added lines at the end of the hunk
      while (addedIndex <= hunkEnd2) {
        commandContext.stdout.writeLine(`+${lines2[addedIndex]}`);
        addedIndex++;
      }
    }
  }
}
