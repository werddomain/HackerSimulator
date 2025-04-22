import { OS } from './os';
import { WindowManager } from './window';
import { FileSystem } from './filesystem';
import { FileEntryUtils } from './file-entry-utils';
import { GuiApplication } from './gui-application';
/**
 * Dialog result type for standard button responses
 */
export type DialogButtonResult = 'ok' | 'cancel' | 'yes' | 'no' | 'retry' | string;

/**
 * Dialog type for message styling
 */
export type DialogType = 'normal' | 'error' | 'warning' | 'information';

/**
 * HTML input types supported by the prompt dialog
 */
export type InputType = 'text' | 'password' | 'email' | 'number' | 'tel' | 'url' | 
                        'date' | 'time' | 'datetime-local' | 'month' | 'week' | 'color';

/**
 * Options for text prompt dialog
 */
export interface PromptOptions {
  placeHolder?: string;
  defaultText?: string;
  inputType?: InputType;
  min?: number | string;
  max?: number | string;
  step?: number | string;
  pattern?: string;
  required?: boolean;
}

/**
 * Options for dialog display
 */
export interface DialogShowOptions {
  /** Parent window ID to display dialog inside of (if not provided, creates a new window) */
  parentWindowId?: string;
  /** Whether to show a backdrop behind the dialog (default: true) */
  showBackdrop?: boolean;
  /** Custom width for the dialog */
  width?: number;
  /** Custom height for the dialog */
  height?: number;
  /** Whether the dialog is modal (prevents interaction with parent window) (default: true) */
  modal?: boolean;
}

/**
 * Options for file picker dialog
 */
export interface FilePickerOptions {
  message?: string;
  fileTypes?: string[];
  initialDirectory?: string;
  initialFileName?: string;
  saveMode?: boolean;
  allowMultiple?: boolean;
}

/**
 * Options for directory picker dialog
 */
export interface DirectoryPickerOptions {
  message?: string;
  initialDirectory?: string;
}

/**
 * Base class for all dialogs
 */
export class BaseDialog {  protected os: OS;
  protected windowId: string | null = null;
  protected parentWindowId: string | null = null;
  protected container: HTMLElement | null = null;
  protected backdropElement: HTMLElement | null = null;
  protected dialogElement: HTMLElement | null = null;
  protected result: any = null;
  protected isOpen: boolean = false;
  protected resolvePromise: ((value: any) => void) | null = null;
  protected dialogPromise: Promise<any> | null = null;
  protected isEmbedded: boolean = false;

  constructor(os: OS, protected windows?: GuiApplication) {
    this.os = os;
  }

