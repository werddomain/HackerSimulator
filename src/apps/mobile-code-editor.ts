// Mobile Code Editor App for the Hacker Game
// Provides touch-friendly code editing features using Monaco Editor
import { OS } from '../core/os';
import * as monaco from 'monaco-editor';
import { FileEntryUtils } from '../core/file-entry-utils';
import { GuiApplication } from '../core/gui-application';
import { ErrorHandler, ErrorLevel } from '../core/error-handler';
import { VirtualKeyboard } from '../core/virtual-keyboard';
import { GestureDetector, GestureType } from '../core/gesture-detector';

/**
 * Mobile-friendly Code Editor App
 * Optimized for touch interactions on smaller screens
 */
export class MobileCodeEditorApp extends GuiApplication {
  private editor: monaco.editor.IStandaloneCodeEditor | null = null;
  private editorContainer: HTMLElement | null = null;
  private currentFilePath: string | null = null;
  private hasUnsavedChanges: boolean = false;
  private monacoLoaded: boolean = false;
  private openFiles: Map<string, {content: string, model: monaco.editor.ITextModel}> = new Map();
  private currentWorkingDirectory: string = '/home/user';
  private fileTreeVisible: boolean = false;
  private virtualKeyboard: VirtualKeyboard | null = null;
  private gestureDetector: GestureDetector | null = null;
  private headerElement: HTMLElement | null = null;
  private fileTreeContainer: HTMLElement | null = null;
  private tabsContainer: HTMLElement | null = null;
  private specialKeysToolbar: HTMLElement | null = null;

  constructor(os: OS) {
    super(os);
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'mobile-code-editor';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    this.initMonaco();
    this.initGestureDetection();
    
    // Check if we have arguments to process after Monaco is initialized
    if (this.commandArgs.length > 0) {
      // First check if it's a directory
      this.os.getFileSystem().stat(this.commandArgs[0])
        .then(stat => {
          if (FileEntryUtils.isDirectory(stat)) {
            // If it's a directory, set it as the working directory
            this.setWorkingDirectory(this.commandArgs[0]);
          } else {
            // Otherwise open the file
            this.openFile(this.commandArgs[0]);
          }
        })
        .catch(error => {
          ErrorHandler.handleError(ErrorLevel.WARNING, 'Could not process command arguments', error);
        });
    }
  }

