import { OS } from '../core/os';
import * as monaco from 'monaco-editor';
import { FileEntryUtils } from '../core/file-entry-utils';

/**
 * Code Editor App for the Hacker Game
 * Provides advanced code editing features using Monaco Editor
 */
export class CodeEditorApp {
  private os: OS;
  private container: HTMLElement | null = null;
  private editor: monaco.editor.IStandaloneCodeEditor | null = null;
  private editorContainer: HTMLElement | null = null;
  private currentFilePath: string | null = null;
  private hasUnsavedChanges: boolean = false;
  private monacoLoaded: boolean = false;
  private terminals: Map<string, HTMLElement> = new Map();
  private activeTerminal: string | null = null;
  private fileTree: HTMLElement | null = null;
  private currentWorkingDirectory: string = '/home/user';

  constructor(os: OS) {
    this.os = os;
  }

  /**
   * Initialize the code editor app
   */
  public init(container: HTMLElement): void {
    this.container = container;
    this.render();
    this.initMonaco();
  }

  /**
   * Render the code editor UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="code-editor-app">
        <div class="editor-toolbar">
          <div class="file-actions">
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
          </div>
          <div class="editor-path"></div>
          <div class="editor-actions">
            <button class="toolbar-btn run-code" title="Run Code">
              <span class="icon">▶️</span> Run
            </button>
            <select class="language-selector">
              <option value="javascript">JavaScript</option>
              <option value="typescript">TypeScript</option>
              <option value="html">HTML</option>
              <option value="css">CSS</option>
              <option value="json">JSON</option>
              <option value="python">Python</option>
              <option value="shell">Shell</option>
            </select>
          </div>
        </div>
        <div class="editor-main">
          <div class="editor-sidebar">
            <div class="sidebar-header">
              <span>Files</span>
              <div class="sidebar-actions">
                <button class="sidebar-btn refresh-files" title="Refresh">↻</button>
                <button class="sidebar-btn new-file-tree" title="New File">+</button>
              </div>
            </div>
            <div class="file-tree"></div>
          </div>
          <div class="editor-content-panel">
            <div class="monaco-container">
              <div class="editor-loading">Loading editor...</div>
            </div>
            <div class="editor-terminal-panel">
              <div class="terminal-tabs">
                <div class="terminal-tab active" data-terminal-id="terminal1">Terminal</div>
                <button class="add-terminal-btn">+</button>
              </div>
              <div class="terminal-container" data-terminal-id="terminal1"></div>
            </div>
          </div>
        </div>
        <div class="editor-statusbar">
          <div class="file-info">
            <span class="file-path">No file open</span>
            <span class="file-status"></span>
          </div>
          <div class="cursor-position">Ln 1, Col 1</div>
        </div>
      </div>
      <style>
        .code-editor-app {
          display: flex;
          flex-direction: column;
          height: 100%;
          background-color: #1e1e1e;
          color: #d4d4d4;
          font-family: 'Segoe UI', 'Arial', sans-serif;
        }
        
        .editor-toolbar {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 5px;
          background-color: #252526;
          border-bottom: 1px solid #3c3c3c;
        }
        
        .file-actions, .editor-actions {
          display: flex;
          gap: 5px;
        }
        
        .editor-path {
          flex: 1;
          padding: 0 10px;
          font-size: 12px;
          white-space: nowrap;
          overflow: hidden;
          text-overflow: ellipsis;
        }
        
        .toolbar-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          padding: 5px 10px;
          cursor: pointer;
          border-radius: 3px;
          display: flex;
          align-items: center;
          gap: 5px;
          font-size: 12px;
        }
        
        .toolbar-btn:hover {
          background-color: #3c3c3c;
        }
        
        .toolbar-btn:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }
        
        .language-selector {
          background-color: #3c3c3c;
          color: #d4d4d4;
          border: 1px solid #555;
          padding: 3px 5px;
          border-radius: 3px;
          cursor: pointer;
        }
        
        .editor-main {
          display: flex;
          flex: 1;
          overflow: hidden;
        }
        
        .editor-sidebar {
          width: 220px;
          background-color: #252526;
          border-right: 1px solid #3c3c3c;
          display: flex;
          flex-direction: column;
        }
        
        .sidebar-header {
          padding: 10px;
          font-size: 13px;
          font-weight: bold;
          display: flex;
          justify-content: space-between;
          align-items: center;
          border-bottom: 1px solid #3c3c3c;
        }
        
        .sidebar-actions {
          display: flex;
          gap: 5px;
        }
        
        .sidebar-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          cursor: pointer;
          border-radius: 3px;
          padding: 2px 5px;
          font-size: 12px;
        }
        
        .sidebar-btn:hover {
          background-color: #3c3c3c;
        }
        
        .file-tree {
          flex: 1;
          overflow: auto;
          padding: 5px;
          font-size: 13px;
        }
        
        .tree-item {
          padding: 3px 5px 3px 15px;
          cursor: pointer;
          border-radius: 3px;
          white-space: nowrap;
          overflow: hidden;
          text-overflow: ellipsis;
          position: relative;
        }
        
        .tree-item:hover {
          background-color: #2a2d2e;
        }
        
        .tree-item.active {
          background-color: #37373d;
        }
        
        .tree-folder {
          padding-left: 5px;
        }
        
        .tree-folder-header {
          cursor: pointer;
          padding: 3px 5px;
          border-radius: 3px;
          display: flex;
          align-items: center;
        }
        
        .tree-folder-header:hover {
          background-color: #2a2d2e;
        }
        
        .tree-folder-content {
          padding-left: 15px;
          display: none;
        }
        
        .tree-folder.expanded > .tree-folder-content {
          display: block;
        }
        
        .tree-folder-toggle {
          margin-right: 5px;
          font-size: 10px;
          width: 10px;
          display: inline-block;
        }
        
        .editor-content-panel {
          flex: 1;
          display: flex;
          flex-direction: column;
          overflow: hidden;
        }
        
        .monaco-container {
          flex: 1;
          position: relative;
        }
        
        .editor-loading {
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          color: #888;
        }
        
        .editor-terminal-panel {
          height: 200px;
          border-top: 1px solid #3c3c3c;
          display: flex;
          flex-direction: column;
          overflow: hidden;
        }
        
        .terminal-tabs {
          display: flex;
          background-color: #252526;
          border-bottom: 1px solid #3c3c3c;
        }
        
        .terminal-tab {
          padding: 5px 15px;
          font-size: 12px;
          cursor: pointer;
          border-right: 1px solid #3c3c3c;
        }
        
        .terminal-tab.active {
          background-color: #1e1e1e;
          border-top: 2px solid #007acc;
          padding-top: 4px;
        }
        
        .add-terminal-btn {
          background: none;
          border: none;
          color: #d4d4d4;
          cursor: pointer;
          padding: 5px 10px;
          font-size: 12px;
        }
        
        .code-editor-app .editor-terminal-panel .terminal-container {
          flex: 1;
          padding: 2px;
          display: none;
        }
        
        .code-editor-app .editor-terminal-panel .terminal-container.active {
          display: block;
        }
        
        .editor-statusbar {
          display: flex;
          justify-content: space-between;
          padding: 3px 10px;
          background-color: #007acc;
          color: white;
          font-size: 12px;
        }
        
        .file-status {
          margin-left: 10px;
        }
        
        /* Dialog styles */
        .dialog-overlay {
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background-color: rgba(0, 0, 0, 0.5);
          display: flex;
          justify-content: center;
          align-items: center;
          z-index: 1000;
        }
        