  /**
   * Shows the dialog and returns a promise that resolves when the dialog is closed
   * @param title Dialog title
   * @param width Dialog width (default: 400)
   * @param height Dialog height (default: 200)
   * @param options Dialog display options
   */
  protected showDialog(title: string, width: number = 400, height: number = 200, options?: DialogShowOptions): Promise<any> {
    if (this.isOpen) {
      return Promise.resolve(null);
    }

    this.isOpen = true;
    this.result = null;
    
    // Use options if provided, otherwise use defaults
    const dialogWidth = options?.width || width;
    const dialogHeight = options?.height || height;
    const showBackdrop = options?.showBackdrop !== false; // Default to true
    const isModal = options?.modal !== false; // Default to true
    this.parentWindowId = options?.parentWindowId || null;
    if (this.windows && !this.parentWindowId) {
      this.parentWindowId = this.windows.WindowId || null;
    }

    this.isEmbedded = !!this.parentWindowId;

    const windowManager = this.os.getWindowManager();

    if (this.isEmbedded && this.parentWindowId) {      // Display the dialog inside the parent window
      const parentElement = windowManager.getWindowContentElement(this.parentWindowId);
      if (!parentElement) {
        console.error('Parent window content element not found');
        this.isOpen = false;
        return Promise.resolve(null);
      }

      // Add GuiApplicationDialog class to the parent element
      parentElement.classList.add('GuiApplicationDialog');
      
      // Create a backdrop if needed
      if (showBackdrop) {
        this.backdropElement = document.createElement('div');
        this.backdropElement.classList.add('dialog-backdrop');
        if (isModal) {
          this.backdropElement.classList.add('modal');
        }
        parentElement.appendChild(this.backdropElement);
      }      // Create the dialog element
      this.dialogElement = document.createElement('div');
      this.dialogElement.classList.add('embedded-dialog');
      this.dialogElement.style.width = `${dialogWidth}px`;
      this.dialogElement.style.height = `${dialogHeight}px`;
      
      // Create dialog header
      const dialogHeader = document.createElement('div');
      dialogHeader.classList.add('dialog-header');
      dialogHeader.textContent = title;
      this.dialogElement.appendChild(dialogHeader);
      
      // Create dialog content container
      this.container = document.createElement('div');
      this.container.classList.add('dialog-container');
      this.dialogElement.appendChild(this.container);
      
      // Append dialog to parent
      if (this.backdropElement) {
        this.backdropElement.appendChild(this.dialogElement);
      } else {
        parentElement.appendChild(this.dialogElement);
      }
      
      // Position the dialog in the center of the parent window
      this.centerDialogInParent();
    } else {
      // Create a new dialog window
      this.windowId = windowManager.createWindow({
        title,
        width: dialogWidth,
        height: dialogHeight,
        appId: 'dialog',
        resizable: false,
        maximizable: false
      });

      // Get the window content element
      this.container = windowManager.getWindowContentElement(this.windowId);
      if (!this.container) {
        this.isOpen = false;
        return Promise.resolve(null);
      }

      // Apply dialog styles
      this.container.classList.add('GuiApplicationDialog');
      this.container.classList.add('dialog-container');
    }    // Return a promise that will be resolved when the dialog is closed
    this.dialogPromise = new Promise((resolve) => {
      this.resolvePromise = resolve;
    });
    return this.dialogPromise;
  }
  
  /**
   * Center the dialog in the parent window
   */
  private centerDialogInParent(): void {
    if (!this.isEmbedded || !this.dialogElement || !this.parentWindowId) return;
    
    const parentElement = this.os.getWindowManager().getWindowContentElement(this.parentWindowId);
    if (!parentElement) return;
    
    const parentRect = parentElement.getBoundingClientRect();
    const dialogRect = this.dialogElement.getBoundingClientRect();
    
    const top = Math.max(0, (parentRect.height - dialogRect.height) / 2);
    const left = Math.max(0, (parentRect.width - dialogRect.width) / 2);
    
    this.dialogElement.style.top = `${top}px`;
    this.dialogElement.style.left = `${left}px`;
  }
  /**
   * Close the dialog and resolve with the result
   */  protected closeDialog(result: any = null): void {
    console.log('Dialog closing with result:', result, 'Embedded:', this.isEmbedded);
    if (!this.isOpen) return;

    this.result = result;
    this.isOpen = false;

    if (this.isEmbedded) {
      // Remove embedded dialog elements
      console.log('Removing embedded dialog elements');
      if (this.backdropElement) {
        this.backdropElement.remove();
        this.backdropElement = null;
      } else if (this.dialogElement) {
        this.dialogElement.remove();
        this.dialogElement = null;
      } else {
        console.warn('No backdrop or dialog element found to remove');
      }
      
      // Remove GuiApplicationDialog class from parent element when dialog is closed
      if (this.parentWindowId) {
        const parentElement = this.os.getWindowManager().getWindowContentElement(this.parentWindowId);
        if (parentElement) {
          parentElement.classList.remove('GuiApplicationDialog');
        }
      }
      
      this.container = null;
    } else if (this.windowId) {
      // Close the window
      console.log('Closing dialog window:', this.windowId);
      const windowManager = this.os.getWindowManager();
      windowManager.closeWindow(this.windowId);
      this.windowId = null;
    }

    // Resolve the promise with the result
    if (this.resolvePromise) {
      console.log('Resolving dialog promise with:', result);
      this.resolvePromise(result);
      this.resolvePromise = null;
    } else {
      console.warn('No resolvePromise function found');
    }
  }
  /**
   * Create a button with standard styling
   */
  protected createButton(text: string, isPrimary: boolean = false, onClick?: () => void): HTMLButtonElement {
    const button = document.createElement('button');
    button.textContent = text;
    button.classList.add('dialog-button');
    
    if (isPrimary) {
      button.classList.add('dialog-button-primary');
    }

    if (onClick) {
      button.addEventListener('click', onClick);
    }

    return button;
  }