  /**
   * Render the mobile code editor UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.className = 'mobile-code-editor';
    
    // Create header
    this.headerElement = document.createElement('div');
    this.headerElement.className = 'editor-header';
    this.headerElement.innerHTML = `
      <button class="toggle-file-tree"><i class="fa fa-folder"></i></button>
      <div class="file-name">Untitled</div>
      <div class="file-actions">
        <button class="save-button"><i class="fa fa-save"></i></button>
        <button class="run-button"><i class="fa fa-play"></i></button>
      </div>
    `;
    this.container.appendChild(this.headerElement);
    
    // Create tabs container
    this.tabsContainer = document.createElement('div');
    this.tabsContainer.className = 'editor-tabs';
    this.container.appendChild(this.tabsContainer);
    
    // Create editor toolbar with commonly used actions
    const toolbar = document.createElement('div');
    toolbar.className = 'editor-toolbar';
    toolbar.innerHTML = `
      <button class="undo-button"><i class="fa fa-undo"></i></button>
      <button class="redo-button"><i class="fa fa-redo"></i></button>
      <button class="find-button"><i class="fa fa-search"></i></button>
      <button class="format-button"><i class="fa fa-indent"></i></button>
    `;
    this.container.appendChild(toolbar);
    
    // Create editor container
    this.editorContainer = document.createElement('div');
    this.editorContainer.className = 'editor-container';
    this.container.appendChild(this.editorContainer);
    
    // Create file tree panel
    this.fileTreeContainer = document.createElement('div');
    this.fileTreeContainer.className = 'file-tree-container';
    this.fileTreeContainer.innerHTML = `
      <div class="file-tree-header">
        <h3>Files</h3>
        <button class="close-file-tree"><i class="fa fa-times"></i></button>
      </div>
      <div class="file-tree"></div>
    `;
    this.container.appendChild(this.fileTreeContainer);
    
    // Create special keys toolbar for code editing
    this.specialKeysToolbar = document.createElement('div');
    this.specialKeysToolbar.className = 'keyboard-toolbar';
    this.specialKeysToolbar.innerHTML = `
      <button data-key="Tab">Tab</button>
      <button data-key="{">{ }</button>
      <button data-key="[">[  ]</button>
      <button data-key="(">(  )</button>
      <button data-key="<">&lt; &gt;</button>
      <button data-key='"'>" "</button>
      <button data-key="'">''</button>
      <button data-key=";">;</button>
      <button data-key=":">:</button>
      <button data-key="=">=</button>
      <button data-key="+">+</button>
      <button data-key="-">-</button>
      <button data-key="*">*</button>
      <button data-key="/">/</button>
      <button data-key="\\">\\</button>
      <button data-key="|">|</button>
      <button data-key="&">&</button>
      <button data-key="!">!</button>
    `;
    this.container.appendChild(this.specialKeysToolbar);
    
    // Attach event listeners
    this.attachEventListeners();
  }

  /**
   * Initialize Monaco Editor with mobile-friendly settings
   */
  private initMonaco(): void {
    if (!this.editorContainer) return;
    
    // Monaco editor options optimized for mobile touch interface
    const editorOptions: monaco.editor.IStandaloneEditorConstructionOptions = {
      automaticLayout: true,
      theme: 'vs-dark',
      minimap: { enabled: false },
      lineNumbers: 'on',
      scrollBeyondLastLine: false,
      fontSize: 16, // Larger font for touch
      wordWrap: 'on',
      scrollbar: {
        verticalScrollbarSize: 12, // Larger for touch
        horizontalScrollbarSize: 12
      },
      renderLineHighlight: 'all',
      glyphMargin: false, // Save space on mobile
      folding: true,
      tabSize: 2,
      contextmenu: false, // We'll use our own touch-friendly context menu
      // Disable certain features for better performance on mobile
      renderValidationDecorations: 'editable',
      renderWhitespace: 'none',
      snippetSuggestions: 'inline'
    };
    
    // Create editor with mobile options
    this.editor = monaco.editor.create(this.editorContainer, editorOptions);
    
    // Create default model if no file is open
    if (!this.currentFilePath) {
      const model = monaco.editor.createModel('// New file', 'javascript');
      this.editor.setModel(model);
    }
    
    this.monacoLoaded = true;
    
    // Handle editor focus events to show/hide virtual keyboard
    this.editor.onDidFocusEditorWidget(() => {
      this.showVirtualKeyboard();
    });
    
    // Handle content changes
    this.editor.onDidChangeModelContent(() => {
      this.hasUnsavedChanges = true;
    });
  }

  /**
   * Initialize gesture detection for mobile interactions
   */
  private initGestureDetection(): void {
    if (!this.container) return;
    
    this.gestureDetector = new GestureDetector(this.container);
    
    // Use pinch gesture for zooming text
    this.gestureDetector.on(GestureType.PINCH, (event) => {
      if (!this.editor) return;
      
      const currentFontSize = this.editor.getOption(monaco.editor.EditorOption.fontSize);
      let newFontSize = currentFontSize;
      
      if (event.scale > 1.05) {
        newFontSize = Math.min(currentFontSize + 1, 24);
      } else if (event.scale < 0.95) {
        newFontSize = Math.max(currentFontSize - 1, 12);
      }
      
      if (newFontSize !== currentFontSize) {
        this.editor.updateOptions({ fontSize: newFontSize });
      }
    });
    
    // Use swipe gestures for navigating tabs
    if (this.tabsContainer) {
      const tabsGestureDetector = new GestureDetector(this.tabsContainer);
      
      tabsGestureDetector.on(GestureType.SWIPE_LEFT, () => {
        this.nextTab();
      });
      
      tabsGestureDetector.on(GestureType.SWIPE_RIGHT, () => {
        this.previousTab();
      });
    }
  }

