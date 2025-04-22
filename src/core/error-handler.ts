import { OS } from './os';

/**
 * Error levels for error logging
 */
export enum ErrorLevel {
    INFO = 'info',
    WARNING = 'warning',
    ERROR = 'error',
    CRITICAL = 'critical'
}

/**
 * Interface for error log entries
 */
export interface ErrorLogEntry {
    id: string;
    timestamp: number;
    level: ErrorLevel;
    message: string;
    fileName: string;
    lineNumber?: number;
    columnNumber?: number;
    stack?: string;
    componentName?: string;
    handled: boolean;
}

/**
 * Interface for error handling settings
 */
export interface ErrorHandlingSettings {
    enableErrorReporting: boolean;
    enableTelemetry: boolean;
    maxStoredErrors: number;
    logToConsole: boolean;
}

/**
 * Error Handler Service for the Hacker Game
 * Provides centralized error handling, logging, and reporting
 */
export class ErrorHandler {
    private static instance: ErrorHandler;
    private errorLog: ErrorLogEntry[] = [];
    private os: OS | null = null;
    private settings: ErrorHandlingSettings = {
        enableErrorReporting: true,
        enableTelemetry: false,
        maxStoredErrors: 100,
        logToConsole: true
    };

    private constructor() {
        // Initialize error handling
        this.setupGlobalErrorHandler();
    }

    /**
     * Get the singleton instance of ErrorHandler
     */
    public static getInstance(os: OS): ErrorHandler {
        if (!ErrorHandler.instance) {
            ErrorHandler.instance = new ErrorHandler();
            ErrorHandler.instance.setOS(os);
        }
        return ErrorHandler.instance;
    }

    /**
     * Set the OS instance for filesystem access
     */
    public setOS(os: OS): void {
        this.os = os;
        this.loadSettings();
    }

    /**
     * Setup global error handler
     */
    private setupGlobalErrorHandler(): void {
        window.addEventListener('error', (event) => {
            this.handleError(
                ErrorLevel.ERROR,
                event.error?.message || event.message,
                event.filename,
                event.lineno,
                event.colno,
                event.error?.stack
            );
            return false;
        });

        window.addEventListener('unhandledrejection', (event) => {
            let message = 'Promise rejection';
            if (event.reason instanceof Error) {
                message = event.reason.message;
                this.handleError(
                    ErrorLevel.ERROR,
                    message,
                    'unknown',
                    undefined,
                    undefined,
                    event.reason.stack
                );
            } else {
                this.handleError(
                    ErrorLevel.ERROR,
                    message,
                    'unknown',
                    undefined,
                    undefined,
                    `Unhandled rejection: ${JSON.stringify(event.reason)}`
                );
            }
        });
    }

    /**
     * Load error handling settings from the OS
     */
    private async loadSettings(): Promise<void> {
        if (!this.os) return;

        try {
            const userSettings = this.os.getUserSettings();
            if (userSettings) {
                this.settings.enableErrorReporting = await userSettings.get('errorReporting', 'enabled', true);
                this.settings.enableTelemetry = await userSettings.get('errorReporting', 'telemetry', true);
                this.settings.maxStoredErrors = await userSettings.get('errorReporting', 'maxErrors', 100);
                this.settings.logToConsole = await userSettings.get('errorReporting', 'logToConsole', true);
            }
        } catch (error) {
            console.error('Failed to load error handling settings:', error);
        }
    }