  /**
   * Create a button container with standard styling
   */
  protected createButtonContainer(): HTMLDivElement {
    const buttonContainer = document.createElement('div');
    buttonContainer.classList.add('dialog-button-container');
    return buttonContainer;
  }
}

/**
 * MessageBox dialog for showing messages and getting user confirmation
 */
export class MessageBox extends BaseDialog {
  /**
   * Show a message box with custom buttons
   * @param title Dialog title
   * @param message Message to display
   * @param buttons Array of button labels (defaults to ['OK'])
   * @param type Dialog type for styling (normal, error, warning, information)
   * @param options Dialog display options
   * @returns Promise that resolves to the label of the button that was clicked
   */  public async Show(
    title: string, 
    message: string, 
    buttons: string[] = ['ok'], 
    type: DialogType = 'normal',
    options?: DialogShowOptions
  ): Promise<DialogButtonResult> {
    // Store the promise from showDialog
    const dialogPromise = this.showDialog(title, 400, 200, options);
    
    // Create a small delay to ensure DOM is ready
    await new Promise(resolve => setTimeout(resolve, 10));
    
    if (!this.container) return 'cancel';

    // Create message element with appropriate styling
    const messageElement = document.createElement('div');
    messageElement.classList.add('dialog-message-box');
    
    // Add icon based on type
    const iconElement = document.createElement('div');
    iconElement.classList.add('dialog-icon');
    
    switch (type) {
      case 'error':
        iconElement.textContent = '❌';
        messageElement.classList.add('error');
        break;
      case 'warning':
        iconElement.textContent = '⚠️';
        messageElement.classList.add('warning');
        break;
      case 'information':
        iconElement.textContent = 'ℹ️';
        messageElement.classList.add('information');
        break;
      default:
        iconElement.textContent = '';
    }
    
    if (iconElement.textContent) {
      messageElement.appendChild(iconElement);
    }
    
    // Add message text
    const textElement = document.createElement('div');
    textElement.textContent = message;
    textElement.classList.add('dialog-text');
    messageElement.appendChild(textElement);
    
    this.container.appendChild(messageElement);

    // Add buttons
    const buttonContainer = this.createButtonContainer();
    
    // Create buttons for each label
    for (let i = 0; i < buttons.length; i++) {
      const buttonLabel = buttons[i].toLowerCase();
      const isPrimary = i === 0; // First button is primary
      
      const button = this.createButton(
        buttonLabel.charAt(0).toUpperCase() + buttonLabel.slice(1), 
        isPrimary,
        () => this.closeDialog(buttonLabel)
      );
      
      buttonContainer.appendChild(button);
    }      this.container.appendChild(buttonContainer);
      
    // Return the promise that will resolve with the selected button
    return this.dialogPromise as Promise<DialogButtonResult>;
  }
}

/**
 * PromptDialog for getting text input from the user
 */