  /**
   * Attach event listeners to UI elements
   */
  private attachEventListeners(): void {
    if (!this.container || !this.headerElement || !this.fileTreeContainer || !this.specialKeysToolbar) return;
    
    // Toggle file tree
    const toggleFileTreeBtn = this.headerElement.querySelector('.toggle-file-tree');
    if (toggleFileTreeBtn) {
      toggleFileTreeBtn.addEventListener('click', () => {
        this.toggleFileTree();
      });
    }
    
    // Close file tree
    const closeFileTreeBtn = this.fileTreeContainer.querySelector('.close-file-tree');
    if (closeFileTreeBtn) {
      closeFileTreeBtn.addEventListener('click', () => {
        this.hideFileTree();
      });
    }
    
    // Save button
    const saveButton = this.headerElement.querySelector('.save-button');
    if (saveButton) {
      saveButton.addEventListener('click', () => {
        this.saveCurrentFile();
      });
    }
    
    // Run button
    const runButton = this.headerElement.querySelector('.run-button');
    if (runButton) {
      runButton.addEventListener('click', () => {
        this.runCurrentFile();
      });
    }
    
    // Special keys toolbar
    const keyButtons = this.specialKeysToolbar.querySelectorAll('button');
    keyButtons.forEach(button => {
      button.addEventListener('click', () => {
        if (!this.editor) return;
        
        const key = button.getAttribute('data-key');
        if (!key) return;
        
        const selection = this.editor.getSelection();
        const selectedText = selection ? this.editor.getModel()?.getValueInRange(selection) || '' : '';
        
        // Handle paired symbols insertion
        let textToInsert = key;
        let positionOffset = 1;
        
        switch (key) {
          case '{':
            textToInsert = selectedText ? `{${selectedText}}` : '{}';
            positionOffset = selectedText ? selectedText.length + 2 : 1;
            break;
          case '[':
            textToInsert = selectedText ? `[${selectedText}]` : '[]';
            positionOffset = selectedText ? selectedText.length + 2 : 1;
            break;
          case '(':
            textToInsert = selectedText ? `(${selectedText})` : '()';
            positionOffset = selectedText ? selectedText.length + 2 : 1;
            break;
          case '<':
            textToInsert = selectedText ? `<${selectedText}>` : '<>';
            positionOffset = selectedText ? selectedText.length + 2 : 1;
            break;
          case '"':
            textToInsert = selectedText ? `"${selectedText}"` : '""';
            positionOffset = selectedText ? selectedText.length + 2 : 1;
            break;
          case "'":
            textToInsert = selectedText ? `'${selectedText}'` : "''";
            positionOffset = selectedText ? selectedText.length + 2 : 1;
            break;
          case 'Tab':
            textToInsert = '\t';
            break;
          default:
            textToInsert = key;
            positionOffset = 1;
        }
        
        // Replace selected text or insert at cursor
        const editOperation = selection && selectedText
          ? { range: selection, text: textToInsert, forceMoveMarkers: true }
          : { range: selection || new monaco.Range(0, 0, 0, 0), text: textToInsert, forceMoveMarkers: true };
        
        this.editor.executeEdits('keyboard-shortcut', [editOperation]);
        
        // Set focus back to editor
        this.editor.focus();
      });
    });
    
    // Toolbar actions
    const undoButton = this.container.querySelector('.undo-button');
    if (undoButton) {
      undoButton.addEventListener('click', () => {
        if (this.editor) this.editor.trigger('keyboard', 'undo', null);
      });
    }
    
    const redoButton = this.container.querySelector('.redo-button');
    if (redoButton) {
      redoButton.addEventListener('click', () => {
        if (this.editor) this.editor.trigger('keyboard', 'redo', null);
      });
    }
    
    const findButton = this.container.querySelector('.find-button');
    if (findButton) {
      findButton.addEventListener('click', () => {
        if (this.editor) this.editor.trigger('keyboard', 'actions.find', null);
      });
    }
    
    const formatButton = this.container.querySelector('.format-button');
    if (formatButton) {
      formatButton.addEventListener('click', () => {
        if (this.editor) this.editor.trigger('keyboard', 'editor.action.formatDocument', null);
      });
    }
  }