    /**
     * Handle an error
     */
    public handleError(
        level: ErrorLevel,
        message: string,
        fileName: string,
        lineNumber?: number,
        columnNumber?: number,
        stack?: string,
        componentName?: string
    ): ErrorLogEntry {
        if (!this.settings.enableErrorReporting) {
            // Only log to console if reporting is disabled but console logging is enabled
            if (this.settings.logToConsole) {
                console.error(`[${level}] ${message}`, { fileName, lineNumber, columnNumber, stack });
            }
            return {
                id: this.generateErrorId(),
                timestamp: Date.now(),
                level,
                message,
                fileName,
                lineNumber,
                columnNumber,
                stack,
                componentName,
                handled: false
            };
        }

        const errorEntry: ErrorLogEntry = {
            id: this.generateErrorId(),
            timestamp: Date.now(),
            level,
            message,
            fileName,
            lineNumber,
            columnNumber,
            stack,
            componentName,
            handled: false
        };

        // Add to error log
        this.errorLog.push(errorEntry);

        // Truncate log if it exceeds max size
        if (this.errorLog.length > this.settings.maxStoredErrors) {
            this.errorLog = this.errorLog.slice(-this.settings.maxStoredErrors);
        }

        // Log to console if enabled
        if (this.settings.logToConsole) {
            console.error(`[${level}] ${message}`, { fileName, lineNumber, columnNumber, stack });
        }

        // Send telemetry if enabled
        if (this.settings.enableTelemetry) {
            this.sendTelemetry(errorEntry);
        }

        // Save the error log
        this.saveErrorLog();

        return errorEntry;
    }

    /**
     * Log an info message
     */
    public logInfo(message: string, componentName?: string): ErrorLogEntry {
        return this.handleError(ErrorLevel.INFO, message, 'info', undefined, undefined, undefined, componentName);
    }

    /**
     * Log a warning message
     */
    public logWarning(message: string, fileName: string, lineNumber?: number, columnNumber?: number, stack?: string, componentName?: string): ErrorLogEntry {
        return this.handleError(ErrorLevel.WARNING, message, fileName, lineNumber, columnNumber, stack, componentName);
    }

    /**
     * Log an error message
     */
    public logError(error: Error, fileName: string, componentName?: string): ErrorLogEntry {
        return this.handleError(
            ErrorLevel.ERROR,
            error.message,
            fileName,
            undefined,
            undefined,
            error.stack,
            componentName
        );
    }    /**
     * Log a critical error message
     */
    public logCritical(error: Error, fileName: string, componentName?: string): ErrorLogEntry {
        return this.handleError(
            ErrorLevel.CRITICAL,
            error.message,
            fileName,
            undefined,
            undefined,
            error.stack,
            componentName
        );
    }    /**
     * Parse, log, and handle an error from a try/catch block
     * This method provides a convenient way to process errors caught in try/catch blocks.
     * It will:
     * 1. Log the error to the console
     * 2. Add the error to the error log
     * 3. Save the error to the appropriate log file
     * 4. Optionally display an error dialog
     * 
     * @param error The error object from catch block
     * @param fileName The source file where the error occurred
     * @param componentName Optional component name for context
     * @param options Configuration options for error handling
     * @returns The created error log entry
     */
    public parse(
        error: unknown, 
        fileName: string, 
        componentName?: string, 
        options: {
            level?: ErrorLevel,
            showDialog?: boolean,
            dialogManager?: any,
            dialogTitle?: string
        } = {}
    ): ErrorLogEntry {
        // Log the raw error to console for immediate visibility during debugging
        console.error(`[ErrorHandler.parse] Error in ${fileName}${componentName ? ` (${componentName})` : ''}:`, error);
        
        // Default options
        const level = options.level || ErrorLevel.ERROR;
        const showDialog = options.showDialog !== undefined ? options.showDialog : this.settings.enableErrorReporting;
        
        // Extract error information
        let message: string;
        let stack: string | undefined;
        
        if (error instanceof Error) {
            message = error.message;
            stack = error.stack;
        } else if (typeof error === 'string') {
            message = error;
        } else if (typeof error === 'object' && error !== null) {
            message = (error as any).message || JSON.stringify(error);
        } else {
            message = 'Unknown error';
        }
        
        // Log the error
        const errorEntry = this.handleError(
            level,
            message,
            fileName,
            undefined,
            undefined,
            stack,
            componentName
        );
        
        // Show error dialog if requested and if dialog manager is provided
        if (showDialog && options.dialogManager) {
            const title = options.dialogTitle || 'Error';
            options.dialogManager.Msgbox.Show(title, message, ['ok'], 'error');
        }
        
        return errorEntry;
    }

    /**
     * Mark an error as handled
     */
    public markAsHandled(errorId: string): void {
        const errorEntry = this.errorLog.find(entry => entry.id === errorId);
        if (errorEntry) {
            errorEntry.handled = true;
            this.saveErrorLog();
        }
    }