export class PromptDialog extends BaseDialog {
  /**
   * Show a text prompt dialog
   * @param title Dialog title
   * @param message Message to display
   * @param promptOptions Prompt options (input type, placeholder, default value, validation attributes)
   * @param dialogOptions Dialog display options
   * @returns Promise that resolves to the input value or null if cancelled
   */
public async Show(
    title: string, 
    message: string, 
    promptOptions?: PromptOptions, 
    dialogOptions?: DialogShowOptions
  ): Promise<string | null> {
    const dialogPromise = this.showDialog(title, 400, 200, dialogOptions);
    
    // Create a small delay to ensure DOM is ready
    await new Promise(resolve => setTimeout(resolve, 10));
    
    if (!this.container) return null;

    // Create message element
    const messageElement = document.createElement('div');
    messageElement.textContent = message;
    messageElement.classList.add('dialog-message');
    this.container.appendChild(messageElement);

    // Create input element
    const inputElement = document.createElement('input');
    inputElement.type = promptOptions?.inputType || 'text';
    inputElement.classList.add('dialog-input');
    
    if (promptOptions?.placeHolder) {
      inputElement.placeholder = promptOptions.placeHolder;
    }
    
    if (promptOptions?.defaultText) {
      inputElement.value = promptOptions.defaultText;
    }
    
    // Set additional HTML5 input attributes if provided
    if (promptOptions?.min !== undefined) {
      inputElement.min = promptOptions.min.toString();
    }
    
    if (promptOptions?.max !== undefined) {
      inputElement.max = promptOptions.max.toString();
    }
    
    if (promptOptions?.step !== undefined) {
      inputElement.step = promptOptions.step.toString();
    }
    
    if (promptOptions?.pattern) {
      inputElement.pattern = promptOptions.pattern;
    }
    
    if (promptOptions?.required) {
      inputElement.required = true;
    }
    
    this.container.appendChild(inputElement);

    // Add buttons
    const buttonContainer = this.createButtonContainer();
    
    // Add Cancel button
    const cancelButton = this.createButton('Cancel', false, () => this.closeDialog(null));
    buttonContainer.appendChild(cancelButton);
    
    // Add OK button
    const okButton = this.createButton('OK', true, () => this.closeDialog(inputElement.value));
    buttonContainer.appendChild(okButton);
    
    this.container.appendChild(buttonContainer);

    // Handle Enter key press
    inputElement.addEventListener('keyup', (e) => {
      if (e.key === 'Enter') {
        this.closeDialog(inputElement.value);
      } else if (e.key === 'Escape') {
        this.closeDialog(null);
      }
    });

    // Set focus to the input element    
    setTimeout(() => {
      inputElement.focus();
      inputElement.select();
    }, 100);

    // Return the stored dialog promise
    return this.dialogPromise as Promise<string | null>;
  }
}

/**
 * FilePicker dialog for selecting files
 */
export class FilePicker extends BaseDialog {
    private fileSystem: FileSystem;

