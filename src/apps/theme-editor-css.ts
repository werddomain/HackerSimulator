/**
 * Theme Editor CSS Helper
 * Used by the theme-editor.ts to manage custom CSS editing
 */
import { FileSystem } from '../core/filesystem';
import { OS } from '../core/os';
import { notification } from '../core/components/notification';

/**
 * Class to help with theme CSS editing functionality
 */
export class ThemeCssEditor {
    private os: OS;
    private tempDir: string = '/tmp/theme-editor';
    
    constructor(os: OS) {
        this.os = os;
    }
    
    /**
     * Create the temporary directory for CSS editing
     */
    public async ensureTempDirectory(): Promise<boolean> {
        try {
            // Check if directory exists
            const dirExists = await this.os.getFileSystem().exists(this.tempDir);
            if (!dirExists) {
                // Create directory
                await this.os.getFileSystem().writeFile(this.tempDir, '', true);
            }
            return true;
        } catch (error) {
            console.error('Error creating temp directory:', error);
            return false;
        }
    }
    
    /**
     * Create a CSS file for editing
     * @param property The property to edit (titleBar, taskbar, startMenu)
     * @param cssContent The current CSS content
     * @returns The path to the CSS file, or null if creation failed
     */
    public async createCssFile(property: string, cssContent: string): Promise<string | null> {
        try {
            // Ensure temp directory exists
            const success = await this.ensureTempDirectory();
            if (!success) {
                throw new Error('Failed to create temporary directory');
            }
            
            // Create filename
            const filename = `${property.toLowerCase()}-custom.css`;
            const filePath = `${this.tempDir}/${filename}`;
            
            // Write CSS content to file
            await this.os.getFileSystem().writeFile(filePath, cssContent || '');
            
            return filePath;
        } catch (error) {
            console.error('Error creating CSS file:', error);
            return null;
        }
    }
    
    /**
     * Read the updated CSS content from a file
     * @param filePath The path to the CSS file
     * @returns The CSS content, or null if reading failed
     */
    public async readCssFile(filePath: string): Promise<string | null> {
        try {
            const content = await this.os.getFileSystem().readFile(filePath);
            return content;
        } catch (error) {
            console.error('Error reading CSS file:', error);
            return null;
        }
    }
    
    /**
     * Launch the code editor app with the CSS file
     * @param filePath The path to the CSS file
     * @param callback Function to call when editing is complete
     */
    public async launchEditor(filePath: string, callback: (updatedCss: string | null) => void): Promise<void> {
        try {
            // Import necessary modules - using a different approach that doesn't rely on dialog
            // Show a message to the user
            notification.info(`Launching code editor for CSS editing. The file will be saved at ${filePath}`);
            
            // Launch the terminal app with a command to open the file in an editor
            const { TerminalApp } = await import('./terminal');
            const terminal = new TerminalApp(this.os);
            
            // Use the terminal to launch an editor with the file
            const command = `launch code-editor ${filePath}`;
            
            // Execute the terminal command directly
            terminal.executeCommand(command);
            
            // Since we can't use the dialog directly, we'll use a simpler approach
            // Wait a short time then read the file
            setTimeout(async () => {
                try {
                    // Read the updated CSS content
                    const updatedCss = await this.readCssFile(filePath);
                    callback(updatedCss);
                } catch (err: any) {
                    console.error('Error reading CSS file:', err);
                    callback(null);
                }
            }, 10000); // Give time for editor to be used
        } catch (error: any) {
            console.error('Error launching editor:', error);
            notification.error('Failed to launch code editor');
            callback(null);
        }
    }
}