  /**
   * Show the mobile virtual keyboard with coding enhancements
   */
  private showVirtualKeyboard(): void {
    if (!this.virtualKeyboard) {
      this.virtualKeyboard = new VirtualKeyboard();
      
      // Add coding-specific keyboard layout
      this.virtualKeyboard.addLayout('code', {
        name: 'Code',
        rows: [
          ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'],
          ['q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p'],
          ['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', ';'],
          ['Shift', 'z', 'x', 'c', 'v', 'b', 'n', 'm', '.', 'Backspace'],
          ['Symbols', 'Space', 'Tab', 'Return']
        ],
        symbolsRows: [
          ['!', '@', '#', '$', '%', '^', '&', '*', '(', ')'],
          ['{', '}', '[', ']', '<', '>', '=', '+', '-', '_'],
          ['\\', '|', '/', '?', '\'', '"', ':', ';', ',', '.'],
          ['Abc', '~', '`', '-', '_', '=', '+', ',', '.', 'Backspace'],
          ['123', 'Space', 'Tab', 'Return']
        ],
        additionalClasses: 'code-editor-keyboard'
      });
      
      this.virtualKeyboard.setLayout('code');
      this.virtualKeyboard.onKeyPress((key) => {
        if (!this.editor) return;
        
        // Handle special keys
        if (key === 'Return') {
          this.editor.trigger('keyboard', 'type', { text: '\n' });
        } else if (key === 'Space') {
          this.editor.trigger('keyboard', 'type', { text: ' ' });
        } else if (key === 'Tab') {
          this.editor.trigger('keyboard', 'tab', {});
        } else if (key === 'Backspace') {
          this.editor.trigger('keyboard', 'deleteLeft', {});
        } else if (key !== 'Shift' && key !== 'Symbols' && key !== 'Abc' && key !== '123') {
          // Regular key
          this.editor.trigger('keyboard', 'type', { text: key });
        }
      });
    }
    
    this.virtualKeyboard.show();
  }

  /**
   * Hide the virtual keyboard
   */
  private hideVirtualKeyboard(): void {
    if (this.virtualKeyboard) {
      this.virtualKeyboard.hide();
    }
  }

  /**
   * Toggle file tree visibility
   */
  private toggleFileTree(): void {
    if (!this.fileTreeContainer) return;
    
    if (this.fileTreeVisible) {
      this.hideFileTree();
    } else {
      this.showFileTree();
    }
  }

  /**
   * Show file tree panel
   */
  private showFileTree(): void {
    if (!this.fileTreeContainer) return;
    
    this.fileTreeContainer.classList.add('visible');
    this.fileTreeVisible = true;
    
    // Refresh file tree contents
    this.loadFileTree();
  }

  /**
   * Hide file tree panel
   */
  private hideFileTree(): void {
    if (!this.fileTreeContainer) return;
    
    this.fileTreeContainer.classList.remove('visible');
    this.fileTreeVisible = false;
  }