    public constructor(os: OS, windows?: GuiApplication) {
        super(os, windows);
        this.fileSystem = os.getFileSystem();
    }
  /**
   * Show a file picker dialog
   * @param title Dialog title
   * @param fileOptions File picker options
   * @param dialogOptions Dialog display options
   * @returns Promise that resolves to the selected file path or null if cancelled
   */
public async Show(
    title: string, 
    fileOptions?: FilePickerOptions, 
    dialogOptions?: DialogShowOptions
  ): Promise<string | null> {
    const dialogPromise = this.showDialog(title, 550, 400, dialogOptions);
    
    // Create a small delay to ensure DOM is ready
    await new Promise(resolve => setTimeout(resolve, 10));
    
    if (!this.container) return null;
   
    // Create file picker UI - this would connect to your FileSystem implementation
    const filesystemElement = document.createElement('div');
    filesystemElement.classList.add('dialog-filesystem');
    
    // Add message if provided
    if (fileOptions?.message) {
      const messageElement = document.createElement('div');
      messageElement.textContent = fileOptions.message;
      messageElement.classList.add('dialog-message');
      this.container.appendChild(messageElement);
    }
    
    // Create navigation elements
    const navContainer = document.createElement('div');
    navContainer.classList.add('dialog-nav-container');
    
    const pathDisplay = document.createElement('div');
    pathDisplay.classList.add('dialog-path-display');
    
    const upButton = document.createElement('button');
    upButton.textContent = '📂 Up';
    upButton.classList.add('dialog-up-button');
    
    navContainer.appendChild(pathDisplay);
    navContainer.appendChild(upButton);
    filesystemElement.appendChild(navContainer);
    
    // Create file list
    const fileList = document.createElement('ul');
    fileList.classList.add('dialog-file-list');
    let currentPath = fileOptions?.initialDirectory || '/home/user';
    const allowMultiple = fileOptions?.allowMultiple || false;
    let selectedFiles: string[] = [];
    let selectedFile: string | null = null;
    
    // Function to load directory contents
    const loadDirectory = async (path: string) => {
      try {
        // Clear selection
        selectedFile = null;
        
        // Update path display
        pathDisplay.textContent = path;
        
        // Clear existing list
        while (fileList.firstChild) {
          fileList.removeChild(fileList.firstChild);
        }
        
        // Get directory contents
        const entries = await this.fileSystem.readDirectory(path);
        
        // Sort entries: directories first, then files
        entries.sort((a, b) => {
          if (a.type === 'directory' && b.type !== 'directory') return -1;
          if (a.type !== 'directory' && b.type === 'directory') return 1;
          return a.name.localeCompare(b.name);
        });          // Add entries to list
        entries.forEach(entry => {
          const item = document.createElement('li');
          item.classList.add('dialog-file-item');
          
          // Add icon based on type
          const icon = document.createElement('span');
          icon.classList.add('icon');
          icon.textContent = entry.type === 'directory' ? '📁' : '📄';
          item.appendChild(icon);
          
          // Add name
          const name = document.createElement('span');
          name.textContent = entry.name;
          name.classList.add('name');
          item.appendChild(name);
          
          // Handle click for selection (only for files, not directories)
          item.addEventListener('click', (e) => {
            // Only allow selecting files, not directories
            if (entry.type === 'directory') {
              return; // Don't select directories
            }
            
            const fullPath = (path + '/' + entry.name).replace(/\/+/g, '/');
            
            if (allowMultiple && (e.ctrlKey || e.shiftKey)) {
              // Multiple selection with Ctrl or Shift
              if (e.ctrlKey) {
                // Toggle selection with Ctrl
                const index = selectedFiles.indexOf(fullPath);
                if (index !== -1) {
                  // Remove from selection
                  selectedFiles.splice(index, 1);
                  item.classList.remove('selected');
                } else {
                  // Add to selection
                  selectedFiles.push(fullPath);
                  item.classList.add('selected');
                }
              } else if (e.shiftKey && selectedFiles.length > 0) {
                // Shift selection - find all items between last selected and current
                const allFileItems = Array.from(fileList.querySelectorAll('li')).filter(li => {
                  // Only include files, not directories
                  return li.querySelector('span')?.textContent === '📄';
                });
                
                const lastSelectedPath = selectedFiles[selectedFiles.length - 1];
                const lastSelectedName = lastSelectedPath.split('/').pop();
                
                // Find indexes of last selected and current items
                let startIdx = -1;
                let endIdx = -1;
                let currentIdx = -1;
                
                allFileItems.forEach((fileItem, idx) => {
                  const itemName = fileItem.querySelector('span:last-child')?.textContent;
                  if (itemName === lastSelectedName) startIdx = idx;
                  if (itemName === entry.name) endIdx = idx;
                  
                  // Track current index for later use
                  if (itemName === entry.name) currentIdx = idx;
                });
                
                // Swap if needed to ensure startIdx <= endIdx
                if (startIdx > endIdx) {
                  [startIdx, endIdx] = [endIdx, startIdx];
                }
                
                // Clear previous selection
                fileList.querySelectorAll('li').forEach(li => {
                  li.classList.remove('selected');
                });
                selectedFiles = [];
                
                // Select all items in range
                for (let i = startIdx; i <= endIdx; i++) {
                  if (i >= 0 && i < allFileItems.length) {
                    allFileItems[i].classList.add('selected');
                    const filename = allFileItems[i].querySelector('span:last-child')?.textContent;
                    if (filename) {
                      const filePath = (path + '/' + filename).replace(/\/+/g, '/');
                      selectedFiles.push(filePath);
                    }
                  }
                }
              }
              
              // Update selectedFile (for backward compatibility)
              selectedFile = selectedFiles.length > 0 ? selectedFiles[0] : null;
            } else {
              // Single selection (default)
              fileList.querySelectorAll('li').forEach(li => {
                li.classList.remove('selected');
              });
              
              item.classList.add('selected');
              selectedFile = fullPath;
              selectedFiles = [fullPath];
            }
          });
          
          // Handle double click for navigation or selection
          item.addEventListener('dblclick', async () => {
            if (entry.type === 'directory') {
              // Navigate to directory
              const newPath = path + '/' + entry.name;
              // Normalize to prevent duplicate slashes
              currentPath = newPath.replace(/\/+/g, '/');
              await loadDirectory(currentPath);
            } else {
              // Select and close dialog for files
              const filePath = path + '/' + entry.name;
              const normalizedPath = filePath.replace(/\/+/g, '/');
              
              if (allowMultiple) {
                this.closeDialog([normalizedPath]); // Return array for multiple select mode
              } else {
                this.closeDialog(normalizedPath); // Return string for single select mode
              }
            }
          });
          
          fileList.appendChild(item);
        });      } catch (error: any) {        console.error(`Error loading directory: ${path}`, error);
        // Add error message to list
        const errorItem = document.createElement('li');
        errorItem.textContent = `Error: Could not load directory (${error.message || 'Unknown error'})`;
        errorItem.classList.add('dialog-error');
        fileList.appendChild(errorItem);
      }
    };
    
    // Handle Up button click
    upButton.addEventListener('click', async () => {
      if (currentPath === '/') {
        return; // Already at root
      }
      
      // Go up one directory
      const parentPath = currentPath.substring(0, currentPath.lastIndexOf('/')) || '/';
      currentPath = parentPath;
      await loadDirectory(currentPath);
    });
    
    // Initial directory load
    loadDirectory(currentPath).catch(error => {
      console.error('Failed to load initial directory:', error);
    });
    
    filesystemElement.appendChild(fileList);
    this.container.appendChild(filesystemElement);    // Create filename input field for save mode
    if (fileOptions?.saveMode) {
      const filenameContainer = document.createElement('div');
      filenameContainer.classList.add('dialog-input-container');
      
      const filenameLabel = document.createElement('label');
      filenameLabel.textContent = 'File name:';
      
      const filenameInput = document.createElement('input');
      filenameInput.type = 'text';
      
      if (fileOptions?.initialFileName) {
        filenameInput.value = fileOptions.initialFileName;
      }
      
      filenameContainer.appendChild(filenameLabel);
      filenameContainer.appendChild(filenameInput);
      this.container.appendChild(filenameContainer);
      
      // Update selectedFile when input changes
      filenameInput.addEventListener('input', () => {
        selectedFile = filenameInput.value;
      });
    }    // Create filter dropdown if file types are provided
    if (fileOptions?.fileTypes && fileOptions.fileTypes.length > 0) {
      const filterContainer = document.createElement('div');
      filterContainer.classList.add('dialog-input-container');
      
      const filterLabel = document.createElement('label');
      filterLabel.textContent = 'File type:';
      
      const filterSelect = document.createElement('select');
      
      // Add All Files option
      const allOption = document.createElement('option');
      allOption.value = '*';
      allOption.textContent = 'All Files (*.*)';
      filterSelect.appendChild(allOption);
      
      // Add each file type
      fileOptions.fileTypes.forEach(fileType => {
        const option = document.createElement('option');
        option.value = fileType;
        option.textContent = `${fileType.toUpperCase()} Files (*.${fileType})`;
        filterSelect.appendChild(option);
      });
      
      filterContainer.appendChild(filterLabel);
      filterContainer.appendChild(filterSelect);
      this.container.appendChild(filterContainer);
    }// Add buttons
    const buttonContainer = this.createButtonContainer();
    
    // Add Cancel button
    const cancelButton = this.createButton('Cancel', false, () => this.closeDialog(null));
    buttonContainer.appendChild(cancelButton);
    
    // Add OK/Open/Save button
    const actionText = fileOptions?.saveMode ? 'Save' : 'Open';
    const okButton = this.createButton(actionText, true, () => {
      if (allowMultiple && selectedFiles.length > 0) {
        this.closeDialog(selectedFiles); // Return array for multiple selection
      } else {
        this.closeDialog(selectedFile); // Return string for single selection
      }
    });
buttonContainer.appendChild(okButton);
    
    this.container.appendChild(buttonContainer);

    // Return the promise
    return this.dialogPromise as Promise<string | null>;
  }
}