        .dialog {
          background-color: #252526;
          border: 1px solid #3c3c3c;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
          width: 550px;
          border-radius: 4px;
          overflow: hidden;
        }
        
        .dialog-header {
          background-color: #333;
          padding: 10px 15px;
          font-weight: bold;
          border-bottom: 1px solid #3c3c3c;
        }
        
        .dialog-content {
          padding: 15px;
          max-height: 500px;
          overflow: auto;
        }
        
        .dialog-footer {
          padding: 10px 15px;
          text-align: right;
          border-top: 1px solid #3c3c3c;
        }
        
        .dialog-btn {
          padding: 5px 15px;
          margin-left: 10px;
          background-color: #0e639c;
          color: white;
          border: none;
          border-radius: 3px;
          cursor: pointer;
        }
        
        .dialog-btn.cancel {
          background-color: #3c3c3c;
        }
        
        .dialog-btn:hover {
          background-color: #1177bb;
        }
        
        .dialog-btn.cancel:hover {
          background-color: #515151;
        }
        
        .file-list {
          max-height: 300px;
          overflow-y: auto;
        }
        
        .file-item {
          padding: 5px 10px;
          cursor: pointer;
          border-radius: 3px;
          display: flex;
          align-items: center;
        }
        
        .file-item:hover {
          background-color: #2a2d2e;
        }
        
        .file-item.selected {
          background-color: #0e639c;
        }
        
        .file-icon {
          margin-right: 5px;
        }
        
        .path-bar {
          display: flex;
          margin-bottom: 10px;
        }
        
        .path-input {
          flex: 1;
          background-color: #3c3c3c;
          border: 1px solid #555;
          color: #d4d4d4;
          padding: 5px 10px;
          border-radius: 3px;
        }
        
        .path-go-btn {
          background-color: #0e639c;
          color: white;
          border: none;
          padding: 5px 10px;
          border-radius: 3px;
          margin-left: 5px;
          cursor: pointer;
        }
        
