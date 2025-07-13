using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using HackerOs.OS.Core;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.UI.Windows;
using HackerOs.OS.IO.FileSystem;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.UI.Windows.TextEditor
{
    public partial class TextEditorApp : WindowBase
    {
        [Inject]
        protected IApplicationBridge ApplicationBridge { get; set; } = null!;
        
        [Inject]
        protected IJSRuntime JSRuntime { get; set; } = null!;
        
        [Inject]
        protected IVirtualFileSystem FileSystem { get; set; } = null!;
        
        // Application state variables
        private bool _isInitialized = false;
        private string? _currentFilePath;
        private StringBuilder _content = new StringBuilder();
        private bool _hasUnsavedChanges = false;
        private Stack<string> _undoStack = new Stack<string>();
        private Stack<string> _redoStack = new Stack<string>();
        private object _lockObject = new object();
        private Timer? _autoSaveTimer;
        
        // UI state variables
        private bool ShowFindDialog { get; set; } = false;
        private bool ShowReplaceDialog { get; set; } = false;
        private bool ShowSettingsDialog { get; set; } = false;
        private bool ShowStatsDialog { get; set; } = false;
        private string SearchText { get; set; } = string.Empty;
        private string ReplaceText { get; set; } = string.Empty;
        private bool CaseSensitive { get; set; } = false;
        private int CurrentLine { get; set; } = 1;
        private int CurrentColumn { get; set; } = 1;
        private string DocumentStats { get; set; } = string.Empty;
        private int LineCount => DocumentContent.Split('\n').Length;
        
        // Editor settings
        private bool ShowLineNumbers { get; set; } = true;
        private bool WordWrap { get; set; } = true;
        private int TabSize { get; set; } = 4;
        private bool ConvertTabsToSpaces { get; set; } = true;
        private bool AutoSave { get; set; } = false;
        private int AutoSaveInterval { get; set; } = 30;
        
        // Document content bound to textarea
        private string DocumentContent
        {
            get
            {
                lock (_lockObject)
                {
                    return _content.ToString();
                }
            }
            set
            {
                lock (_lockObject)
                {
                    var oldContent = _content.ToString();
                    
                    if (oldContent != value)
                    {
                        // Add current content to undo stack
                        _undoStack.Push(oldContent);
                        _redoStack.Clear();
                        
                        // Update content
                        _content.Clear();
                        _content.Append(value);
                        _hasUnsavedChanges = true;
                        
                        // Limit undo stack size
                        if (_undoStack.Count > 100)
                        {
                            var temp = _undoStack.ToArray()[..100];
                            _undoStack.Clear();
                            for (int i = temp.Length - 1; i >= 0; i--)
                            {
                                _undoStack.Push(temp[i]);
                            }
                        }
                    }
                }
            }
        }
        
        // Property accessors
        public string? CurrentFilePath => _currentFilePath;
        public bool HasUnsavedChanges => _hasUnsavedChanges;
        
        /// <summary>
        /// Initializes the application when the component is first rendered.
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            // Set window properties
            WindowTitle = "Text Editor";
            IconPath = "/icons/text-editor.png";
            MinWidth = 400;
            MinHeight = 300;
            DefaultWidth = 600;
            DefaultHeight = 400;
            
            await base.OnInitializedAsync();
        }
        
        /// <summary>
        /// Initializes the application after the component has rendered.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await InitializeApplicationAsync();
            }
            
            await base.OnAfterRenderAsync(firstRender);
        }
        
        /// <summary>
        /// Initializes the application, registers with the ApplicationBridge,
        /// and performs initial setup.
        /// </summary>
        private async Task InitializeApplicationAsync()
        {
            try
            {
                await ApplicationBridge.InitializeAsync(this);
                
                // Check if a file path was provided in launch arguments
                if (LaunchParameters?.Arguments?.Count > 0)
                {
                    var filePath = LaunchParameters.Arguments[0];
                    await OpenFileAsync(filePath);
                }
                else
                {
                    // Create new document
                    await NewDocumentAsync();
                }
                
                // Start auto-save timer if enabled
                if (AutoSave)
                {
                    StartAutoSaveTimer();
                }
                
                _isInitialized = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Error initializing Text Editor: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Called when the window is closing. Performs cleanup operations.
        /// </summary>
        protected override async Task OnWindowClosingAsync()
        {
            try
            {
                // Stop auto-save timer
                _autoSaveTimer?.Dispose();
                
                // Check for unsaved changes
                if (_hasUnsavedChanges)
                {
                    // In a real implementation, we would show a dialog to save changes
                    // For now, we'll just log a warning
                    await RaiseErrorAsync("Warning: Unsaved changes will be lost.");
                }
                
                await base.OnWindowClosingAsync();
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Error closing Text Editor: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Implements the SetStateAsync method from IApplication.
        /// </summary>
        public override async Task SetStateAsync(ApplicationState state)
        {
            try
            {
                await base.SetStateAsync(state);
                
                // Application-specific state handling
                if (state == ApplicationState.Active)
                {
                    // Focus the editor when window becomes active
                    await JSRuntime.InvokeVoidAsync("textEditorApp.focusEditor");
                }
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Error setting state for Text Editor: {ex.Message}");
            }
        }
        
        // File Operations
        
        /// <summary>
        /// Create a new document
        /// </summary>
        private async Task NewDocumentAsync()
        {
            // Check for unsaved changes first (to be implemented with dialogs)
            
            lock (_lockObject)
            {
                _currentFilePath = null;
                _content.Clear();
                _hasUnsavedChanges = false;
                _undoStack.Clear();
                _redoStack.Clear();
            }
            
            StateHasChanged();
            await RaiseInfoAsync("New document created.");
        }
        
        /// <summary>
        /// Open a file for editing
        /// </summary>
        private async Task OpenFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // In a real implementation, this would show a file picker
                    return;
                }
                
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await RaiseErrorAsync("No user context available for file operation.");
                    return;
                }
                
                if (!await FileSystem.FileExistsAsync(filePath, user))
                {
                    await RaiseErrorAsync($"File not found: {filePath}");
                    return;
                }
                
                var content = await FileSystem.ReadFileAsync(filePath, user);
                
                lock (_lockObject)
                {
                    _currentFilePath = filePath;
                    _content.Clear();
                    _content.Append(content);
                    _hasUnsavedChanges = false;
                    _undoStack.Clear();
                    _redoStack.Clear();
                }
                
                StateHasChanged();
                await RaiseInfoAsync($"File opened: {System.IO.Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Failed to open file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save current document
        /// </summary>
        private async Task SaveAsync()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                // Show save as dialog in a real implementation
                await RaiseErrorAsync("No file path specified. Use Save As instead.");
                return;
            }
            
            await SaveAsAsync(_currentFilePath);
        }
        
        /// <summary>
        /// Save document with a specific file path
        /// </summary>
        private async Task SaveAsAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // In a real implementation, this would show a file picker
                    return;
                }
                
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await RaiseErrorAsync("No user context available for file operation.");
                    return;
                }
                
                string content;
                lock (_lockObject)
                {
                    content = _content.ToString();
                }
                
                await FileSystem.WriteFileAsync(filePath, content, user);
                
                lock (_lockObject)
                {
                    _currentFilePath = filePath;
                    _hasUnsavedChanges = false;
                }
                
                StateHasChanged();
                await RaiseInfoAsync($"File saved: {System.IO.Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Failed to save file: {ex.Message}");
            }
        }
        
        // Edit Operations
        
        /// <summary>
        /// Undo the last edit operation
        /// </summary>
        private async Task UndoAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_undoStack.Count == 0)
                    {
                        return;
                    }
                    
                    var currentContent = _content.ToString();
                    var previousContent = _undoStack.Pop();
                    
                    _redoStack.Push(currentContent);
                    
                    _content.Clear();
                    _content.Append(previousContent);
                    _hasUnsavedChanges = true;
                }
                
                StateHasChanged();
                await RaiseInfoAsync("Undo operation completed.");
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Undo failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Redo the last undone operation
        /// </summary>
        private async Task RedoAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_redoStack.Count == 0)
                    {
                        return;
                    }
                    
                    var currentContent = _content.ToString();
                    var nextContent = _redoStack.Pop();
                    
                    _undoStack.Push(currentContent);
                    
                    _content.Clear();
                    _content.Append(nextContent);
                    _hasUnsavedChanges = true;
                }
                
                StateHasChanged();
                await RaiseInfoAsync("Redo operation completed.");
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Redo failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Find all occurrences of a string
        /// </summary>
        private async Task<List<int>> FindAllAsync(string searchText, bool caseSensitive = false)
        {
            try
            {
                var positions = new List<int>();
                string content;
                
                lock (_lockObject)
                {
                    content = _content.ToString();
                }
                
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int index = 0;
                
                while ((index = content.IndexOf(searchText, index, comparison)) != -1)
                {
                    positions.Add(index);
                    index += searchText.Length;
                }
                
                await RaiseInfoAsync($"Found {positions.Count} occurrences of '{searchText}'");
                return positions;
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Failed to search text: {ex.Message}");
                return new List<int>();
            }
        }
        
        /// <summary>
        /// Replace all occurrences of a string
        /// </summary>
        private async Task<int> ReplaceAllAsync(string searchText, string replaceText, bool caseSensitive = false)
        {
            try
            {
                string oldContent;
                int replacements = 0;
                
                lock (_lockObject)
                {
                    oldContent = _content.ToString();
                    var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    
                    string newContent = oldContent;
                    int index = 0;
                    
                    while ((index = newContent.IndexOf(searchText, index, comparison)) != -1)
                    {
                        newContent = newContent.Substring(0, index) + replaceText + newContent.Substring(index + searchText.Length);
                        index += replaceText.Length;
                        replacements++;
                    }
                    
                    if (replacements > 0)
                    {
                        _content.Clear();
                        _content.Append(newContent);
                        _hasUnsavedChanges = true;
                        
                        // Add to undo stack
                        _undoStack.Push(oldContent);
                        _redoStack.Clear();
                    }
                }
                
                StateHasChanged();
                await RaiseInfoAsync($"Replaced {replacements} occurrences of '{searchText}' with '{replaceText}'");
                return replacements;
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Failed to replace text: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Get document statistics
        /// </summary>
        private async Task<string> GetDocumentStatsAsync()
        {
            try
            {
                string content;
                lock (_lockObject)
                {
                    content = _content.ToString();
                }
                
                var lines = content.Split('\n').Length;
                var words = content.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                var characters = content.Length;
                var charactersNoSpaces = content.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "").Length;
                
                return $"Document Statistics:\n" +
                       $"Lines: {lines:N0}\n" +
                       $"Words: {words:N0}\n" +
                       $"Characters: {characters:N0}\n" +
                       $"Characters (no spaces): {charactersNoSpaces:N0}\n" +
                       $"File: {System.IO.Path.GetFileName(_currentFilePath) ?? "Untitled"}\n" +
                       $"Unsaved Changes: {(_hasUnsavedChanges ? "Yes" : "No")}";
            }
            catch (Exception ex)
            {
                return $"Error getting document statistics: {ex.Message}";
            }
        }
        
        private void StartAutoSaveTimer()
        {
            _autoSaveTimer?.Dispose();
            
            _autoSaveTimer = new Timer(AutoSaveInterval * 1000);
            _autoSaveTimer.Elapsed += async (sender, e) => await AutoSaveCallbackAsync();
            _autoSaveTimer.AutoReset = true;
            _autoSaveTimer.Start();
        }
        
        private async Task AutoSaveCallbackAsync()
        {
            try
            {
                if (_hasUnsavedChanges && !string.IsNullOrEmpty(_currentFilePath))
                {
                    await SaveAsync();
                    await RaiseInfoAsync("Auto-saved document.");
                }
            }
            catch (Exception ex)
            {
                await RaiseErrorAsync($"Auto-save failed: {ex.Message}");
            }
        }
        
        // Event handlers for UI
        
        private void NewDocument() => InvokeAsync(NewDocumentAsync);
        
        private void OpenFile() => InvokeAsync(async () => await OpenFileAsync(_currentFilePath ?? ""));
        
        private void SaveFile() => InvokeAsync(SaveAsync);
        
        private void SaveFileAs() => InvokeAsync(async () => await SaveAsAsync(_currentFilePath ?? ""));
        
        private void Undo() => InvokeAsync(UndoAsync);
        
        private void Redo() => InvokeAsync(RedoAsync);
        
        private void FindText() => ShowFindDialog = true;
        
        private void ReplaceText() => ShowReplaceDialog = true;
        
        private void ShowSettings() => ShowSettingsDialog = true;
        
        private async Task ShowStats()
        {
            DocumentStats = await GetDocumentStatsAsync();
            ShowStatsDialog = true;
            StateHasChanged();
        }
        
        private async Task FindNext()
        {
            if (string.IsNullOrEmpty(SearchText))
                return;
                
            var positions = await FindAllAsync(SearchText, CaseSensitive);
            if (positions.Count > 0)
            {
                // In a real implementation, we would highlight the text and scroll to it
                // For now, we'll just log the position
                await RaiseInfoAsync($"Found at position {positions[0]}");
            }
            else
            {
                await RaiseInfoAsync($"No occurrences of '{SearchText}' found");
            }
        }
        
        private async Task FindAll()
        {
            if (string.IsNullOrEmpty(SearchText))
                return;
                
            await FindAllAsync(SearchText, CaseSensitive);
        }
        
        private async Task Replace()
        {
            if (string.IsNullOrEmpty(SearchText))
                return;
                
            // In a real implementation, we would replace the current selection
            // For now, we'll just log a message
            await RaiseInfoAsync("Replace operation not fully implemented in this version");
        }
        
        private async Task ReplaceAll()
        {
            if (string.IsNullOrEmpty(SearchText))
                return;
                
            await ReplaceAllAsync(SearchText, ReplaceText, CaseSensitive);
        }
        
        private void SaveSettings()
        {
            // Update timer if auto-save setting changed
            if (AutoSave)
            {
                StartAutoSaveTimer();
            }
            else
            {
                _autoSaveTimer?.Dispose();
                _autoSaveTimer = null;
            }
            
            ShowSettingsDialog = false;
            StateHasChanged();
        }
        
        private void HandleKeyDown(KeyboardEventArgs e)
        {
            // Handle keyboard shortcuts
            if (e.CtrlKey)
            {
                switch (e.Key?.ToLower())
                {
                    case "s":
                        if (e.ShiftKey)
                            SaveFileAs();
                        else
                            SaveFile();
                        break;
                    case "o":
                        OpenFile();
                        break;
                    case "n":
                        NewDocument();
                        break;
                    case "z":
                        Undo();
                        break;
                    case "y":
                        Redo();
                        break;
                    case "f":
                        FindText();
                        break;
                    case "h":
                        ReplaceText();
                        break;
                }
            }
        }
        
        // Helper methods
        
        private async Task RaiseInfoAsync(string message)
        {
            try
            {
                await ApplicationBridge.RaiseInfoAsync(this, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error raising info: {ex.Message}");
            }
        }
        
        private async Task RaiseErrorAsync(string message)
        {
            try
            {
                await ApplicationBridge.RaiseErrorAsync(this, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error raising error: {ex.Message}");
            }
        }
    }
}
