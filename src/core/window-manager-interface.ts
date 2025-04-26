/**
 * Window Manager Interfaces
 * Defines common interfaces for window management across platforms
 */

import { WindowOptions } from './window-types';

/**
 * Common interface for window managers across platforms
 */
export interface IWindowManager {
  /**
   * Initialize the window manager
   */
  init(): void;
  
  /**
   * Create a new window
   * @param options Window options
   * @returns Window ID
   */
  createWindow(options: WindowOptions): string;
  
  /**
   * Close a window
   * @param windowId Window ID
   */
  closeWindow(windowId: string): void;
  
  /**
   * Get window element
   * @param windowId Window ID
   * @returns Window element or null if not found
   */
  getWindowElement(windowId: string): HTMLElement | null;
  
  /**
   * Get window options
   * @param windowId Window ID
   * @returns Window options or null if not found
   */
  getWindowOptions(windowId: string): WindowOptions | null;
  
  /**
   * Minimize a window
   * @param windowId Window ID
   */
  minimizeWindow(windowId: string): void;
  
  /**
   * Maximize a window
   * @param windowId Window ID
   */
  maximizeWindow(windowId: string): void;
  
  /**
   * Restore a window (from minimized or maximized state)
   * @param windowId Window ID
   */
  restoreWindow(windowId: string): void;
  
  /**
   * Activate a window (bring to front)
   * @param windowId Window ID
   */
  activateWindow(windowId: string): void;
  
  /**
   * Check if a window is minimized
   * @param windowId Window ID
   * @returns True if window is minimized
   */
  isWindowMinimized(windowId: string): boolean;
  
  /**
   * Check if a window is maximized
   * @param windowId Window ID
   * @returns True if window is maximized
   */
  isWindowMaximized(windowId: string): boolean;
  
  /**
   * Get active window ID
   * @returns Active window ID or null if no window is active
   */
  getActiveWindowId(): string | null;
  
  /**
   * Close all windows
   */
  closeAllWindows(): void;
  
  /**
   * Set window content
   * @param windowId Window ID
   * @param content Window content
   */
  setWindowContent(windowId: string, content: HTMLElement | string): void;
  
  /**
   * Update window title
   * @param windowId Window ID
   * @param title New window title
   */
  updateWindowTitle(windowId: string, title: string): void;
  
  /**
   * Show a notification to the user
   * @param options Notification options
   */
  showNotification(options: { title: string; message: string; type?: 'info' | 'success' | 'warning' | 'error' }): void;
  
  /**
   * Show a prompt dialog to get user input
   * @param options Prompt options
   * @param callback Function to call with the user's input
   */
  showPrompt(options: { title: string; message: string; defaultValue?: string; placeholder?: string }, callback: (value: string | null) => void): void;
  
  /**
   * Show a confirmation dialog to get user approval
   * @param options Confirmation dialog options
   * @param callback Function to call with the user's decision
   */
  showConfirm(options: { title: string; message: string; okText?: string; cancelText?: string }, callback: (confirmed: boolean) => void): void;
}