  /**
   * Load and render file tree for the current working directory
   */
  private loadFileTree(): void {
    if (!this.fileTreeContainer) return;
    
    const fileTreeElement = this.fileTreeContainer.querySelector('.file-tree');
    if (!fileTreeElement) return;
    
    this.os.getFileSystem().readdir(this.currentWorkingDirectory)
      .then(entries => {
        let html = '';
        
        // Sort entries: directories first, then files
        const sortedEntries = [...entries].sort((a, b) => {
          const aIsDir = FileEntryUtils.isDirectory(a);
          const bIsDir = FileEntryUtils.isDirectory(b);
          
          if (aIsDir && !bIsDir) return -1;
          if (!aIsDir && bIsDir) return 1;
          return a.name.localeCompare(b.name);
        });
        
        // Build tree HTML
        for (const entry of sortedEntries) {
          const isDir = FileEntryUtils.isDirectory(entry);
          const icon = isDir ? 'fa-folder' : 'fa-file-code';
          const fullPath = `${this.currentWorkingDirectory}/${entry.name}`.replace(/\/+/g, '/');
          
          html += `
            <div class="file-entry" data-path="${fullPath}" data-is-dir="${isDir}">
              <i class="fa ${icon}"></i>
              <span>${entry.name}</span>
            </div>
          `;
        }
        
        fileTreeElement.innerHTML = html;
        
        // Add click handlers to file entries
        const fileEntries = fileTreeElement.querySelectorAll('.file-entry');
        fileEntries.forEach(entry => {
          entry.addEventListener('click', (e) => {
            const target = e.currentTarget as HTMLElement;
            const path = target.getAttribute('data-path');
            const isDir = target.getAttribute('data-is-dir') === 'true';
            
            if (!path) return;
            
            if (isDir) {
              this.setWorkingDirectory(path);
              this.loadFileTree();
            } else {
              this.openFile(path);
              this.hideFileTree();
            }
          });
        });
      })
      .catch(error => {
        ErrorHandler.handleError(ErrorLevel.ERROR, `Failed to load directory: ${this.currentWorkingDirectory}`, error);
        fileTreeElement.innerHTML = `<div class="error">Error loading files</div>`;
      });
  }

  /**
   * Set the current working directory
   */
  private setWorkingDirectory(path: string): void {
    this.currentWorkingDirectory = path;
  }

  /**
   * Open a file for editing
   */
  private openFile(filePath: string): void {
    if (!this.editor) return;
    
    // Check if file is already open
    if (this.openFiles.has(filePath)) {
      this.switchToFile(filePath);
      return;
    }
    
    // Read file contents
    this.os.getFileSystem().readFile(filePath)
      .then(content => {
        // Determine language from file extension
        const extension = filePath.split('.').pop()?.toLowerCase() || '';
        let language = 'plaintext';
        
        switch (extension) {
          case 'js':
            language = 'javascript';
            break;
          case 'ts':
            language = 'typescript';
            break;
          case 'html':
            language = 'html';
            break;
          case 'css':
            language = 'css';
            break;
          case 'json':
            language = 'json';
            break;
          case 'md':
            language = 'markdown';
            break;
          // Add more language mappings as needed
        }
        
        // Create model for the file
        const model = monaco.editor.createModel(content, language);
        
        // Add to open files
        this.openFiles.set(filePath, {
          content,
          model
        });
        
        // Add tab
        this.addFileTab(filePath);
        
        // Set as current file
        this.switchToFile(filePath);
      })
      .catch(error => {
        ErrorHandler.handleError(ErrorLevel.ERROR, `Failed to open file: ${filePath}`, error);
      });
  }

  /**
   * Add a tab for an open file
   */
  private addFileTab(filePath: string): void {
    if (!this.tabsContainer) return;
    
    const fileName = filePath.split('/').pop() || 'Untitled';
    
    const tab = document.createElement('div');
    tab.className = 'tab';
    tab.setAttribute('data-path', filePath);
    tab.textContent = fileName;
    tab.addEventListener('click', () => {
      this.switchToFile(filePath);
    });
    
    this.tabsContainer.appendChild(tab);
  }

