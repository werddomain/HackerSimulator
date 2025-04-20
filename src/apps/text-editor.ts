import { OS } from '../core/os';
import { FileEntryUtils } from '../core/file-entry-utils';
import { GuiApplication } from '../core/gui-application';

/**
 * Text Editor App for the Hacker Game
 * Provides basic text editing capabilities
 */
export class TextEditorApp extends GuiApplication {
  private editor: HTMLTextAreaElement | null = null;
  private currentFilePath: string | null = null;
  private hasUnsavedChanges: boolean = false;
  private statusBar: HTMLElement | null = null;
  private toolbar: HTMLElement | null = null;

  constructor(os: OS) {
    super(os);
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'text-editor';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    this.setupEventListeners();
  }

  /**
   * Render the text editor UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="text-editor-app">
        <div class="text-editor-toolbar">
          <button class="toolbar-btn new-file" title="New File">
            <span class="icon">📄</span> New
          </button>
          <button class="toolbar-btn open-file" title="Open File">
            <span class="icon">📂</span> Open
          </button>
          <button class="toolbar-btn save-file" title="Save File">
            <span class="icon">💾</span> Save
          </button>
          <button class="toolbar-btn save-as" title="Save As">
            <span class="icon">💾</span> Save As
          </button>
          <div class="separator"></div>
          <button class="toolbar-btn cut" title="Cut">
            <span class="icon">✂️</span> Cut
          </button>
          <button class="toolbar-btn copy" title="Copy">
            <span class="icon">📋</span> Copy
          </button>
          <button class="toolbar-btn paste" title="Paste">
            <span class="icon">📌</span> Paste
          </button>
          <div class="separator"></div>
          <button class="toolbar-btn find" title="Find">
            <span class="icon">🔍</span> Find
          </button>
          <button class="toolbar-btn replace" title="Replace">
            <span class="icon">🔄</span> Replace
          </button>
        </div>
        <div class="text-editor-content">
          <textarea class="text-editor-textarea" spellcheck="false"></textarea>
        </div>
        <div class="text-editor-status-bar">
          <div class="file-path">No file open</div>
          <div class="editor-status">Ready</div>
        </div>
      </div>
      <style>
        .text-editor-app {
          display: flex;
          flex-direction: column;
          height: 100%;
          background-color: #1e1e1e;
          color: #d4d4d4;
          font-family: 'Courier New', monospace;
        }
        
        .text-editor-toolbar {
          display: flex;
          padding: 5px;
          background-color: #333;
          border-bottom: 1px solid #454545;
        }
        
        .toolbar-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          padding: 5px 10px;
          cursor: pointer;
          font-size: 12px;
          border-radius: 3px;
          display: flex;
          align-items: center;
          gap: 5px;
        }
        
        .toolbar-btn:hover {
          background-color: #454545;
        }
        
        .separator {
          width: 1px;
          background-color: #555;
          margin: 0 10px;
        }
        
        .text-editor-content {
          flex: 1;
          position: relative;
          overflow: hidden;
        }
        
        .text-editor-textarea {
          width: 100%;
          height: 100%;
          background-color: #1e1e1e;
          color: #d4d4d4;
          border: none;
          resize: none;
          padding: 10px;
          font-family: 'Courier New', monospace;
          font-size: 14px;
          line-height: 1.5;
          tab-size: 4;
          outline: none;
        }
        
        .text-editor-status-bar {
          display: flex;
          justify-content: space-between;
          padding: 3px 10px;
          background-color: #007acc;
          color: white;
          font-size: 12px;
        }
        
        .find-replace-panel {
          position: absolute;
          top: 10px;
          right: 10px;
          background-color: #252526;
          border: 1px solid #454545;
          box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
          padding: 10px;
          z-index: 100;
          display: none;
        }
        
        .find-replace-panel.visible {
          display: block;
        }
        
        .find-replace-input {
          background-color: #3c3c3c;
          color: #d4d4d4;
          border: 1px solid #555;
          padding: 5px;
          margin-bottom: 5px;
          width: 200px;
        }
        
        .find-replace-btn {
          background-color: #0e639c;
          color: white;
          border: none;
          padding: 5px 10px;
          margin-right: 5px;
          cursor: pointer;
        }
        
        .find-replace-btn:hover {
          background-color: #1177bb;
        }
      </style>
    `;
    
    this.editor = this.container.querySelector('.text-editor-textarea');
    this.statusBar = this.container.querySelector('.text-editor-status-bar');
    this.toolbar = this.container.querySelector('.text-editor-toolbar');
  }

  /**
   * Setup event listeners
   */
  private setupEventListeners(): void {
    if (!this.container || !this.editor) return;
    
    // File operations
    const newFileBtn = this.container.querySelector('.toolbar-btn.new-file');
    const openFileBtn = this.container.querySelector('.toolbar-btn.open-file');
    const saveFileBtn = this.container.querySelector('.toolbar-btn.save-file');
    const saveAsBtn = this.container.querySelector('.toolbar-btn.save-as');
    
    // Edit operations
    const cutBtn = this.container.querySelector('.toolbar-btn.cut');
    const copyBtn = this.container.querySelector('.toolbar-btn.copy');
    const pasteBtn = this.container.querySelector('.toolbar-btn.paste');
    const findBtn = this.container.querySelector('.toolbar-btn.find');
    const replaceBtn = this.container.querySelector('.toolbar-btn.replace');
    
    // File operations
    newFileBtn?.addEventListener('click', () => this.newFile());
    openFileBtn?.addEventListener('click', () => this.openFileDialog());
    saveFileBtn?.addEventListener('click', () => this.saveFile());
    saveAsBtn?.addEventListener('click', () => this.saveFileAs());
    
    // Edit operations
    cutBtn?.addEventListener('click', () => this.cut());
    copyBtn?.addEventListener('click', () => this.copy());
    pasteBtn?.addEventListener('click', () => this.paste());
    findBtn?.addEventListener('click', () => this.showFindPanel());
    replaceBtn?.addEventListener('click', () => this.showReplacePanel());
    
    // Track changes
    this.editor.addEventListener('input', () => {
      this.hasUnsavedChanges = true;
      this.updateStatus();
    });
    
    // Keyboard shortcuts
    this.editor.addEventListener('keydown', (e) => {
      // Save: Ctrl+S
      if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        this.saveFile();
      }
      // New: Ctrl+N
      else if (e.ctrlKey && e.key === 'n') {
        e.preventDefault();
        this.newFile();
      }
      // Open: Ctrl+O
      else if (e.ctrlKey && e.key === 'o') {
        e.preventDefault();
        this.openFileDialog();
      }
      // Find: Ctrl+F
      else if (e.ctrlKey && e.key === 'f') {
        e.preventDefault();
        this.showFindPanel();
      }
      // Replace: Ctrl+H
      else if (e.ctrlKey && e.key === 'h') {
        e.preventDefault();
        this.showReplacePanel();
      }
      // Tab key handling
      else if (e.key === 'Tab') {
        e.preventDefault();
        this.insertTextAtCursor('    '); // Insert 4 spaces
      }
    });
  }

  /**
   * Insert text at cursor position
   */
  private insertTextAtCursor(text: string): void {
    if (!this.editor) return;
    
    const startPos = this.editor.selectionStart;
    const endPos = this.editor.selectionEnd;
    
    this.editor.value = this.editor.value.substring(0, startPos) + 
                       text + 
                       this.editor.value.substring(endPos);
    
    // Set cursor position after inserted text
    this.editor.selectionStart = this.editor.selectionEnd = startPos + text.length;
    
    this.hasUnsavedChanges = true;
    this.updateStatus();
  }

  /**
   * Create a new file
   */
  private newFile(): void {
    if (this.hasUnsavedChanges) {
      if (!confirm('You have unsaved changes. Do you want to continue?')) {
        return;
      }
    }
    
    if (this.editor) {
      this.editor.value = '';
    }
    
    this.currentFilePath = null;
    this.hasUnsavedChanges = false;
    this.updateStatus();
  }

  /**
   * Open file dialog
   */
  private openFileDialog(): void {
    if (this.hasUnsavedChanges) {
      if (!confirm('You have unsaved changes. Do you want to continue?')) {
        return;
      }
    }
    
    // Create a file picker dialog
    const dialog = document.createElement('div');
    dialog.className = 'file-picker-dialog';
    dialog.innerHTML = `
      <div class="file-picker-header">
        <h2>Open File</h2>
        <button class="close-btn">×</button>
      </div>
      <div class="file-picker-content">
        <div class="file-browser">
          <div class="path-bar">
            <input type="text" class="path-input" value="/home/user" />
            <button class="nav-btn">Go</button>
          </div>
          <div class="files-list"></div>
        </div>
      </div>
      <div class="file-picker-footer">
        <button class="cancel-btn">Cancel</button>
        <button class="open-btn" disabled>Open</button>
      </div>
      <style>
        .file-picker-dialog {
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          width: 80%;
          height: 70%;
          background-color: #252526;
          border: 1px solid #454545;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
          display: flex;
          flex-direction: column;
          z-index: 1000;
        }
        .file-picker-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 10px 15px;
          background-color: #333;
          border-bottom: 1px solid #454545;
        }
        .file-picker-header h2 {
          margin: 0;
          font-size: 16px;
          color: #d4d4d4;
        }
        .close-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          font-size: 20px;
          cursor: pointer;
        }
        .file-picker-content {
          flex: 1;
          overflow: hidden;
          display: flex;
          flex-direction: column;
        }
        .file-browser {
          flex: 1;
          display: flex;
          flex-direction: column;
        }
        .path-bar {
          display: flex;
          padding: 10px;
          background-color: #2d2d2d;
        }
        .path-input {
          flex: 1;
          background-color: #3c3c3c;
          border: 1px solid #555;
          color: #d4d4d4;
          padding: 5px 10px;
        }
        .nav-btn {
          background-color: #0e639c;
          color: white;
          border: none;
          padding: 5px 10px;
          margin-left: 5px;
          cursor: pointer;
        }
        .files-list {
          flex: 1;
          overflow: auto;
          padding: 10px;
        }
        .file-item {
          padding: 5px 10px;
          cursor: pointer;
          display: flex;
          align-items: center;
        }
        .file-item:hover {
          background-color: #2a2d2e;
        }
        .file-item.selected {
          background-color: #04395e;
        }
        .file-icon {
          margin-right: 5px;
        }
        .file-picker-footer {
          display: flex;
          justify-content: flex-end;
          padding: 10px 15px;
          background-color: #333;
          border-top: 1px solid #454545;
        }
        .cancel-btn, .open-btn {
          padding: 5px 15px;
          border: none;
          cursor: pointer;
          margin-left: 10px;
        }
        .cancel-btn {
          background-color: #3c3c3c;
          color: #d4d4d4;
        }
        .open-btn {
          background-color: #0e639c;
          color: white;
        }
        .open-btn:disabled {
          background-color: #264f78;
          opacity: 0.5;
          cursor: not-allowed;
        }
      </style>
    `;
    
    this.container?.appendChild(dialog);
    
    const closeBtn = dialog.querySelector('.close-btn');
    const cancelBtn = dialog.querySelector('.cancel-btn');
    const openBtn = dialog.querySelector<HTMLButtonElement>('.open-btn');
    const pathInput = dialog.querySelector<HTMLInputElement>('.path-input');
    const navBtn = dialog.querySelector('.nav-btn');
    const filesList = dialog.querySelector('.files-list');
    
    let currentPath = '/home/user';
    let selectedFile: string | null = null;
    
    // Load files and directories
    const loadPath = (path: string) => {
      currentPath = path;
      if (pathInput) pathInput.value = path;
      
      if (filesList) {
        filesList.innerHTML = '<div class="loading">Loading...</div>';
        
        // Use the OS's file system to get directory contents
        this.os.getFileSystem().readDirectory(path)
          .then(entries => {
            filesList.innerHTML = '';
            
            // Add parent directory option if not at root
            if (path !== '/') {
              const parentDir = document.createElement('div');
              parentDir.className = 'file-item directory';
              parentDir.innerHTML = '<span class="file-icon">📁</span> ..';
              parentDir.addEventListener('click', () => {
                const parentPath = path.substring(0, path.lastIndexOf('/'));
                loadPath(parentPath || '/');
              });
              filesList.appendChild(parentDir);
            }
              // Sort entries: directories first, then files
            const sortedEntries = entries.sort((a, b) => {
              const aIsDir = FileEntryUtils.isDirectory(a);
              const bIsDir = FileEntryUtils.isDirectory(b);
              
              if (aIsDir && !bIsDir) return -1;
              if (!aIsDir && bIsDir) return 1;
              return a.name.localeCompare(b.name);
            });
            
            // Add entries to list
            sortedEntries.forEach(entry => {
              const item = document.createElement('div');
              item.className = `file-item ${FileEntryUtils.isDirectory(entry) ? 'directory' : 'file'}`;
              item.innerHTML = `<span class="file-icon">${FileEntryUtils.isDirectory(entry) ? '📁' : '📄'}</span> ${entry.name}`;
              
              item.addEventListener('click', () => {
                // Clear previous selection
                document.querySelectorAll('.file-item.selected').forEach(el => {
                  el.classList.remove('selected');
                });
                
                if (FileEntryUtils.isDirectory(entry)) {
                  // Navigate to directory
                  loadPath(`${path === '/' ? '' : path}/${entry.name}`);
                } else {
                  // Select file
                  item.classList.add('selected');
                  selectedFile = `${path === '/' ? '' : path}/${entry.name}`;
                  if (openBtn) openBtn.disabled = false;
                }
              });
                // Double-click to open directly
              item.addEventListener('dblclick', () => {
                if (!FileEntryUtils.isDirectory(entry)) {
                  selectedFile = `${path === '/' ? '' : path}/${entry.name}`;
                  openSelectedFile();
                }
              });
              
              filesList.appendChild(item);
            });
            
            if (entries.length === 0) {
              filesList.innerHTML = '<div class="empty-dir">Empty directory</div>';
            }
          })
          .catch(err => {
            filesList.innerHTML = `<div class="error">Error: ${err.message}</div>`;
          });
      }
    };
    
    // Load initial path
    loadPath(currentPath);
    
    // Handle navigation button
    navBtn?.addEventListener('click', () => {
      if (pathInput) {
        loadPath(pathInput.value);
      }
    });
    
    // Handle path input enter key
    pathInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        loadPath(pathInput.value);
      }
    });
    
    // Open selected file
    const openSelectedFile = () => {
      if (selectedFile) {
        this.openFile(selectedFile);
        closeDialog();
      }
    };
    
    // Handle open button
    openBtn?.addEventListener('click', openSelectedFile);
    
    // Close dialog
    const closeDialog = () => {
      dialog.remove();
    };
    
    closeBtn?.addEventListener('click', closeDialog);
    cancelBtn?.addEventListener('click', closeDialog);
  }

  /**
   * Open a file
   */
  public openFile(filePath: string): void {
    this.os.getFileSystem().readFile(filePath)
      .then(content => {
        if (this.editor) {
          this.editor.value = content;
        }
        this.currentFilePath = filePath;
        this.hasUnsavedChanges = false;
        this.updateStatus();
      })
      .catch(error => {
        console.error('Failed to open file:', error);
        alert(`Failed to open file: ${error.message}`);
      });
  }

  /**
   * Save current file
   */
  private saveFile(): void {
    if (!this.currentFilePath) {
      this.saveFileAs();
      return;
    }
    
    if (!this.editor) return;
    
    const content = this.editor.value;
    
    this.os.getFileSystem().writeFile(this.currentFilePath, content)
      .then(() => {
        this.hasUnsavedChanges = false;
        this.updateStatus();
      })
      .catch(error => {
        console.error('Failed to save file:', error);
        alert(`Failed to save file: ${error.message}`);
      });
  }

  /**
   * Save file as
   */
  private saveFileAs(): void {
    // Create a save file dialog
    const dialog = document.createElement('div');
    dialog.className = 'file-picker-dialog';
    dialog.innerHTML = `
      <div class="file-picker-header">
        <h2>Save File As</h2>
        <button class="close-btn">×</button>
      </div>
      <div class="file-picker-content">
        <div class="file-browser">
          <div class="path-bar">
            <input type="text" class="path-input" value="/home/user" />
            <button class="nav-btn">Go</button>
          </div>
          <div class="files-list"></div>
        </div>
        <div class="file-name-row">
          <label>File name:</label>
          <input type="text" class="file-name-input" value="untitled.txt" />
        </div>
      </div>
      <div class="file-picker-footer">
        <button class="cancel-btn">Cancel</button>
        <button class="save-btn">Save</button>
      </div>
      <style>
        .file-picker-dialog {
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          width: 80%;
          height: 70%;
          background-color: #252526;
          border: 1px solid #454545;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
          display: flex;
          flex-direction: column;
          z-index: 1000;
        }
        .file-name-row {
          display: flex;
          align-items: center;
          padding: 10px;
          background-color: #2d2d2d;
          border-top: 1px solid #3c3c3c;
        }
        .file-name-row label {
          margin-right: 10px;
          color: #d4d4d4;
        }
        .file-name-input {
          flex: 1;
          background-color: #3c3c3c;
          border: 1px solid #555;
          color: #d4d4d4;
          padding: 5px 10px;
        }
        .save-btn {
          padding: 5px 15px;
          background-color: #0e639c;
          color: white;
          border: none;
          cursor: pointer;
          margin-left: 10px;
        }
      </style>
    `;
    
    this.container?.appendChild(dialog);
    
    // Add all the same styles as the open dialog
    
    const closeBtn = dialog.querySelector('.close-btn');
    const cancelBtn = dialog.querySelector('.cancel-btn');
    const saveBtn = dialog.querySelector('.save-btn');
    const pathInput = dialog.querySelector<HTMLInputElement>('.path-input');
    const fileNameInput = dialog.querySelector<HTMLInputElement>('.file-name-input');
    const navBtn = dialog.querySelector('.nav-btn');
    const filesList = dialog.querySelector('.files-list');
    
    let currentPath = '/home/user';
    
    // Set initial filename from current file path if exists
    if (this.currentFilePath && fileNameInput) {
      const filename = this.currentFilePath.split('/').pop() || 'untitled.txt';
      fileNameInput.value = filename;
    }
    
    // Load files and directories
    const loadPath = (path: string) => {
      currentPath = path;
      if (pathInput) pathInput.value = path;
      
      if (filesList) {
        filesList.innerHTML = '<div class="loading">Loading...</div>';
        
        // Use the OS's file system to get directory contents
        this.os.getFileSystem().readDirectory(path)
          .then(entries => {
            filesList.innerHTML = '';
            
            // Add parent directory option if not at root
            if (path !== '/') {
              const parentDir = document.createElement('div');
              parentDir.className = 'file-item directory';
              parentDir.innerHTML = '<span class="file-icon">📁</span> ..';
              parentDir.addEventListener('click', () => {
                const parentPath = path.substring(0, path.lastIndexOf('/'));
                loadPath(parentPath || '/');
              });
              filesList.appendChild(parentDir);
            }
              // Sort entries: directories first, then files
            const sortedEntries = entries.sort((a, b) => {
              const aIsDir = FileEntryUtils.isDirectory(a);
              const bIsDir = FileEntryUtils.isDirectory(b);
              
              if (aIsDir && !bIsDir) return -1;
              if (!aIsDir && bIsDir) return 1;
              return a.name.localeCompare(b.name);
            });
            
            // Add entries to list
            sortedEntries.forEach(entry => {
              const item = document.createElement('div');
              item.className = `file-item ${FileEntryUtils.isDirectory(entry) ? 'directory' : 'file'}`;
              item.innerHTML = `<span class="file-icon">${FileEntryUtils.isDirectory(entry) ? '📁' : '📄'}</span> ${entry.name}`;
              
              item.addEventListener('click', () => {
                if (FileEntryUtils.isDirectory(entry)) {
                  // Navigate to directory
                  loadPath(`${path === '/' ? '' : path}/${entry.name}`);
                } else {
                  // Set filename
                  if (fileNameInput) {
                    fileNameInput.value = entry.name;
                  }
                }
              });
              
              filesList.appendChild(item);
            });
            
            if (entries.length === 0) {
              filesList.innerHTML = '<div class="empty-dir">Empty directory</div>';
            }
          })
          .catch(err => {
            filesList.innerHTML = `<div class="error">Error: ${err.message}</div>`;
          });
      }
    };
    
    // Load initial path
    loadPath(currentPath);
    
    // Handle navigation button
    navBtn?.addEventListener('click', () => {
      if (pathInput) {
        loadPath(pathInput.value);
      }
    });
    
    // Handle path input enter key
    pathInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        loadPath(pathInput.value);
      }
    });
    
    // Handle save button
    saveBtn?.addEventListener('click', () => {
      if (!fileNameInput || !this.editor) return;
      
      const fileName = fileNameInput.value.trim();
      if (!fileName) {
        alert('Please enter a file name');
        return;
      }
      
      const filePath = `${currentPath === '/' ? '' : currentPath}/${fileName}`;
      const content = this.editor.value;
      
      this.os.getFileSystem().writeFile(filePath, content)
        .then(() => {
          this.currentFilePath = filePath;
          this.hasUnsavedChanges = false;
          this.updateStatus();
          closeDialog();
        })
        .catch(error => {
          console.error('Failed to save file:', error);
          alert(`Failed to save file: ${error.message}`);
        });
    });
    
    // Close dialog
    const closeDialog = () => {
      dialog.remove();
    };
    
    closeBtn?.addEventListener('click', closeDialog);
    cancelBtn?.addEventListener('click', closeDialog);
  }

  /**
   * Cut selected text
   */
  private cut(): void {
    if (!this.editor) return;
    
    // Execute the cut command
    document.execCommand('cut');
  }

  /**
   * Copy selected text
   */
  private copy(): void {
    if (!this.editor) return;
    
    // Execute the copy command
    document.execCommand('copy');
  }

  /**
   * Paste text from clipboard
   */
  private paste(): void {
    if (!this.editor) return;
    
    // Execute the paste command
    document.execCommand('paste');
  }

  /**
   * Show find panel
   */
  private showFindPanel(): void {
    this.showFindReplacePanel(false);
  }

  /**
   * Show replace panel
   */
  private showReplacePanel(): void {
    this.showFindReplacePanel(true);
  }

  /**
   * Show find/replace panel
   */
  private showFindReplacePanel(showReplace: boolean): void {
    // Remove existing panel if any
    this.removeFindReplacePanel();
    
    if (!this.container) return;
    
    // Create panel
    const panel = document.createElement('div');
    panel.className = 'find-replace-panel visible';
    panel.innerHTML = `
      <div>
        <input type="text" class="find-replace-input find-input" placeholder="Find" />
        ${showReplace ? '<input type="text" class="find-replace-input replace-input" placeholder="Replace with" />' : ''}
      </div>
      <div style="margin-top: 10px;">
        <button class="find-replace-btn find-btn">Find</button>
        <button class="find-replace-btn find-next-btn">Find Next</button>
        ${showReplace ? '<button class="find-replace-btn replace-btn">Replace</button>' : ''}
        ${showReplace ? '<button class="find-replace-btn replace-all-btn">Replace All</button>' : ''}
        <button class="find-replace-btn close-btn">Close</button>
      </div>
    `;
    
    this.container.querySelector('.text-editor-content')?.appendChild(panel);
    
    // Focus find input
    const findInput = panel.querySelector<HTMLInputElement>('.find-input');
    findInput?.focus();
    
    // Get selected text for initial search
    if (this.editor && this.editor.selectionStart !== this.editor.selectionEnd) {
      const selectedText = this.editor.value.substring(
        this.editor.selectionStart,
        this.editor.selectionEnd
      );
      
      if (findInput) {
        findInput.value = selectedText;
      }
    }
    
    // Add event listeners
    const closeBtn = panel.querySelector('.close-btn');
    const findBtn = panel.querySelector('.find-btn');
    const findNextBtn = panel.querySelector('.find-next-btn');
    const replaceBtn = panel.querySelector('.replace-btn');
    const replaceAllBtn = panel.querySelector('.replace-all-btn');
    
    closeBtn?.addEventListener('click', () => this.removeFindReplacePanel());
    findBtn?.addEventListener('click', () => this.findText(findInput?.value || ''));
    findNextBtn?.addEventListener('click', () => this.findNextText(findInput?.value || ''));
    
    if (showReplace) {
      const replaceInput = panel.querySelector<HTMLInputElement>('.replace-input');
      replaceBtn?.addEventListener('click', () => this.replaceText(
        findInput?.value || '',
        replaceInput?.value || ''
      ));
      replaceAllBtn?.addEventListener('click', () => this.replaceAllText(
        findInput?.value || '',
        replaceInput?.value || ''
      ));
    }
    
    // Handle Enter key in inputs
    findInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        this.findText(findInput.value);
      }
    });
  }

  /**
   * Remove find/replace panel
   */
  private removeFindReplacePanel(): void {
    const panel = this.container?.querySelector('.find-replace-panel');
    if (panel) {
      panel.remove();
    }
  }

  /**
   * Find text in editor
   */
  private findText(text: string, startPosition?: number): boolean {
    if (!this.editor || !text) return false;
    
    const content = this.editor.value;
    const start = startPosition !== undefined ? startPosition : this.editor.selectionEnd;
    
    const index = content.indexOf(text, start);
    
    if (index !== -1) {
      // Scroll to and select the found text
      this.editor.focus();
      this.editor.setSelectionRange(index, index + text.length);
      
      // Make sure the selection is visible
      this.scrollToSelection();
      
      return true;
    } else {
      // Try from the beginning if not found after the current position
      if (start > 0) {
        const fromStartIndex = content.indexOf(text, 0);
        
        if (fromStartIndex !== -1) {
          // Found from the beginning
          this.editor.focus();
          this.editor.setSelectionRange(fromStartIndex, fromStartIndex + text.length);
          
          // Make sure the selection is visible
          this.scrollToSelection();
          
          alert('Search reached the end of the file. Continuing from the top.');
          return true;
        }
      }
      
      alert('Text not found');
      return false;
    }
  }

  /**
   * Find next occurrence of text
   */
  private findNextText(text: string): boolean {
    if (!this.editor) return false;
    
    return this.findText(text, this.editor.selectionEnd);
  }

  /**
   * Replace selected text
   */
  private replaceText(findText: string, replaceText: string): void {
    if (!this.editor) return;
    
    // If text is selected and matches the search text, replace it
    const selectedText = this.editor.value.substring(
      this.editor.selectionStart,
      this.editor.selectionEnd
    );
    
    if (selectedText === findText) {
      const start = this.editor.selectionStart;
      
      // Replace the selected text
      this.editor.setRangeText(replaceText);
      
      // Update selection to after the replacement
      this.editor.setSelectionRange(start + replaceText.length, start + replaceText.length);
      
      this.hasUnsavedChanges = true;
      this.updateStatus();
      
      // Find the next occurrence
      this.findNextText(findText);
    } else {
      // If nothing is selected or doesn't match, find first
      this.findText(findText);
    }
  }

  /**
   * Replace all occurrences of text
   */
  private replaceAllText(findText: string, replaceText: string): void {
    if (!this.editor || !findText) return;
    
    const content = this.editor.value;
    const newContent = content.split(findText).join(replaceText);
    
    // Only update if there were changes
    if (newContent !== content) {
      this.editor.value = newContent;
      this.hasUnsavedChanges = true;
      this.updateStatus();
      
      // Count the number of replacements
      const count = (content.split(findText).length - 1);
      alert(`Replaced ${count} occurrence${count !== 1 ? 's' : ''}`);
    } else {
      alert('Text not found');
    }
  }

  /**
   * Scroll to current selection
   */
  private scrollToSelection(): void {
    if (!this.editor) return;
    
    // This is a simple way to ensure selected text is visible
    // by making the editor element scroll to the selection
    this.editor.blur();
    this.editor.focus();
  }

  /**
   * Update status bar
   */
  private updateStatus(): void {
    if (!this.statusBar) return;
    
    const filePathElement = this.statusBar.querySelector('.file-path');
    const statusElement = this.statusBar.querySelector('.editor-status');
    
    if (filePathElement) {
      filePathElement.textContent = this.currentFilePath || 'No file open';
    }
    
    if (statusElement) {
      statusElement.textContent = this.hasUnsavedChanges ? 'Unsaved changes' : 'Ready';
    }
    
    // Update window title if needed
    const filename = this.currentFilePath ? this.currentFilePath.split('/').pop() : 'Untitled';
    const title = `${filename}${this.hasUnsavedChanges ? ' *' : ''} - Text Editor`;
    
    // Dispatch an event to update the window title
    const event = new CustomEvent('app-title-change', {
      detail: {
        title: title
      }
    });
    document.dispatchEvent(event);
  }
}
