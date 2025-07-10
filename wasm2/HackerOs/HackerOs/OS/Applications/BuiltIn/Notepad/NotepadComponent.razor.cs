using HackerOs.OS.IO;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text;

namespace HackerOs.OS.Applications.BuiltIn.Notepad;

public partial class NotepadComponent : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    
    [Parameter] public string Content { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ContentChanged { get; set; }
    
    [Parameter] public string? FilePath { get; set; }
    [Parameter] public EventCallback<string?> FilePathChanged { get; set; }
    
    [Parameter] public bool IsModified { get; set; }
    [Parameter] public EventCallback<bool> IsModifiedChanged { get; set; }
    
    [Parameter] public HackerOs.OS.IO.IVirtualFileSystem? FileSystem { get; set; }
    [Parameter] public User.User? CurrentUser { get; set; }
    
    private ElementReference textAreaRef;
    private IJSObjectReference? _jsModule;
    
    // UI state
    private int _lines = 0;
    private int _characters = 0;
    private bool _showOpenDialog = false;
    private bool _showSaveDialog = false;
    private string? _currentDirectory;
    private List<FileSystemEntry> _directoryEntries = new();
    private FileSystemEntry? _selectedFile;
    private string _saveFileName = string.Empty;
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        // Initialize file explorer to user's home directory
        if (FileSystem != null && CurrentUser != null)
        {
            _currentDirectory = CurrentUser.HomeDirectory;
            await LoadDirectoryEntriesAsync(_currentDirectory);
        }
        
        // Update text statistics
        UpdateTextStatistics();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/HackerOs/OS/Applications/BuiltIn/Notepad/NotepadComponent.razor.js");
                
            if (_jsModule != null)
            {
                await _jsModule.InvokeVoidAsync("initializeNotepad", textAreaRef);
            }
        }
    }
    
    private async Task NewFile()
    {
        if (IsModified)
        {
            // Ask for confirmation if there's unsaved content
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "Discard current changes?");
            if (!confirmed) return;
        }
        
        Content = string.Empty;
        await ContentChanged.InvokeAsync(Content);
        
        FilePath = null;
        await FilePathChanged.InvokeAsync(FilePath);
        
        IsModified = false;
        await IsModifiedChanged.InvokeAsync(IsModified);
        
        UpdateTextStatistics();
    }
    
    private async Task OpenFile()
    {
        if (FileSystem == null || CurrentUser == null)
            return;
            
        if (IsModified)
        {
            // Ask for confirmation if there's unsaved content
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "Discard current changes?");
            if (!confirmed) return;
        }
        
        // Show open file dialog
        _showOpenDialog = true;
        
        // Initialize file explorer if needed
        if (string.IsNullOrEmpty(_currentDirectory))
        {
            _currentDirectory = CurrentUser.HomeDirectory;
            await LoadDirectoryEntriesAsync(_currentDirectory);
        }
    }
    
    private async Task SaveFile()
    {
        if (FileSystem == null || CurrentUser == null)
            return;
            
        if (string.IsNullOrEmpty(FilePath))
        {
            // If no file path, do Save As instead
            await SaveFileAs();
            return;
        }
        
        await SaveContentToFileAsync(FilePath);
    }
    
    private async Task SaveFileAs()
    {
        if (FileSystem == null || CurrentUser == null)
            return;
            
        // Show save file dialog
        _showSaveDialog = true;
        
        // Initialize file explorer if needed
        if (string.IsNullOrEmpty(_currentDirectory))
        {
            _currentDirectory = CurrentUser.HomeDirectory;
            await LoadDirectoryEntriesAsync(_currentDirectory);
        }
        
        // Set default filename from current file path or "untitled.txt"
        _saveFileName = string.IsNullOrEmpty(FilePath) 
            ? "untitled.txt" 
            : HSystem.IO.HPath.GetFileName(FilePath);
    }
    
    private async Task LoadDirectoryEntriesAsync(string? directory)
    {
        if (string.IsNullOrEmpty(directory) || FileSystem == null || CurrentUser == null)
            return;
            
        try
        {
            _directoryEntries = await FileSystem.GetDirectoryEntriesAsync(directory, CurrentUser);
            // Sort: directories first, then files, both alphabetically
            _directoryEntries = _directoryEntries
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name)
                .ToList();
                
            // Add parent directory entry if not at root
            if (directory != "/")
            {
                var parentDir = HSystem.IO.HPath.GetDirectoryName(directory);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    _directoryEntries.Insert(0, new FileSystemEntry
                    {
                        Name = "..",
                        FullPath = parentDir,
                        IsDirectory = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading directory: {ex.Message}");
            _directoryEntries = new List<FileSystemEntry>();
        }
    }
    
    private async Task OnFileEntryClick(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            // Navigate to directory
            _currentDirectory = entry.FullPath;
            await LoadDirectoryEntriesAsync(_currentDirectory);
            _selectedFile = null;
        }
        else
        {
            // Select file
            _selectedFile = entry;
            
            // If in save dialog, update filename
            if (_showSaveDialog)
            {
                _saveFileName = entry.Name;
            }
        }
    }
    
    private void CancelOpenDialog()
    {
        _showOpenDialog = false;
        _selectedFile = null;
    }
    
    private async Task ConfirmOpenFile()
    {
        if (_selectedFile == null || FileSystem == null || CurrentUser == null)
        {
            _showOpenDialog = false;
            return;
        }
        
        try
        {
            // Read file content
            var content = await FileSystem.ReadAllTextAsync(_selectedFile.FullPath, CurrentUser);
            
            // Update content
            Content = content;
            await ContentChanged.InvokeAsync(Content);
            
            // Update file path
            FilePath = _selectedFile.FullPath;
            await FilePathChanged.InvokeAsync(FilePath);
            
            // Reset modified flag
            IsModified = false;
            await IsModifiedChanged.InvokeAsync(IsModified);
            
            // Update text statistics
            UpdateTextStatistics();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error opening file: {ex.Message}");
            await JSRuntime.InvokeVoidAsync("alert", $"Error opening file: {ex.Message}");
        }
        
        _showOpenDialog = false;
        _selectedFile = null;
    }
    
    private void CancelSaveDialog()
    {
        _showSaveDialog = false;
        _saveFileName = string.Empty;
    }
    
    private async Task ConfirmSaveFile()
    {
        if (string.IsNullOrEmpty(_saveFileName) || string.IsNullOrEmpty(_currentDirectory) || 
            FileSystem == null || CurrentUser == null)
        {
            _showSaveDialog = false;
            return;
        }
        
        try
        {
            // Construct full file path
            var filePath = HSystem.IO.HPath.Combine(_currentDirectory, _saveFileName);
            
            // Save file
            await SaveContentToFileAsync(filePath);
            
            // Update file path
            FilePath = filePath;
            await FilePathChanged.InvokeAsync(FilePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
            await JSRuntime.InvokeVoidAsync("alert", $"Error saving file: {ex.Message}");
        }
        
        _showSaveDialog = false;
        _saveFileName = string.Empty;
    }
    
    private async Task SaveContentToFileAsync(string filePath)
    {
        if (FileSystem == null || CurrentUser == null)
            return;
            
        try
        {
            // Write content to file
            await FileSystem.WriteAllTextAsync(filePath, Content, CurrentUser);
            
            // Reset modified flag
            IsModified = false;
            await IsModifiedChanged.InvokeAsync(IsModified);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
            await JSRuntime.InvokeVoidAsync("alert", $"Error saving file: {ex.Message}");
        }
    }
    
    private void UpdateTextStatistics()
    {
        _characters = Content.Length;
        _lines = Content.Split('\n').Length;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }
    }
}
