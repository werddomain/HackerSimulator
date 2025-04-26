/**
 * Shared type definitions for window management
 */

/**
 * Interface for window properties
 */
export interface WindowOptions {
  title: string;
  width: number;
  height: number;
  x?: number;
  y?: number;
  resizable?: boolean;
  minimizable?: boolean;
  maximizable?: boolean;
  closable?: boolean;
  appId: string;
  icon?: string;
  processId?: number;
}

/**
 * Interface for notification options
 */
export interface NotificationOptions {
  title: string;
  message: string;
  type?: 'info' | 'success' | 'warning' | 'error';
}

/**
 * Interface for prompt dialog options
 */
export interface PromptOptions {
  title: string;
  message: string;
  defaultValue?: string;
  placeholder?: string;
}

/**
 * Interface for confirmation dialog options
 */
export interface ConfirmOptions {
  title: string;
  message: string;
  okText?: string;
  cancelText?: string;
}
