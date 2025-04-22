import { OS } from '../core/os';
import * as monaco from 'monaco-editor';
import { FileEntryUtils } from '../core/file-entry-utils';
import { GuiApplication } from '../core/gui-application';
import { ErrorHandler, ErrorLevel } from '../core/error-handler';

/**
 * Code Editor App for the Hacker Game
 * Provides advanced code editing features using Monaco Editor
 */
export class CodeEditorApp extends GuiApplication {
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
    super(os);
    
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'code-editor';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
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
             <!-- NOTE: DO NOT ADD STYLES HERE! 
     All styles for the code editor should be added to code-editor.less instead.
     This ensures proper scoping and prevents conflicts with other components. -->
      <style>
 <!-- NOTE: DO NOT ADD STYLES HERE! -->
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
  private async showOpenDialog(): Promise<void> {
    // Use the FilePicker dialog from DialogManager in the base class
    const filePickerOptions = {
      initialDirectory: this.currentWorkingDirectory,
      message: "Select a file to open"
    };
    
    const selectedPath = await this.dialogManager.FilePicker.Show("Open File", filePickerOptions);
    
    if (selectedPath) {
      this.openFile(selectedPath);
    }
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
  private async saveFileAs(): Promise<void> {
    if (!this.editor) return;
    
    // Get initial file name from current file path if available
    const initialFileName = this.currentFilePath ? 
      this.currentFilePath.split('/').pop() || 'untitled.js' : 'untitled.js';
    
    // Configure file picker for save mode
    const filePickerOptions = {
      initialDirectory: this.currentWorkingDirectory,
      initialFileName: initialFileName,
      message: "Choose location to save file",
      saveMode: true
    };
    
    // Show file picker dialog using the base class dialog manager
    const filePath = await this.dialogManager.FilePicker.Show("Save As", filePickerOptions);
    
    if (filePath) {
      // Check if file exists
      const exists = await this.os.getFileSystem().exists(filePath);
      
      if (exists) {
        // Ask for confirmation before overwriting
        const result = await this.dialogManager.Msgbox.Show(
          'Overwrite File',
          `File "${filePath.split('/').pop()}" already exists. Do you want to overwrite it?`,
          ['yes', 'no']
        );
        
        if (result === 'yes') {
          this.saveToPath(filePath);
        }
      } else {
        this.saveToPath(filePath);
      }
    }
  }
  /**
   * Save content to specified path
   */
  private saveToPath(filePath: string): void {
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
      })
      .catch(error => {
        console.error('Error saving file:', error);
        this.dialogManager.Msgbox.Show('Error', `Failed to save file: ${error.message}`, ['ok'], 'error');
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
  private async showNewFileDialog(): Promise<void> {
    // Show a prompt dialog to get the file name
    const fileName = await this.dialogManager.Prompt.Show(
      'New File',
      'Enter file name:',
      { defaultText: 'untitled.js' }
    );
    
    if (!fileName || !fileName.trim()) return;
    
    const filePath = `${this.currentWorkingDirectory === '/' ? '' : this.currentWorkingDirectory}/${fileName}`;
    
    try {
      // Check if file exists
      const exists = await this.os.getFileSystem().exists(filePath);
      
      if (exists) {
        // Show confirmation dialog if file exists
        const result = await this.dialogManager.Msgbox.Show(
          'File Exists',
          `File "${fileName}" already exists. Do you want to overwrite it?`,
          ['yes', 'no']
        );
        
        if (result === 'yes') {
          this.createNewFileAtPath(filePath);
        }
      } else {
        this.createNewFileAtPath(filePath);
      }
    } catch (error) {
      // var errorMessage = ErrorHandler.getErrorMessage(error);
      // console.error('Error checking file existence:', error);
      // this.dialogManager.Msgbox.Show(
      //   'Error', 
      //   `Failed to check if file exists: ${error.message}`, 
      //   ['ok'], 
      //   'error'
      // );
    
    }
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
        ErrorHandler.getInstance().parse(error, 'code-editor.ts', 'CodeEditorApp', {
          showDialog: true,
          dialogManager: this.dialogManager,
          dialogTitle: 'Error Creating File'
        });
      });
  }
  /**
   * Show input dialog
   */
  private async showInputDialog(
    title: string, 
    message: string, 
    defaultValue: string,
    onConfirm: (value: string) => void
  ): Promise<void> {
    // Use the dialogManager from GuiApplication base class
    const result = await this.dialogManager.Prompt.Show(title, message, {
      defaultText: defaultValue
    });
    
    if (result !== null) {
      onConfirm(result);
    }
  }
  /**
   * Show confirmation dialog
   */
  private async showConfirmDialog(
    title: string, 
    message: string, 
    onConfirm: () => void,
    onCancel?: () => void
  ): Promise<void> {
    // Use the dialogManager from GuiApplication base class
    const result = await this.dialogManager.Msgbox.Show(title, message, ['yes', 'no']);
    
    if (result === 'yes') {
      onConfirm();
    } else if (onCancel) {
      onCancel();
    }
  }
  /**
   * Show error dialog
   */
  private async showErrorDialog(title: string, message: string): Promise<void> {
    // Use the dialogManager from GuiApplication base class to show an error message box
    await this.dialogManager.Msgbox.Show(title, message, ['ok'], 'error');
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