        .unsaved-indicator::after {
          content: '*';
          margin-left: 3px;
        }
      </style>
    `;
    
    this.editorContainer = this.container.querySelector('.monaco-container');
    this.fileTree = this.container.querySelector('.file-tree');
    
    // Create initial terminal
    this.terminals.set('terminal1', this.container.querySelector('.terminal-container[data-terminal-id="terminal1"]')!);
    this.activeTerminal = 'terminal1';
    
    this.setupEventListeners();
    this.loadFileTree();
  }  /**
   * Initialize Monaco editor
   */
  private initMonaco(): void {
    if (!this.editorContainer) return;
    
    // Configure Monaco workers
    (window as any).MonacoEnvironment = {
      getWorker: function(_moduleId: string, label: string) {
        // Use a proxied worker that doesn't rely on specific worker files
        // This is a workaround when worker files aren't properly bundled
        const workerContent = `
          self.MonacoEnvironment = {
            baseUrl: 'https://unpkg.com/monaco-editor@latest/min/'
          };
          importScripts('https://unpkg.com/monaco-editor@latest/min/vs/base/worker/workerMain.js');
        `;
        const blob = new Blob([workerContent], { type: 'application/javascript' });
        return new Worker(URL.createObjectURL(blob));
      }
    };
    
    // Create editor instance
    this.editor = monaco.editor.create(this.editorContainer, {
      value: '',
      language: 'javascript',
      theme: 'vs-dark',
      automaticLayout: true,
      minimap: {
        enabled: true
      },
      scrollBeyondLastLine: false,
      fontSize: 14,
      tabSize: 2,
      renderLineHighlight: 'all',
      lineNumbers: 'on',
      wordWrap: 'on'
    });
    
    // Hide loading message
    const loadingEl = this.editorContainer.querySelector('.editor-loading');
    if (loadingEl) loadingEl.remove();
    
    // Track editor changes
    this.editor.onDidChangeModelContent(() => {
      if (!this.hasUnsavedChanges) {
        this.hasUnsavedChanges = true;
        this.updateStatusBar();
      }
    });
    
    // Track cursor position
    this.editor.onDidChangeCursorPosition((e) => {
      this.updateCursorPosition(e.position);
    });
    
    this.monacoLoaded = true;
    
    // Setup keyboard shortcuts
    this.editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
      this.saveFile();
    });
    
    // Set initial cursor position
    this.updateCursorPosition({ lineNumber: 1, column: 1 });
  }

  /**
   * Setup event listeners
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // File operations
    const newFileBtn = this.container.querySelector('.toolbar-btn.new-file');
    const openFileBtn = this.container.querySelector('.toolbar-btn.open-file');
    const saveFileBtn = this.container.querySelector('.toolbar-btn.save-file');
    const saveAsBtn = this.container.querySelector('.toolbar-btn.save-as');
    const runCodeBtn = this.container.querySelector('.toolbar-btn.run-code');
    
    // File tree operations
    const refreshFilesBtn = this.container.querySelector('.sidebar-btn.refresh-files');
    const newFileTreeBtn = this.container.querySelector('.sidebar-btn.new-file-tree');
    
    // Terminal operations
    const addTerminalBtn = this.container.querySelector('.add-terminal-btn');
    const terminalTabs = this.container.querySelector('.terminal-tabs');
    
    // Language selector
    const languageSelector = this.container.querySelector<HTMLSelectElement>('.language-selector');
    
    // File operations
    newFileBtn?.addEventListener('click', () => this.newFile());
    openFileBtn?.addEventListener('click', () => this.openFileDialog());
    saveFileBtn?.addEventListener('click', () => this.saveFile());
    saveAsBtn?.addEventListener('click', () => this.saveFileAs());
    runCodeBtn?.addEventListener('click', () => this.runCode());
    
    // File tree operations
    refreshFilesBtn?.addEventListener('click', () => this.loadFileTree());
    newFileTreeBtn?.addEventListener('click', () => this.showNewFileDialog());
    
    // Terminal operations
    addTerminalBtn?.addEventListener('click', () => this.addTerminal());
    terminalTabs?.addEventListener('click', (e) => {
      const target = e.target as HTMLElement;
      if (target.classList.contains('terminal-tab')) {
        const terminalId = target.getAttribute('data-terminal-id');
        if (terminalId) this.activateTerminal(terminalId);
      }
    });
    
    // Language selector
    languageSelector?.addEventListener('change', () => {
      if (this.editor && languageSelector.value) {
        monaco.editor.setModelLanguage(this.editor.getModel()!, languageSelector.value);
      }
    });
  }

  /**
   * Load file tree
   */
  private loadFileTree(): void {
    if (!this.fileTree) return;
    
    this.fileTree.innerHTML = '<div class="loading">Loading...</div>';
    
    this.renderDirectoryInTree(this.currentWorkingDirectory, this.fileTree, true);
  }

  /**
   * Render directory contents in file tree
   */
  private renderDirectoryInTree(path: string, parentElement: HTMLElement, isRoot: boolean = false): void {
    this.os.getFileSystem().readDirectory(path)
      .then(entries => {
        // Clear if this is the root
        if (isRoot) {
          parentElement.innerHTML = '';
        }
        
        // Sort entries: directories first, then files
        entries.sort((a, b) => {
          if (FileEntryUtils.isDirectory(a) && !FileEntryUtils.isDirectory(b)) return -1;
          if (!FileEntryUtils.isDirectory(a) && FileEntryUtils.isDirectory(b)) return 1;
          return a.name.localeCompare(b.name);
        });
        
        // Add entries to the tree
        entries.forEach(entry => {
          const fullPath = `${path === '/' ? '' : path}/${entry.name}`;
          
          if (FileEntryUtils.isDirectory(entry)) {
            // Directory
            const folderEl = document.createElement('div');
            folderEl.className = 'tree-folder';
            folderEl.innerHTML = `
              <div class="tree-folder-header">
                <span class="tree-folder-toggle">▶</span>
                <span class="file-icon">📁</span>
                <span class="folder-name">${entry.name}</span>
              </div>
              <div class="tree-folder-content"></div>
            `;
            
            const headerEl = folderEl.querySelector('.tree-folder-header');
            const contentEl = folderEl.querySelector('.tree-folder-content') as HTMLElement;
            const toggleEl = folderEl.querySelector('.tree-folder-toggle');
            
            headerEl?.addEventListener('click', () => {
              const isExpanded = folderEl.classList.toggle('expanded');
              if (toggleEl) toggleEl.textContent = isExpanded ? '▼' : '▶';
              
              // Load content if expanding and not loaded yet
              if (isExpanded && contentEl && contentEl.children.length === 0) {
                this.renderDirectoryInTree(fullPath, contentEl);
              }
            });
            
            parentElement.appendChild(folderEl);
          } else {
            // File
            const fileEl = document.createElement('div');
            fileEl.className = 'tree-item';
            
            // Get appropriate icon based on file type
            const icon = this.getFileIcon(entry.name);
            
            fileEl.innerHTML = `
              <span class="file-icon">${icon}</span>
              <span class="file-name">${entry.name}</span>
            `;
            
            fileEl.addEventListener('click', () => {
              // Mark as active
              this.container?.querySelectorAll('.tree-item.active').forEach(item => {
                item.classList.remove('active');
              });
              fileEl.classList.add('active');
              
              // Open the file
              this.openFile(fullPath);
            });
            
            parentElement.appendChild(fileEl);
          }
        });
        
        // Show empty message if no entries
        if (entries.length === 0 && isRoot) {
          parentElement.innerHTML = '<div class="empty-message">No files found</div>';
        }
      })
      .catch(error => {
        console.error('Error loading directory:', error);
        parentElement.innerHTML = `<div class="error">Error: ${error.message}</div>`;
      });
  }

  /**
   * Get appropriate file icon
   */
  private getFileIcon(fileName: string): string {
    const extension = fileName.split('.').pop()?.toLowerCase();
    
    // Programming languages
    if (['js', 'ts', 'jsx', 'tsx'].includes(extension || '')) return '📄';
    if (['html', 'htm', 'xml'].includes(extension || '')) return '📄';
    if (['css', 'scss', 'sass', 'less'].includes(extension || '')) return '📄';
    if (['json', 'yaml', 'yml', 'toml'].includes(extension || '')) return '📄';
    if (['py', 'rb', 'php', 'java', 'c', 'cpp', 'cs', 'go'].includes(extension || '')) return '📄';
    
    // Config files
    if (['gitignore', 'env', 'editorconfig'].includes(extension || '')) return '⚙️';
    
    // Images
    if (['png', 'jpg', 'jpeg', 'gif', 'svg', 'bmp'].includes(extension || '')) return '🖼️';
    
    // Documents
    if (['md', 'txt', 'rtf', 'pdf', 'doc', 'docx'].includes(extension || '')) return '📝';
    
    // Default
    return '📄';
  }

  /**
   * Create a new file
   */
  private newFile(): void {
    if (this.hasUnsavedChanges) {
      this.showConfirmDialog(
        'Unsaved Changes',
        'You have unsaved changes. Do you want to continue?',
        () => this.createNewFile()
      );
    } else {
      this.createNewFile();
    }
  }

  /**
   * Create a new file implementation
   */
  private createNewFile(): void {
    if (!this.editor) return;
    
    this.editor.setValue('');
    this.currentFilePath = null;
    this.hasUnsavedChanges = false;
    this.updateLanguageFromFilename('');
    this.updateStatusBar();
  }

  /**
   * Open file dialog
   */
  private openFileDialog(): void {
    if (this.hasUnsavedChanges) {
      this.showConfirmDialog(
        'Unsaved Changes',
        'You have unsaved changes. Do you want to continue?',
        () => this.showOpenDialog()
      );
    } else {
      this.showOpenDialog();
    }
  }

  /**
   * Show open file dialog
   */
  private showOpenDialog(): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">Open File</div>
        <div class="dialog-content">
          <div class="path-bar">
            <input type="text" class="path-input" value="${this.currentWorkingDirectory}">
            <button class="path-go-btn">Go</button>
          </div>
          <div class="file-list"></div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn open" disabled>Open</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    const pathInput = dialog.querySelector<HTMLInputElement>('.path-input');
    const pathGoBtn = dialog.querySelector('.path-go-btn');
    const fileList = dialog.querySelector('.file-list');
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const openBtn = dialog.querySelector<HTMLButtonElement>('.dialog-btn.open');
    
    let selectedPath: string | null = null;
    
    // Load files in the current directory
    const loadDirectory = (path: string) => {
      if (!fileList) return;
      
      fileList.innerHTML = '<div class="loading">Loading...</div>';
      
      this.os.getFileSystem().readDirectory(path)
        .then(entries => {
          // Sort entries
          entries.sort((a, b) => {
            if (FileEntryUtils.isDirectory(a) && !FileEntryUtils.isDirectory(b)) return -1;
            if (!FileEntryUtils.isDirectory(a) && FileEntryUtils.isDirectory(b)) return 1;
            return a.name.localeCompare(b.name);
          });
          
          // Show parent directory option if not at root
          fileList.innerHTML = '';
          if (path !== '/') {
            const parentItem = document.createElement('div');
            parentItem.className = 'file-item parent-dir';
            parentItem.innerHTML = `
              <span class="file-icon">📁</span>
              <span class="file-name">..</span>
            `;
            parentItem.addEventListener('click', () => {
              const parentPath = path.substring(0, path.lastIndexOf('/')) || '/';
              if (pathInput) pathInput.value = parentPath;
              loadDirectory(parentPath);
            });
            fileList.appendChild(parentItem);
          }
          
          // Add entries
          entries.forEach(entry => {
            const fullPath = `${path === '/' ? '' : path}/${entry.name}`;
            const item = document.createElement('div');
            item.className = 'file-item';
              const icon = FileEntryUtils.isDirectory(entry) ? '📁' : this.getFileIcon(entry.name);
            
            item.innerHTML = `
              <span class="file-icon">${icon}</span>
              <span class="file-name">${entry.name}</span>
            `;
            
            item.addEventListener('click', () => {
              // Clear previous selection
              dialog.querySelectorAll('.file-item.selected').forEach(el => {
                el.classList.remove('selected');
              });
              
              if (FileEntryUtils.isDirectory(entry)) {
                // Navigate to directory
                if (pathInput) pathInput.value = fullPath;
                loadDirectory(fullPath);
                
                if (openBtn) openBtn.disabled = true;
                selectedPath = null;
              } else {
                // Select file
                item.classList.add('selected');
                selectedPath = fullPath;
                
                if (openBtn) openBtn.disabled = false;
              }
            });
            
            // Double-click to open
            item.addEventListener('dblclick', () => {
              if (FileEntryUtils.isDirectory(entry)) {
                if (pathInput) pathInput.value = fullPath;
                loadDirectory(fullPath);
              } else {
                dialog.remove();
                this.openFile(fullPath);
              }
            });
            
            fileList.appendChild(item);
          });
          
          if (entries.length === 0) {
            fileList.innerHTML += '<div class="empty-directory">Empty directory</div>';
          }
        })
        .catch(error => {
          console.error('Error loading directory:', error);
          fileList.innerHTML = `<div class="error">Error: ${error.message}</div>`;
        });
    };
    
    // Initial load
    loadDirectory(this.currentWorkingDirectory);
    
    // Path navigation
    pathGoBtn?.addEventListener('click', () => {
      if (pathInput) {
        loadDirectory(pathInput.value);
      }
    });
    
    pathInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        loadDirectory(pathInput.value);
      }
    });
    
    // Button handlers
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
    });
    
    openBtn?.addEventListener('click', () => {
      if (selectedPath) {
        dialog.remove();
        this.openFile(selectedPath);
      }
    });
  }

  /**
   * Open a file
   */
  public openFile(filePath: string): void {
    if (!this.editor) return;
    
    this.os.getFileSystem().readFile(filePath)
      .then(content => {
        this.editor?.setValue(content);
        this.currentFilePath = filePath;
        this.hasUnsavedChanges = false;
        this.updateLanguageFromFilename(filePath);
        this.updateStatusBar();
        
        // Update working directory
        this.currentWorkingDirectory = filePath.substring(0, filePath.lastIndexOf('/')) || '/';
      })
      .catch(error => {
        console.error('Error opening file:', error);
        this.showErrorDialog('Error', `Failed to open file: ${error.message}`);
      });
  }

  /**
   * Update editor language based on filename
   */
  private updateLanguageFromFilename(filePath: string): void {
    if (!this.editor) return;
    
    const fileName = filePath.split('/').pop() || '';
    const extension = fileName.split('.').pop()?.toLowerCase() || '';
    
    // Update language selector
    const languageSelector = this.container?.querySelector<HTMLSelectElement>('.language-selector');
    
    let language = 'plaintext';
    
    // Map file extensions to languages
    if (['js'].includes(extension)) language = 'javascript';
    else if (['ts'].includes(extension)) language = 'typescript';
    else if (['jsx', 'tsx'].includes(extension)) language = 'typescript';
    else if (['html', 'htm'].includes(extension)) language = 'html';
    else if (['css'].includes(extension)) language = 'css';
    else if (['json'].includes(extension)) language = 'json';
    else if (['py'].includes(extension)) language = 'python';
    else if (['sh', 'bash'].includes(extension)) language = 'shell';
    
    // Set language in editor
    monaco.editor.setModelLanguage(this.editor.getModel()!, language);
    
    // Update selector if available
    if (languageSelector && languageSelector.querySelector(`option[value="${language}"]`)) {
      languageSelector.value = language;
    }
  }

  /**
   * Save current file
   */
  private saveFile(): void {
    if (!this.editor) return;
    
    if (!this.currentFilePath) {
      this.saveFileAs();
      return;
    }
    
    const content = this.editor.getValue();
    
    this.os.getFileSystem().writeFile(this.currentFilePath, content)
      .then(() => {
        this.hasUnsavedChanges = false;
        this.updateStatusBar();
      })
      .catch(error => {
        console.error('Error saving file:', error);
        this.showErrorDialog('Error', `Failed to save file: ${error.message}`);
      });
  }

  /**
   * Save file as
   */
  private saveFileAs(): void {
    if (!this.editor) return;
    
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">Save As</div>
        <div class="dialog-content">
          <div class="path-bar">
            <input type="text" class="path-input" value="${this.currentWorkingDirectory}">
            <button class="path-go-btn">Go</button>
          </div>
          <div class="file-list"></div>
          <div class="filename-row" style="margin-top: 10px;">
            <label for="filename">File name:</label>
            <input type="text" id="filename" class="path-input filename-input" value="${this.currentFilePath ? this.currentFilePath.split('/').pop() || '' : 'untitled.js'}">
          </div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn save">Save</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    const pathInput = dialog.querySelector<HTMLInputElement>('.path-input');
    const pathGoBtn = dialog.querySelector('.path-go-btn');
    const fileList = dialog.querySelector('.file-list');
    const fileNameInput = dialog.querySelector<HTMLInputElement>('.filename-input');
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const saveBtn = dialog.querySelector('.dialog-btn.save');
    
    // Load files in the directory
    const loadDirectory = (path: string) => {
      if (!fileList) return;
      
      fileList.innerHTML = '<div class="loading">Loading...</div>';
      
      this.os.getFileSystem().readDirectory(path)
        .then(entries => {
          // Sort entries
          entries.sort((a, b) => {
            if (FileEntryUtils.isDirectory(a) && !FileEntryUtils.isDirectory(b)) return -1;
            if (!FileEntryUtils.isDirectory(a) && FileEntryUtils.isDirectory(b)) return 1;
            return a.name.localeCompare(b.name);
          });
          
          // Show parent directory option if not at root
          fileList.innerHTML = '';
          if (path !== '/') {
            const parentItem = document.createElement('div');
            parentItem.className = 'file-item parent-dir';
            parentItem.innerHTML = `
              <span class="file-icon">📁</span>
              <span class="file-name">..</span>
            `;
            parentItem.addEventListener('click', () => {
              const parentPath = path.substring(0, path.lastIndexOf('/')) || '/';
              if (pathInput) pathInput.value = parentPath;
              loadDirectory(parentPath);
            });
            fileList.appendChild(parentItem);
          }
          
          // Add entries
          entries.forEach(entry => {
            const fullPath = `${path === '/' ? '' : path}/${entry.name}`;
            const item = document.createElement('div');
            item.className = 'file-item';
            
            const icon = FileEntryUtils.isDirectory(entry) ? '📁' : this.getFileIcon(entry.name);
            
            item.innerHTML = `
              <span class="file-icon">${icon}</span>
              <span class="file-name">${entry.name}</span>
            `;
            
            item.addEventListener('click', () => {
              if (FileEntryUtils.isDirectory(entry)) {
                // Navigate to directory
                if (pathInput) pathInput.value = fullPath;
                loadDirectory(fullPath);
              } else {
                // Set filename
                if (fileNameInput) {
                  fileNameInput.value = entry.name;
                }
              }
            });
            
            fileList.appendChild(item);
          });
          
          if (entries.length === 0) {
            fileList.innerHTML += '<div class="empty-directory">Empty directory</div>';
          }
        })
        .catch(error => {
          console.error('Error loading directory:', error);
          fileList.innerHTML = `<div class="error">Error: ${error.message}</div>`;
        });
    };
    
    // Initial load
    loadDirectory(this.currentWorkingDirectory);
    
    // Path navigation
    pathGoBtn?.addEventListener('click', () => {
      if (pathInput) {
        loadDirectory(pathInput.value);
      }
    });
    
    pathInput?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        loadDirectory(pathInput.value);
      }
    });
    
    // Button handlers
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
    });
    
    saveBtn?.addEventListener('click', () => {
      if (!pathInput || !fileNameInput) return;
      
      const directoryPath = pathInput.value;
      const fileName = fileNameInput.value.trim();
      
      if (!fileName) {
        this.showErrorDialog('Error', 'Please enter a file name');
        return;
      }
      
      const filePath = `${directoryPath === '/' ? '' : directoryPath}/${fileName}`;
      
      // Check if file exists
      this.os.getFileSystem().exists(filePath)
        .then(exists => {
          if (exists) {
            this.showConfirmDialog(
              'Overwrite File',
              `File "${fileName}" already exists. Do you want to overwrite it?`,
              () => this.saveToPath(filePath, dialog)
            );
          } else {
            this.saveToPath(filePath, dialog);
          }
        })
        .catch(error => {
          console.error('Error checking file existence:', error);
          this.showErrorDialog('Error', `Failed to check if file exists: ${error.message}`);
        });
    });
  }

  /**
   * Save content to specified path
   */
  private saveToPath(filePath: string, dialogToRemove?: HTMLElement): void {
    if (!this.editor) return;
    
    const content = this.editor.getValue();
    
    this.os.getFileSystem().writeFile(filePath, content)
      .then(() => {
        this.currentFilePath = filePath;
        this.hasUnsavedChanges = false;
        this.updateStatusBar();
        this.updateLanguageFromFilename(filePath);
        
        // Update working directory
        this.currentWorkingDirectory = filePath.substring(0, filePath.lastIndexOf('/')) || '/';
        
        // Refresh file tree
        this.loadFileTree();
        
        // Remove dialog if provided
        if (dialogToRemove) {
          dialogToRemove.remove();
        }
      })
      .catch(error => {
        console.error('Error saving file:', error);
        this.showErrorDialog('Error', `Failed to save file: ${error.message}`);
      });
  }

  /**
   * Run the current code
   */
  private runCode(): void {
    if (!this.editor || !this.activeTerminal) return;
    
    // Save file first if has unsaved changes
    if (this.hasUnsavedChanges) {
      this.showConfirmDialog(
        'Unsaved Changes',
        'Save changes before running?',
        () => {
          this.saveFile();
          this.executeCode();
        },
        () => {
          this.executeCode();
        }
      );
    } else {
      this.executeCode();
    }
  }

  /**
   * Execute the current code
   */
  private executeCode(): void {
    if (!this.editor || !this.currentFilePath) {
      this.showErrorDialog('Error', 'Please save the file before running');
      return;
    }
    
    const terminalContainer = this.terminals.get(this.activeTerminal || 'terminal1');
    if (!terminalContainer) return;
    
    // Get file extension to determine how to run
    const extension = this.currentFilePath.split('.').pop()?.toLowerCase();
    
    // Create terminal app if not exists
    if (!terminalContainer.querySelector('.terminal-app')) {
      import('../apps/terminal').then(module => {
        const terminalApp = new module.TerminalApp(this.os);
        terminalApp.init(terminalContainer);
        
        // Execute command based on file type
        this.runFileInTerminal(terminalApp, extension);
      });
    } else {
      // Get terminal app instance
      const terminalApp = (terminalContainer as any).terminalApp;
      if (terminalApp) {
        this.runFileInTerminal(terminalApp, extension);
      }
    }
  }

  /**
   * Run file in terminal based on extension
   */
  private runFileInTerminal(terminalApp: any, extension?: string): void {
    if (!this.currentFilePath) return;
    
    let command = '';
    
    switch (extension) {
      case 'js':
        command = `node ${this.currentFilePath}`;
        break;
      case 'ts':
        command = `ts-node ${this.currentFilePath}`;
        break;
      case 'py':
        command = `python ${this.currentFilePath}`;
        break;
      case 'sh':
        command = `sh ${this.currentFilePath}`;
        break;
      case 'html':
        command = `open ${this.currentFilePath}`;
        break;
      default:
        command = `cat ${this.currentFilePath}`;
    }
    
    // Execute the command
    terminalApp.executeCommand(command);
  }

  /**
   * Add a new terminal
   */
  private addTerminal(): void {
    const terminalId = `terminal${this.terminals.size + 1}`;
    
    // Create tab
    const terminalTabs = this.container?.querySelector('.terminal-tabs');
    const newTab = document.createElement('div');
    newTab.className = 'terminal-tab';
    newTab.setAttribute('data-terminal-id', terminalId);
    newTab.textContent = `Terminal ${this.terminals.size + 1}`;
    
    // Insert before the add button
    const addTerminalBtn = this.container?.querySelector('.add-terminal-btn');
    if (addTerminalBtn) {
      terminalTabs?.insertBefore(newTab, addTerminalBtn);
    }
    
    // Create container
    const terminalPanel = this.container?.querySelector('.editor-terminal-panel');
    const newContainer = document.createElement('div');
    newContainer.className = 'terminal-container';
    newContainer.setAttribute('data-terminal-id', terminalId);
    terminalPanel?.appendChild(newContainer);
    
    // Store reference
    this.terminals.set(terminalId, newContainer);
    
    // Activate the new terminal
    this.activateTerminal(terminalId);
  }

  /**
   * Activate a terminal tab
   */
  private activateTerminal(terminalId: string): void {
    // Update tabs
    this.container?.querySelectorAll('.terminal-tab').forEach(tab => {
      tab.classList.toggle('active', tab.getAttribute('data-terminal-id') === terminalId);
    });
    
    // Update containers
    this.container?.querySelectorAll('.terminal-container').forEach(container => {
      container.classList.toggle('active', container.getAttribute('data-terminal-id') === terminalId);
    });
    
    this.activeTerminal = terminalId;
    
    // Initialize terminal if not already done
    const terminalContainer = this.terminals.get(terminalId);
    if (terminalContainer && !terminalContainer.querySelector('.terminal-app')) {
      import('../apps/terminal').then(module => {
        const terminalApp = new module.TerminalApp(this.os);
        terminalApp.init(terminalContainer);
        
        // Store reference to terminal app
        (terminalContainer as any).terminalApp = terminalApp;
      });
    }
  }

  /**
   * Update cursor position display
   */
  private updateCursorPosition(position: { lineNumber: number; column: number }): void {
    const cursorPositionEl = this.container?.querySelector('.cursor-position');
    if (cursorPositionEl) {
      cursorPositionEl.textContent = `Ln ${position.lineNumber}, Col ${position.column}`;
    }
  }

  /**
   * Update status bar
   */
  private updateStatusBar(): void {
    const filePathEl = this.container?.querySelector('.file-path');
    const fileStatusEl = this.container?.querySelector('.file-status');
    const editorPathEl = this.container?.querySelector('.editor-path');
    
    if (filePathEl) {
      const fileName = this.currentFilePath ? 
        this.currentFilePath.split('/').pop() : 'untitled';
      
      if (this.hasUnsavedChanges) {
        filePathEl.textContent = fileName + ' *';
      } else {
        filePathEl.textContent = fileName || 'No file open';
      }
    }
    
    if (fileStatusEl) {
      fileStatusEl.textContent = this.hasUnsavedChanges ? 'Unsaved changes' : '';
    }
    
    if (editorPathEl) {
      editorPathEl.textContent = this.currentFilePath || '';
    }
    
    // Update window title
    const fileName = this.currentFilePath ? 
      this.currentFilePath.split('/').pop() : 'untitled';
    
    const title = `${fileName}${this.hasUnsavedChanges ? ' *' : ''} - Code Editor`;
    
    // Dispatch an event to update the window title
    const event = new CustomEvent('app-title-change', {
      detail: {
        title: title
      }
    });
    document.dispatchEvent(event);
  }

  /**
   * Show new file dialog in the current directory
   */
  private showNewFileDialog(): void {
    this.showInputDialog(
      'New File',
      'Enter file name:',
      'untitled.js',
      (fileName) => {
        if (!fileName.trim()) return;
        
        const filePath = `${this.currentWorkingDirectory === '/' ? '' : this.currentWorkingDirectory}/${fileName}`;
        
        // Check if file exists
        this.os.getFileSystem().exists(filePath)
          .then(exists => {
            if (exists) {
              this.showConfirmDialog(
                'File Exists',
                `File "${fileName}" already exists. Do you want to overwrite it?`,
                () => this.createNewFileAtPath(filePath)
              );
            } else {
              this.createNewFileAtPath(filePath);
            }
          })
          .catch(error => {
            console.error('Error checking file existence:', error);
            this.showErrorDialog('Error', `Failed to check if file exists: ${error.message}`);
          });
      }
    );
  }

  /**
   * Create a new file at the specified path
   */
  private createNewFileAtPath(filePath: string): void {
    this.os.getFileSystem().writeFile(filePath, '')
      .then(() => {
        // Refresh file tree
        this.loadFileTree();
        
        // Open the new file
        this.openFile(filePath);
      })
      .catch(error => {
        console.error('Error creating file:', error);
        this.showErrorDialog('Error', `Failed to create file: ${error.message}`);
      });
  }

  /**
   * Show input dialog
   */
  private showInputDialog(
    title: string, 
    message: string, 
    defaultValue: string,
    onConfirm: (value: string) => void
  ): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message">${message}</div>
          <input type="text" class="path-input dialog-input" value="${this.escapeHtml(defaultValue)}">
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn confirm">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    // Focus input
    const input = dialog.querySelector<HTMLInputElement>('.dialog-input');
    input?.focus();
    input?.select();
    
    // Button handlers
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const confirmBtn = dialog.querySelector('.dialog-btn.confirm');
    
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
    });
    
    confirmBtn?.addEventListener('click', () => {
      if (input) {
        onConfirm(input.value);
      }
      dialog.remove();
    });
    
    // Handle enter key
    input?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        onConfirm(input.value);
        dialog.remove();
      }
    });
  }

  /**
   * Show confirmation dialog
   */
  private showConfirmDialog(
    title: string, 
    message: string, 
    onConfirm: () => void,
    onCancel?: () => void
  ): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message">${message}</div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn cancel">Cancel</button>
          <button class="dialog-btn confirm">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    // Button handlers
    const cancelBtn = dialog.querySelector('.dialog-btn.cancel');
    const confirmBtn = dialog.querySelector('.dialog-btn.confirm');
    
    cancelBtn?.addEventListener('click', () => {
      dialog.remove();
      if (onCancel) onCancel();
    });
    
    confirmBtn?.addEventListener('click', () => {
      dialog.remove();
      onConfirm();
    });
  }

  /**
   * Show error dialog
   */
  private showErrorDialog(title: string, message: string): void {
    const dialog = document.createElement('div');
    dialog.className = 'dialog-overlay';
    dialog.innerHTML = `
      <div class="dialog">
        <div class="dialog-header">${title}</div>
        <div class="dialog-content">
          <div class="dialog-message" style="color: #ff6b6b;">${message}</div>
        </div>
        <div class="dialog-footer">
          <button class="dialog-btn confirm">OK</button>
        </div>
      </div>
    `;
    
    this.container?.appendChild(dialog);
    
    // Button handler
    const confirmBtn = dialog.querySelector('.dialog-btn.confirm');
    confirmBtn?.addEventListener('click', () => {
      dialog.remove();
    });
  }

  /**
   * Escape HTML special characters
   */
  private escapeHtml(str: string): string {
    return str
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }
}
