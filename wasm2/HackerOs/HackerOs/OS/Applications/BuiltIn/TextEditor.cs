using HackerOs.OS.Applications;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using System.Text;

namespace HackerOs.OS.Applications.BuiltIn
{
    /// <summary>
    /// Text editor application with basic editing features
    /// </summary>
    [App("Text Editor", "text-editor", 
        Description = "Simple text editor for creating and editing text files",
        IconPath = "/icons/text-editor.png",
        Categories = new[] { "Office", "Development" })]
    [OpenFileType("Text Files", "txt", "text", "log")]
    [OpenFileType("Configuration Files", "conf", "config", "ini", "json", "xml", Priority = 10)]
    [OpenFileType("Source Code", "cs", "ts", "js", "html", "css", "md", Priority = 5)]
    public class TextEditor : ApplicationBase
    {
        private readonly IVirtualFileSystem _fileSystem;
        private string? _currentFilePath;
        private StringBuilder _content;
        private bool _hasUnsavedChanges;
        private Stack<string> _undoStack;
        private Stack<string> _redoStack;
        private readonly object _lockObject = new object();

        // Editor configuration
        public class EditorSettings
        {
            public bool ShowLineNumbers { get; set; } = true;
            public bool WordWrap { get; set; } = true;
            public int TabSize { get; set; } = 4;
            public bool ConvertTabsToSpaces { get; set; } = true;
            public bool AutoSave { get; set; } = false;
            public int AutoSaveInterval { get; set; } = 30; // seconds
        }

        private EditorSettings _settings;
        private Timer? _autoSaveTimer;