    /**
     * Get all error logs
     */
    public getErrorLog(): ErrorLogEntry[] {
        return [...this.errorLog];
    }

    /**
     * Clear all error logs
     */
    public clearErrorLog(): void {
        this.errorLog = [];
        this.saveErrorLog();
    }

    /**
     * Update error handling settings
     */
    public async updateSettings(settings: Partial<ErrorHandlingSettings>): Promise<void> {
        this.settings = { ...this.settings, ...settings };

        if (this.os) {
            const userSettings = this.os.getUserSettings();
            if (userSettings) {
                await Promise.all([
                    userSettings.set('errorReporting', 'enabled', this.settings.enableErrorReporting),
                    userSettings.set('errorReporting', 'telemetry', this.settings.enableTelemetry),
                    userSettings.set('errorReporting', 'maxErrors', this.settings.maxStoredErrors),
                    userSettings.set('errorReporting', 'logToConsole', this.settings.logToConsole)
                ]);


            }
        }
    }

    /**
     * Get current error handling settings
     */
    public getSettings(): ErrorHandlingSettings {
        return { ...this.settings };
    }

    /**
     * Generate a unique error ID
     */
    private generateErrorId(): string {
        return Date.now().toString(36) + Math.random().toString(36).substr(2, 5);
    }    /**
     * Root folder for storing logs
     */
    public static readonly LOG_FOLDER_ROOT = '/system/logs';

    /**
     * Get the folder path for current day's logs
     */
    private getCurrentDayFolder(): string {
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        return `${ErrorHandler.LOG_FOLDER_ROOT}/${year}${month}${day}`;
    }

    /**
     * Get the file name for the current hour's log
     */
    private getCurrentHourLogFile(): string {
        const now = new Date();
        const hour = String(now.getHours()).padStart(2, '0');
        return `${this.getCurrentDayFolder()}/errors_${hour}.json`;
    }

    /**
     * Get the index file path that tracks all log files
     */
    private getLogIndexFile(): string {
        return `${ErrorHandler.LOG_FOLDER_ROOT}/log_index.json`;
    }

    /**
     * Ensure the log directory structure exists
     */
    private async ensureLogDirectories(): Promise<void> {
        if (!this.os) return;
        
        try {
            // Create root logs directory if it doesn't exist
            const rootExists = await this.os.getFileSystem().exists(ErrorHandler.LOG_FOLDER_ROOT);
            if (!rootExists) {
                await this.os.getFileSystem().createDirectory(ErrorHandler.LOG_FOLDER_ROOT);
            }
            
            // Create current day directory if it doesn't exist
            const dayFolder = this.getCurrentDayFolder();
            const dayFolderExists = await this.os.getFileSystem().exists(dayFolder);
            if (!dayFolderExists) {
                await this.os.getFileSystem().createDirectory(dayFolder);
            }
        } catch (error) {
            console.error('Failed to create log directories:', error);
        }
    }
    public static getErrorMessage(error: Error | unknown): string {
        if (error instanceof Error) {
            return error.message;
        } else if (typeof error === 'string') {
            return error;
        } else if (typeof error === 'object' && error !== null) {
            return (<any>error).message || JSON.stringify(error);
        } else {
            return 'Unknown error';
        }

    }
    /**
     * Update the log index file with a new log file
     */
    private async updateLogIndex(logFilePath: string): Promise<void> {
        if (!this.os) return;
        
        try {
            const indexFile = this.getLogIndexFile();
            const indexExists = await this.os.getFileSystem().exists(indexFile);
            
            let logFiles: string[] = [];
            
            // Load existing index if it exists
            if (indexExists) {
                const indexContent = await this.os.getFileSystem().readFile(indexFile);
                try {
                    logFiles = JSON.parse(indexContent);
                    if (!Array.isArray(logFiles)) {
                        logFiles = [];
                    }
                } catch (error) {
                    console.error('Failed to parse log index file:', error);
                    logFiles = [];
                }
            }
            
            // Add the new log file if it's not already in the index
            if (!logFiles.includes(logFilePath)) {
                logFiles.push(logFilePath);
                await this.os.getFileSystem().writeFile(indexFile, JSON.stringify(logFiles));
            }
        } catch (error) {
            console.error('Failed to update log index:', error);
        }
    }

