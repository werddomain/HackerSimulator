using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.IO;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace HackerOs.OS.Applications.UI.Windows.Notepad
{
    /// <summary>
    /// Notepad application for editing text files in HackerOS
    /// This is a sample implementation using the new application architecture
    /// </summary>
    [App("Notepad", "system.notepad", 
        Description = "Simple text editor for creating and editing text files",
        Type = ApplicationType.WindowedApplication,
        IconPath = "/images/icons/notepad.png")]
    public partial class NotepadApp
    {
        #region Dependencies

        [Inject] private IVirtualFileSystem FileSystem { get; set; } = null!;
        [Inject] private IUserManager UserManager { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private ILogger<NotepadApp> Logger { get; set; } = null!;

        #endregion

        #region State Variables

        /// <summary>
        /// Reference to the text area element for manipulation via JS
        /// </summary>
        private ElementReference TextAreaRef;

        /// <summary>
        /// Current content of the document
        /// </summary>
        private string DocumentContent { get; set; } = string.Empty;

        /// <summary>
        /// Path to the currently open file
        /// </summary>
        private string? CurrentFilePath { get; set; }

        /// <summary>
        /// Whether the document has unsaved changes
        /// </summary>
        private bool IsDocumentModified { get; set; }

        /// <summary>
        /// Number of lines in the document
        /// </summary>
        private int LineCount { get; set; } = 1;

        /// <summary>
        /// Number of characters in the document
        /// </summary>
        private int CharacterCount { get; set; } = 0;

        /// <summary>
        /// JavaScript module reference for interop
        /// </summary>
        private IJSObjectReference? JsModule;

        #endregion

        #region File Dialog Properties

        // Open file dialog
        private bool ShowOpenFileDialog { get; set; }
        private string CurrentDirectory { get; set; } = string.Empty;
        private List<FileSystemEntry> DirectoryEntries { get; set; } = new();
        private FileSystemEntry? SelectedFileEntry { get; set; }

        // Save file dialog
        private bool ShowSaveFileDialog { get; set; }
        private string SaveFileName { get; set; } = string.Empty;

        // Unsaved changes dialog
        private bool ShowUnsavedChangesDialog { get; set; }
        private UnsavedChangesResponse PendingResponse { get; set; } = UnsavedChangesResponse.Cancel;
        private TaskCompletionSource<UnsavedChangesResponse>? UnsavedChangesDialogTask;
        private Action? AfterUnsavedChangesAction;

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called when the component is initialized
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            // Set window title
            Title = "Notepad";
            
            // Get user's home directory as starting point
            var currentUser = await UserManager.GetCurrentUserAsync();
            if (currentUser != null)
            {
                CurrentDirectory = currentUser.HomeDirectory;
                await LoadDirectoryEntriesAsync(CurrentDirectory);
            }
            
            // Subscribe to window close event
            OnClose += async () =>
            {
                if (IsDocumentModified)
                {
                    // Show unsaved changes dialog and cancel close if needed
                    var response = await ShowUnsavedChangesDialogAsync();
                    if (response == UnsavedChangesResponse.Cancel)
                    {
                        // Cancel window close
                        return false;
                    }
                    else if (response == UnsavedChangesResponse.Save)
                    {
                        // Save document first
                        await SaveDocumentWithConfirmationAsync();
                    }
                    // Otherwise, discard changes and continue closing
                }
                
                return true;
            };
            
            Logger.LogInformation("Notepad application initialized");
        }

        /// <summary>
        /// Called after the component has been rendered
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            
            if (firstRender)
            {
                try
                {
                    // Import JavaScript module for enhanced functionality
                    JsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                        "import", "./_content/HackerOs/js/notepad.js");
                    
                    if (JsModule != null)
                    {
                        await JsModule.InvokeVoidAsync("initNotepad", TextAreaRef);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to initialize JS module for Notepad");
                }
            }
        }

        /// <summary>
        /// Called when the application is started
        /// </summary>
        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            Logger.LogInformation("Starting Notepad application");
            
            // Process launch parameters if any
            if (context.Parameters != null && context.Parameters.TryGetValue("filePath", out var filePath))
            {
                if (filePath != null)
                {
                    await OpenFileAsync(filePath.ToString());
                }
            }
            
            return await base.OnStartAsync(context);
        }

        /// <summary>
        /// Called when the application is stopped
        /// </summary>
        protected override async Task<bool> OnStopAsync()
        {
            Logger.LogInformation("Stopping Notepad application");
            
            // Clean up resources
            if (JsModule != null)
            {
                await JsModule.DisposeAsync();
                JsModule = null;
            }
            
            return await base.OnStopAsync();
        }

        #endregion

        #region Document Operations

        /// <summary>
        /// Creates a new document
        /// </summary>
        private async Task NewDocument()
        {
            if (IsDocumentModified)
            {
                var response = await ShowUnsavedChangesDialogAsync();
                if (response == UnsavedChangesResponse.Cancel)
                {
                    return;
                }
                else if (response == UnsavedChangesResponse.Save)
                {
                    await SaveDocumentWithConfirmationAsync();
                }
            }
            
            // Reset document state
            DocumentContent = string.Empty;
            CurrentFilePath = null;
            IsDocumentModified = false;
            UpdateTextStatistics();
            
            Logger.LogInformation("Created new document");
            StateHasChanged();
        }

        /// <summary>
        /// Opens a document by showing the file dialog
        /// </summary>
        private void OpenDocument()
        {
            ShowOpenFileDialog = true;
            StateHasChanged();
        }

        /// <summary>
        /// Saves the current document
        /// </summary>
        private async Task SaveDocument()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                // No current file path, show save as dialog
                await SaveDocumentAs();
                return;
            }
            
            await SaveFileAsync(CurrentFilePath);
        }

        /// <summary>
        /// Saves the document with a new name
        /// </summary>
        private Task SaveDocumentAs()
        {
            SaveFileName = string.IsNullOrEmpty(CurrentFilePath) 
                ? "untitled.txt" 
                : HSystem.IO.HPath.GetFileName(CurrentFilePath);
                
            ShowSaveFileDialog = true;
            StateHasChanged();
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Opens a file from the given path
        /// </summary>
        private async Task OpenFileAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || FileSystem == null)
            {
                return;
            }
            
            try
            {
                // Get current user
                var currentUser = await UserManager.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    Logger.LogWarning("No current user found when trying to open file");
                    return;
                }
                
                // Read file content
                var content = await FileSystem.ReadAllTextAsync(filePath, currentUser);
                
                // Update document state
                DocumentContent = content;
                CurrentFilePath = filePath;
                IsDocumentModified = false;
                UpdateTextStatistics();
                
                Logger.LogInformation("Opened file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening file: {FilePath}", filePath);
                await RaiseErrorReceivedAsync($"Error opening file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves the current content to a file
        /// </summary>
        private async Task SaveFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || FileSystem == null)
            {
                return;
            }
            
            try
            {
                // Get current user
                var currentUser = await UserManager.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    Logger.LogWarning("No current user found when trying to save file");
                    return;
                }
                
                // Write content to file
                await FileSystem.WriteAllTextAsync(filePath, DocumentContent, currentUser);
                
                // Update document state
                CurrentFilePath = filePath;
                IsDocumentModified = false;
                
                Logger.LogInformation("Saved file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving file: {FilePath}", filePath);
                await RaiseErrorReceivedAsync($"Error saving file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves the document with confirmation dialogs if needed
        /// </summary>
        private async Task SaveDocumentWithConfirmationAsync()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                // No current file path, need to show save as dialog
                SaveFileName = "untitled.txt";
                ShowSaveFileDialog = true;
                StateHasChanged();
                
                // Wait for the dialog to complete
                var tcs = new TaskCompletionSource<bool>();
                AfterUnsavedChangesAction = () => tcs.SetResult(true);
                await tcs.Task;
            }
            else
            {
                // Save directly
                await SaveFileAsync(CurrentFilePath);
            }
        }

        /// <summary>
        /// Updates text statistics (line count, character count)
        /// </summary>
        private void UpdateTextStatistics()
        {
            CharacterCount = DocumentContent.Length;
            LineCount = DocumentContent.Split('\n').Length;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles input changes in the text area
        /// </summary>
        private void HandleDocumentChanged(ChangeEventArgs e)
        {
            if (e.Value is string newContent)
            {
                DocumentContent = newContent;
                IsDocumentModified = true;
                UpdateTextStatistics();
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts
        /// </summary>
        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            // Handle keyboard shortcuts
            if (e.CtrlKey)
            {
                switch (e.Key?.ToLower())
                {
                    case "s":
                        if (e.ShiftKey)
                        {
                            await SaveDocumentAs();
                        }
                        else
                        {
                            await SaveDocument();
                        }
                        break;
                    case "o":
                        await CheckUnsavedChangesAsync(() => OpenDocument());
                        break;
                    case "n":
                        await NewDocument();
                        break;
                }
            }
        }

        #endregion

        #region File Browser Methods

        /// <summary>
        /// Loads directory entries for the specified path
        /// </summary>
        private async Task LoadDirectoryEntriesAsync(string directory)
        {
            if (string.IsNullOrEmpty(directory) || FileSystem == null)
            {
                return;
            }
            
            try
            {
                // Get current user
                var currentUser = await UserManager.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    Logger.LogWarning("No current user found when trying to load directory");
                    return;
                }
                
                // Get directory entries
                var entries = await FileSystem.GetDirectoryEntriesAsync(directory, currentUser);
                
                // Sort: directories first, then files, both alphabetically
                DirectoryEntries = entries
                    .OrderByDescending(e => e.IsDirectory)
                    .ThenBy(e => e.Name)
                    .ToList();
                
                // Add parent directory entry if not at root
                if (directory != "/")
                {
                    var parentPath = HSystem.IO.HPath.GetDirectoryName(directory);
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        DirectoryEntries.Insert(0, new FileSystemEntry
                        {
                            Name = "..",
                            FullPath = parentPath,
                            IsDirectory = true,
                            LastModified = DateTime.MinValue
                        });
                    }
                }
                
                // Update current directory
                CurrentDirectory = directory;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading directory: {Directory}", directory);
                DirectoryEntries = new List<FileSystemEntry>();
            }
        }

        /// <summary>
        /// Selects a file entry in the file browser
        /// </summary>
        private void SelectFileEntry(FileSystemEntry entry)
        {
            SelectedFileEntry = entry;
            
            // If in save dialog and not a directory, update filename
            if (ShowSaveFileDialog && !entry.IsDirectory)
            {
                SaveFileName = entry.Name;
            }
        }

        /// <summary>
        /// Handles double-click on a file entry
        /// </summary>
        private async Task HandleFileEntryDoubleClick(FileSystemEntry entry)
        {
            if (entry.IsDirectory)
            {
                // Navigate to directory
                await LoadDirectoryEntriesAsync(entry.FullPath);
                SelectedFileEntry = null;
            }
            else if (ShowOpenFileDialog)
            {
                // In open dialog, select and open file
                SelectedFileEntry = entry;
                await ConfirmOpenFile();
            }
            else if (ShowSaveFileDialog)
            {
                // In save dialog, select file
                SelectedFileEntry = entry;
                SaveFileName = entry.Name;
            }
        }

        /// <summary>
        /// Closes the open file dialog
        /// </summary>
        private void CloseOpenFileDialog()
        {
            ShowOpenFileDialog = false;
            SelectedFileEntry = null;
        }

        /// <summary>
        /// Confirms opening the selected file
        /// </summary>
        private async Task ConfirmOpenFile()
        {
            if (SelectedFileEntry == null || SelectedFileEntry.IsDirectory)
            {
                return;
            }
            
            await OpenFileAsync(SelectedFileEntry.FullPath);
            CloseOpenFileDialog();
        }

        /// <summary>
        /// Closes the save file dialog
        /// </summary>
        private void CloseSaveFileDialog()
        {
            ShowSaveFileDialog = false;
            SelectedFileEntry = null;
            
            // Execute any pending action
            AfterUnsavedChangesAction?.Invoke();
            AfterUnsavedChangesAction = null;
        }

        /// <summary>
        /// Confirms saving the file with the specified name
        /// </summary>
        private async Task ConfirmSaveFile()
        {
            if (string.IsNullOrWhiteSpace(SaveFileName))
            {
                return;
            }
            
            // Construct full file path
            var filePath = HSystem.IO.HPath.Combine(CurrentDirectory, SaveFileName);
            
            // Save file
            await SaveFileAsync(filePath);
            
            // Close dialog
            ShowSaveFileDialog = false;
            
            // Execute any pending action
            AfterUnsavedChangesAction?.Invoke();
            AfterUnsavedChangesAction = null;
        }

        #endregion

        #region Unsaved Changes Dialog

        /// <summary>
        /// Shows the unsaved changes dialog
        /// </summary>
        private Task<UnsavedChangesResponse> ShowUnsavedChangesDialogAsync()
        {
            UnsavedChangesDialogTask = new TaskCompletionSource<UnsavedChangesResponse>();
            ShowUnsavedChangesDialog = true;
            StateHasChanged();
            
            return UnsavedChangesDialogTask.Task;
        }

        /// <summary>
        /// Handles the response from the unsaved changes dialog
        /// </summary>
        private void HandleUnsavedChangesResponse(UnsavedChangesResponse response)
        {
            ShowUnsavedChangesDialog = false;
            UnsavedChangesDialogTask?.SetResult(response);
            UnsavedChangesDialogTask = null;
        }

        /// <summary>
        /// Checks for unsaved changes before executing an action
        /// </summary>
        private async Task CheckUnsavedChangesAsync(Action action)
        {
            if (IsDocumentModified)
            {
                var response = await ShowUnsavedChangesDialogAsync();
                if (response == UnsavedChangesResponse.Cancel)
                {
                    return;
                }
                else if (response == UnsavedChangesResponse.Save)
                {
                    await SaveDocumentWithConfirmationAsync();
                }
            }
            
            action();
        }

        #endregion
    }

    /// <summary>
    /// Represents the possible responses to the unsaved changes dialog
    /// </summary>
    public enum UnsavedChangesResponse
    {
        /// <summary>
        /// Save the document
        /// </summary>
        Save,
        
        /// <summary>
        /// Discard changes
        /// </summary>
        Discard,
        
        /// <summary>
        /// Cancel the operation
        /// </summary>
        Cancel
    }
}