  /**
   * Switch to a different open file
   */
  private switchToFile(filePath: string): void {
    if (!this.editor || !this.headerElement || !this.tabsContainer) return;
    
    const fileData = this.openFiles.get(filePath);
    if (!fileData) return;
    
    // Set current file path
    this.currentFilePath = filePath;
    
    // Update model
    this.editor.setModel(fileData.model);
    
    // Update header
    const fileNameElement = this.headerElement.querySelector('.file-name');
    if (fileNameElement) {
      const fileName = filePath.split('/').pop() || 'Untitled';
      fileNameElement.textContent = fileName;
    }
    
    // Update active tab
    const tabs = this.tabsContainer.querySelectorAll('.tab');
    tabs.forEach(tab => {
      const tabPath = tab.getAttribute('data-path');
      if (tabPath === filePath) {
        tab.classList.add('active');
      } else {
        tab.classList.remove('active');
      }
    });
    
    // Reset unsaved changes flag
    this.hasUnsavedChanges = false;
  }

  /**
   * Navigate to the next tab
   */
  private nextTab(): void {
    if (!this.tabsContainer || !this.currentFilePath) return;
    
    const tabs = Array.from(this.tabsContainer.querySelectorAll('.tab'));
    const currentIndex = tabs.findIndex(tab => tab.getAttribute('data-path') === this.currentFilePath);
    
    if (currentIndex >= 0 && currentIndex < tabs.length - 1) {
      const nextTab = tabs[currentIndex + 1];
      const nextPath = nextTab.getAttribute('data-path');
      if (nextPath) {
        this.switchToFile(nextPath);
      }
    }
  }

  /**
   * Navigate to the previous tab
   */
  private previousTab(): void {
    if (!this.tabsContainer || !this.currentFilePath) return;
    
    const tabs = Array.from(this.tabsContainer.querySelectorAll('.tab'));
    const currentIndex = tabs.findIndex(tab => tab.getAttribute('data-path') === this.currentFilePath);
    
    if (currentIndex > 0) {
      const prevTab = tabs[currentIndex - 1];
      const prevPath = prevTab.getAttribute('data-path');
      if (prevPath) {
        this.switchToFile(prevPath);
      }
    }
  }

  /**
   * Save the current file
   */
  private saveCurrentFile(): void {
    if (!this.editor || !this.currentFilePath) {
      // If no file is open, prompt for save location
      // This would require implementing a save dialog
      return;
    }
    
    const content = this.editor.getValue();
    
    this.os.getFileSystem().writeFile(this.currentFilePath, content)
      .then(() => {
        this.hasUnsavedChanges = false;
        
        // Update stored content
        const fileData = this.openFiles.get(this.currentFilePath!);
        if (fileData) {
          fileData.content = content;
        }
      })
      .catch(error => {
        ErrorHandler.handleError(ErrorLevel.ERROR, `Failed to save file: ${this.currentFilePath}`, error);
      });
  }

  /**
   * Run the current file
   */
  private runCurrentFile(): void {
    if (!this.currentFilePath) return;
    
    // Extract file extension to determine how to run it
    const extension = this.currentFilePath.split('.').pop()?.toLowerCase();
    
    if (extension === 'js' || extension === 'ts') {
      // For JavaScript/TypeScript files, we might execute them in a simulated environment
      this.saveCurrentFile();
      this.os.getTerminalManager().runCommand(`execute ${this.currentFilePath}`);
    }
    // Add handlers for other file types as needed
  }

  /**
   * Resize handler
   */
  public resize(): void {
    super.resize();
    
    // Ensure Monaco editor layout is updated
    if (this.editor) {
      this.editor.layout();
    }
  }

  /**
   * Clean up resources when application is closed
   */
  public async close(): Promise<void> {
    // Check for unsaved changes
    if (this.hasUnsavedChanges) {
      // In a real implementation, we would prompt the user to save
      // For now, just auto-save
      this.saveCurrentFile();
    }
    
    // Dispose Monaco models
    this.openFiles.forEach(fileData => {
      fileData.model.dispose();
    });
    this.openFiles.clear();
    
    // Dispose editor
    if (this.editor) {
      this.editor.dispose();
      this.editor = null;
    }
    
    // Hide virtual keyboard
    this.hideVirtualKeyboard();
    
    // Cleanup gesture detector
    if (this.gestureDetector) {
      this.gestureDetector.destroy();
      this.gestureDetector = null;
    }
    
    return super.close();
  }
}