/**
 * DirectoryPicker dialog for selecting directories
 */
export class DirectoryPicker extends BaseDialog {
     private fileSystem: FileSystem;

    public constructor(os: OS, windows?: GuiApplication) {
        super(os, windows);
        this.fileSystem = os.getFileSystem();
    }
  /**
   * Show a directory picker dialog
   * @param title Dialog title
   * @param dirOptions Directory picker options
   * @param dialogOptions Dialog display options
   * @returns Promise that resolves to the selected directory path or null if cancelled
   */
public async Show(
    title: string, 
    dirOptions?: DirectoryPickerOptions, 
    dialogOptions?: DialogShowOptions
  ): Promise<string | null> {
    const dialogPromise = this.showDialog(title, 550, 400, dialogOptions);
    
    // Create a small delay to ensure DOM is ready
    await new Promise(resolve => setTimeout(resolve, 10));
    
    if (!this.container) return null;

    // Create directory picker UI - this would connect to your FileSystem implementation
    const filesystemElement = document.createElement('div');
    filesystemElement.style.flex = '1';
    filesystemElement.style.overflow = 'auto';
    filesystemElement.style.border = '1px solid #ccc';
    filesystemElement.style.marginBottom = '15px';
    filesystemElement.style.padding = '10px';
    
    // Add message if provided
    if (dirOptions?.message) {
      const messageElement = document.createElement('div');
      messageElement.textContent = dirOptions.message;
      messageElement.style.marginBottom = '15px';
      this.container.appendChild(messageElement);
    }
    
    // Here you would populate the directory list from your filesystem
    // For placeholder purposes, we'll just add some dummy directories
    const dirList = document.createElement('ul');
    dirList.style.listStyle = 'none';
    dirList.style.padding = '0';
    dirList.style.margin = '0';
    
    // TODO: Replace with real filesystem access
    const dummyDirs = [
      'Documents',
      'Downloads',
      'Pictures',
      'Music',
      'Videos'
    ];
    
    let selectedDir: string | null = null;
    
    dummyDirs.forEach(dir => {
      const dirItem = document.createElement('li');
      dirItem.textContent = `📁 ${dir}`;
      dirItem.style.padding = '8px';
      dirItem.style.cursor = 'pointer';
      
      dirItem.addEventListener('click', () => {
        // Deselect all items
        dirList.querySelectorAll('li').forEach(item => {
          item.style.backgroundColor = '';
        });
        
        // Select this item
        dirItem.style.backgroundColor = '#e0e0e0';
        selectedDir = dir;
      });
      
      dirItem.addEventListener('dblclick', () => {
        this.closeDialog(dir);
      });
      
      dirList.appendChild(dirItem);
    });
    
    filesystemElement.appendChild(dirList);
    this.container.appendChild(filesystemElement);

    // Add buttons
    const buttonContainer = this.createButtonContainer();
    
    // Add Cancel button
    const cancelButton = this.createButton('Cancel', false, () => this.closeDialog(null));
    buttonContainer.appendChild(cancelButton);
      // Add Select button
    const selectButton = this.createButton('Select Folder', true, () => this.closeDialog(selectedDir));
    buttonContainer.appendChild(selectButton);
    
    this.container.appendChild(buttonContainer);

    // Return the promise
    return this.dialogPromise as Promise<string | null>;
  }
}

/**
 * Dialog manager class to handle all dialog functionality
 * This will be used as a property in GuiApplication
 */
export class DialogManager {
  private os: OS;
  private msgbox: MessageBox;
  private prompt: PromptDialog;
  private filePicker: FilePicker;
  private dirPicker: DirectoryPicker;
  constructor(os: OS, private windows: GuiApplication) {
    this.os = os;
    this.msgbox = new MessageBox(os, windows);
    this.prompt = new PromptDialog(os, windows);
    this.filePicker = new FilePicker(os, windows);
    this.dirPicker = new DirectoryPicker(os, windows);
  }

  /**
   * Access to MessageBox dialog
   */
  get Msgbox(): MessageBox {
    return this.msgbox;
  }

  /**
   * Access to Prompt dialog
   */
  get Prompt(): PromptDialog {
    return this.prompt;
  }

  /**
   * Access to FilePicker dialog
   */
  get FilePicker(): FilePicker {
    return this.filePicker;
  }

  /**
   * Access to DirectoryPicker dialog
   */
  get DirectoryPicker(): DirectoryPicker {
    return this.dirPicker;
  }
}