    /**
     * Save current in-memory error logs to the appropriate hourly file
     */
    private async saveErrorLog(): Promise<void> {
        if (!this.os || this.errorLog.length === 0) return;

        try {
            // Ensure log directories exist
            await this.ensureLogDirectories();
            
            // Get current log file path
            const logFilePath = this.getCurrentHourLogFile();
            
            // Load existing logs for this hour if they exist
            const fileExists = await this.os.getFileSystem().exists(logFilePath);
            let hourlyLogs: ErrorLogEntry[] = [];
            
            if (fileExists) {
                const content = await this.os.getFileSystem().readFile(logFilePath);
                try {
                    hourlyLogs = JSON.parse(content);
                    if (!Array.isArray(hourlyLogs)) {
                        hourlyLogs = [];
                    }
                } catch (error) {
                    console.error('Failed to parse hourly log file:', error);
                }
            }
            
            // Find which logs need to be added (those not already in the hour file)
            const existingIds = new Set(hourlyLogs.map(log => log.id));
            const newLogs = this.errorLog.filter(log => !existingIds.has(log.id));
            
            // If we have new logs, add them and save the file
            if (newLogs.length > 0) {
                const updatedLogs = [...hourlyLogs, ...newLogs];
                await this.os.getFileSystem().writeFile(logFilePath, JSON.stringify(updatedLogs));
                
                // Update the index file with this log file
                await this.updateLogIndex(logFilePath);
            }
        } catch (error) {
            console.error('Failed to save error log:', error);
        }
    }

    /**
     * Load all error logs from the filesystem
     */
    public async loadErrorLog(): Promise<void> {
        if (!this.os) {
            return Promise.reject(new Error('OS not initialized'));
        }
        
        try {
            // Ensure log directories exist
            await this.ensureLogDirectories();
            
            // Check if index file exists
            const indexFile = this.getLogIndexFile();
            const indexExists = await this.os.getFileSystem().exists(indexFile);
            
            if (!indexExists) {
                // No logs exist yet
                this.errorLog = [];
                return;
            }
            
            // Load the log index
            const indexContent = await this.os.getFileSystem().readFile(indexFile);
            let logFiles: string[] = [];
            
            try {
                logFiles = JSON.parse(indexContent);
                if (!Array.isArray(logFiles)) {
                    logFiles = [];
                }
            } catch (error) {
                console.error('Failed to parse log index:', error);
                return;
            }
            
            // Load all log files
            this.errorLog = [];
            
            for (const logFile of logFiles) {
                try {
                    const fileExists = await this.os.getFileSystem().exists(logFile);
                    if (fileExists) {
                        const content = await this.os.getFileSystem().readFile(logFile);
                        const logs = JSON.parse(content) as ErrorLogEntry[];
                        
                        if (Array.isArray(logs)) {
                            this.errorLog.push(...logs);
                        }
                    }
                } catch (error) {
                    console.error(`Failed to load log file ${logFile}:`, error);
                }
            }
            
            // Sort logs by timestamp (newest first)
            this.errorLog.sort((a, b) => b.timestamp - a.timestamp);
            
            // Limit to max number of stored errors
            if (this.errorLog.length > this.settings.maxStoredErrors) {
                this.errorLog = this.errorLog.slice(0, this.settings.maxStoredErrors);
            }
        } catch (error) {
            console.error('Failed to load error logs:', error);
        }
    }

    /**
     * Send telemetry data
     */
    private sendTelemetry(errorEntry: ErrorLogEntry): void {
        // In a real application, this would send data to a telemetry service
        // For the simulation, we'll just log to the console
        if (this.settings.enableTelemetry) {
            console.log('[Telemetry] Sending error data:', {
                level: errorEntry.level,
                message: errorEntry.message,
                fileName: errorEntry.fileName,
                timestamp: errorEntry.timestamp
            });
        }
    }
}