        public TextEditor(IVirtualFileSystem fileSystem) : base()
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _content = new StringBuilder();
            _hasUnsavedChanges = false;
            _undoStack = new Stack<string>();
            _redoStack = new Stack<string>();
            _settings = new EditorSettings();
        }

        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            try
            {
                // Check if a file path was provided in arguments
                if (context.Arguments?.Count > 0)
                {
                    var filePath = context.Arguments[0];
                    await OpenFileAsync(filePath);
                }
                else
                {
                    // Create new document
                    await NewDocumentAsync();
                }

                // Start auto-save timer if enabled
                if (_settings.AutoSave)
                {
                    StartAutoSaveTimer();
                }

                await OnOutputAsync("Text Editor started.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to start text editor: {ex.Message}");
                return false;
            }
        }        protected override async Task<bool> OnStopAsync()
        {
            try
            {
                // Stop auto-save timer
                _autoSaveTimer?.Dispose();

                // Check for unsaved changes
                if (_hasUnsavedChanges)
                {
                    await OnOutputAsync("Warning: Unsaved changes will be lost.");
                    // In a real UI, this would show a save dialog
                }

                await OnOutputAsync("Text Editor closed.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Error stopping text editor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create a new document
        /// </summary>
        public async Task NewDocumentAsync()
        {
            lock (_lockObject)
            {
                _currentFilePath = null;
                _content.Clear();
                _hasUnsavedChanges = false;
                _undoStack.Clear();
                _redoStack.Clear();
            }

            await OnOutputAsync("New document created.");
        }

        /// <summary>
        /// Open a file for editing
        /// </summary>
        public async Task<bool> OpenFileAsync(string filePath)
        {            try
            {
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return false;
                }

                if (!await _fileSystem.FileExistsAsync(filePath, user))
                {
                    await OnErrorAsync($"File not found: {filePath}");
                    return false;
                }

                var content = await _fileSystem.ReadFileAsync(filePath, user);
                
                lock (_lockObject)
                {
                    _currentFilePath = filePath;
                    _content.Clear();
                    _content.Append(content);
                    _hasUnsavedChanges = false;
                    _undoStack.Clear();
                    _redoStack.Clear();
                }

                await OnOutputAsync($"File opened: {HSystem.IO.HPath.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to open file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save current document
        /// </summary>
        public async Task<bool> SaveAsync()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                await OnErrorAsync("No file path specified. Use SaveAsAsync instead.");
                return false;
            }

            return await SaveAsAsync(_currentFilePath);
        }

        /// <summary>
        /// Save document with a specific file path
        /// </summary>
        public async Task<bool> SaveAsAsync(string filePath)
        {
            try
            {
                var user = Context?.UserSession?.GetUser();
                if (user == null)
                {
                    await OnErrorAsync("No user context available for file operation.");
                    return false;
                }

                string content;
                lock (_lockObject)
                {
                    content = _content.ToString();
                }

                await _fileSystem.WriteFileAsync(filePath, content, user);
                
                lock (_lockObject)
                {
                    _currentFilePath = filePath;
                    _hasUnsavedChanges = false;
                }

                await OnOutputAsync($"File saved: {HSystem.IO.HPath.GetFileName(filePath)}");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to save file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Insert text at current cursor position
        /// </summary>
        public async Task InsertTextAsync(string text, int position = -1)
        {
            try
            {
                string oldContent;
                lock (_lockObject)
                {
                    oldContent = _content.ToString();
                    
                    if (position < 0 || position > _content.Length)
                    {
                        position = _content.Length;
                    }

                    _content.Insert(position, text);
                    _hasUnsavedChanges = true;

                    // Add to undo stack
                    _undoStack.Push(oldContent);
                    _redoStack.Clear(); // Clear redo stack when new action is performed
                    
                    // Limit undo stack size
                    if (_undoStack.Count > 100)
                    {
                        var temp = _undoStack.ToArray().Take(100).ToArray();
                        _undoStack.Clear();
                        for (int i = temp.Length - 1; i >= 0; i--)
                        {
                            _undoStack.Push(temp[i]);
                        }
                    }
                }

                await OnOutputAsync($"Text inserted at position {position}");
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to insert text: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete text in the specified range
        /// </summary>
        public async Task DeleteTextAsync(int startPosition, int length)
        {
            try
            {                string oldContent;
                bool invalidPosition = false;
                lock (_lockObject)
                {
                    oldContent = _content.ToString();
                      
                    if (startPosition < 0 || startPosition >= _content.Length)
                    {
                        invalidPosition = true;
                    }
                    else
                    {
                        if (startPosition + length > _content.Length)
                        {
                            length = _content.Length - startPosition;
                        }

                        _content.Remove(startPosition, length);
                        _hasUnsavedChanges = true;

                        // Add to undo stack
                        _undoStack.Push(oldContent);
                        _redoStack.Clear();
                    }
                }

                // If there was an error, report it outside the lock
                if (invalidPosition)
                {
                    await OnErrorAsync("Invalid start position for delete operation.");
                    return;
                }

                await OnOutputAsync($"Deleted {length} characters from position {startPosition}");
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to delete text: {ex.Message}");
            }
        }

        /// <summary>
        /// Replace all occurrences of a string
        /// </summary>
        public async Task<int> ReplaceAllAsync(string searchText, string replaceText, bool caseSensitive = false)
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

                await OnOutputAsync($"Replaced {replacements} occurrences of '{searchText}' with '{replaceText}'");
                return replacements;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to replace text: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Find all occurrences of a string
        /// </summary>
        public async Task<List<int>> FindAllAsync(string searchText, bool caseSensitive = false)
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

                await OnOutputAsync($"Found {positions.Count} occurrences of '{searchText}'");
                return positions;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to search text: {ex.Message}");
                return new List<int>();
            }
        }

        /// <summary>
        /// Undo last operation
        /// </summary>
        public async Task<bool> UndoAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_undoStack.Count == 0)
                    {
                        return false;
                    }

                    var currentContent = _content.ToString();
                    var previousContent = _undoStack.Pop();
                    
                    _redoStack.Push(currentContent);
                    
                    _content.Clear();
                    _content.Append(previousContent);
                    _hasUnsavedChanges = true;
                }

                await OnOutputAsync("Undo operation completed.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Undo failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        public async Task<bool> RedoAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_redoStack.Count == 0)
                    {
                        return false;
                    }

                    var currentContent = _content.ToString();
                    var nextContent = _redoStack.Pop();
                    
                    _undoStack.Push(currentContent);
                    
                    _content.Clear();
                    _content.Append(nextContent);
                    _hasUnsavedChanges = true;
                }

                await OnOutputAsync("Redo operation completed.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Redo failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get current document content
        /// </summary>
        public string GetContent()
        {
            lock (_lockObject)
            {
                return _content.ToString();
            }
        }

        /// <summary>
        /// Get current file path
        /// </summary>
        public string? GetCurrentFilePath()
        {
            return _currentFilePath;
        }

        /// <summary>
        /// Check if document has unsaved changes
        /// </summary>
        public bool HasUnsavedChanges()
        {
            return _hasUnsavedChanges;
        }

        /// <summary>
        /// Get editor settings
        /// </summary>
        public EditorSettings GetSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Update editor settings
        /// </summary>
        public async Task UpdateSettingsAsync(EditorSettings newSettings)
        {
            _settings = newSettings;
            
            // Update auto-save timer
            if (_settings.AutoSave && _autoSaveTimer == null)
            {
                StartAutoSaveTimer();
            }
            else if (!_settings.AutoSave && _autoSaveTimer != null)
            {
                _autoSaveTimer.Dispose();
                _autoSaveTimer = null;
            }

            await OnOutputAsync("Editor settings updated.");
        }

        /// <summary>
        /// Get document statistics
        /// </summary>
        public async Task<string> GetDocumentStatsAsync()
        {
            try
            {
                // Await a completed task to make this method truly async
                await Task.CompletedTask;
                
                string content;
                lock (_lockObject)
                {
                    content = _content.ToString();
                }

                var lines = content.Split('\n').Length;
                var words = content.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                var characters = content.Length;
                var charactersNoSpaces = content.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "").Length;

                var stats = $"Document Statistics:\n" +
                           $"Lines: {lines:N0}\n" +
                           $"Words: {words:N0}\n" +
                           $"Characters: {characters:N0}\n" +
                           $"Characters (no spaces): {charactersNoSpaces:N0}\n" +
                           $"File: {HSystem.IO.HPath.GetFileName(_currentFilePath) ?? "Untitled"}\n" +
                           $"Unsaved Changes: {(_hasUnsavedChanges ? "Yes" : "No")}";

                return stats;
            }
            catch (Exception ex)
            {
                return $"Error getting document statistics: {ex.Message}";
            }
        }

        private void StartAutoSaveTimer()
        {
            if (_autoSaveTimer != null) return;

            _autoSaveTimer = new Timer(AutoSaveCallback, null, 
                TimeSpan.FromSeconds(_settings.AutoSaveInterval), 
                TimeSpan.FromSeconds(_settings.AutoSaveInterval));
        }

        private async void AutoSaveCallback(object? state)
        {
            try
            {
                if (_hasUnsavedChanges && !string.IsNullOrEmpty(_currentFilePath))
                {
                    await SaveAsync();
                    await OnOutputAsync("Auto-saved document.");
                }
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Auto-save failed: {ex.Message}");
            }
        }
    }
}
