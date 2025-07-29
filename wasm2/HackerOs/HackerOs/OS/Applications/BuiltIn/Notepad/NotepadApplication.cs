using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Lifecycle;
using HackerOs.OS.Applications.Registry;
using HackerOs.OS.Applications.UI;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications.BuiltIn.Notepad;

[App("Notepad", "builtin.Notepad")]
public class NotepadApplication : WindowApplicationBase
{
    private string _content = string.Empty;
    private string? _currentFilePath;
    private bool _isModified = false;
    private IVirtualFileSystem? _fileSystem;
    private User.User? _currentUser;
    
    protected override string WindowTitle => 
        string.IsNullOrEmpty(_currentFilePath) 
        ? "Untitled - Notepad" 
        : $"{Path.GetFileName(_currentFilePath)}{(_isModified ? "*" : "")} - Notepad";
    
    protected override RenderFragment WindowIcon => builder =>
    {
        builder.OpenElement(0, "i");
        builder.AddAttribute(1, "class", "fas fa-file-alt");
        builder.CloseElement();
    };
    
    protected override RenderFragment GetWindowContent()
    {
        return builder =>
        {
            builder.OpenComponent<NotepadComponent>(0);
            builder.AddAttribute(1, "Content", _content);
            builder.AddAttribute(2, "ContentChanged", EventCallback.Factory.Create<string>(this, OnContentChanged));
            builder.AddAttribute(3, "FilePath", _currentFilePath);
            builder.AddAttribute(4, "FilePathChanged", EventCallback.Factory.Create<string?>(this, OnFilePathChanged));
            builder.AddAttribute(5, "IsModified", _isModified);
            builder.AddAttribute(6, "IsModifiedChanged", EventCallback.Factory.Create<bool>(this, OnIsModifiedChanged));
            builder.AddAttribute(7, "FileSystem", _fileSystem);
            builder.AddAttribute(8, "CurrentUser", _currentUser);
            builder.CloseComponent();
        };
    }
    
    // Implementation for ApplicationBase.OnStartAsync
    protected override Task<bool> OnStartAsync(ApplicationLaunchContext context)
    {
        // Get dependencies from service provider
        if (ServiceProvider != null)
        {
            _fileSystem = ServiceProvider.GetService<IVirtualFileSystem>();
            // Use the user from the context
            _currentUser = context.User;
        }
        
        // Check if there are any arguments (file to open)
        if (context.Arguments.Count > 0)
        {
            var filePath = context.Arguments[0];
            _ = OpenFileAsync(filePath);
        }
        
        return Task.FromResult(true);
    }
    
    protected override Task<bool> OnStopAsync()
    {
        // Save application state if needed
        return Task.FromResult(true);
    }
    
    // Implementation for ApplicationLifecycle.OnCloseRequestAsync
    public Task<bool> OnCloseRequestAsync(ApplicationLifecycleContext context)
    {
        // If the content is modified, ask for confirmation
        if (_isModified)
        {
            // Handle unsaved changes logic here
            // For now, just return true to allow closing
            return Task.FromResult(true);
        }
        
        return Task.FromResult(true);
    }
    
    private void OnContentChanged(string newContent)
    {
        _content = newContent;
        _isModified = true;
        UpdateWindowTitle();
    }
    
    private void OnFilePathChanged(string? newPath)
    {
        _currentFilePath = newPath;
        UpdateWindowTitle();
    }
    
    private void OnIsModifiedChanged(bool isModified)
    {
        _isModified = isModified;
        UpdateWindowTitle();
    }
    
    private async Task UpdateWindowTitleAsync()
    {
        // Simply log that we want to update the title
        await Task.CompletedTask;
        Console.WriteLine($"Would update window title to: {WindowTitle}");
    }
    
    private void UpdateWindowTitle()
    {
        // Fire and forget
        _ = UpdateWindowTitleAsync();
    }
    
    private async Task OpenFileAsync(string filePath)
    {
        if (_fileSystem == null || _currentUser == null)
            return;
            
        try
        {
            var content = await _fileSystem.ReadAllTextAsync(filePath, _currentUser);
            if (content != null)
            {
                _content = content;
                _currentFilePath = filePath;
                _isModified = false;
                UpdateWindowTitle();
            }
        }
        catch (Exception ex)
        {
            // Log error
            Console.Error.WriteLine($"Error opening file: {ex.Message}");
        }
    }
    
    private async Task SaveFileAsync(string filePath)
    {
        if (_fileSystem == null || _currentUser == null)
            return;
            
        try
        {
            var success = await _fileSystem.WriteAllTextAsync(filePath, _content, _currentUser);
            if (success)
            {
                _currentFilePath = filePath;
                _isModified = false;
                UpdateWindowTitle();
            }
        }
        catch (Exception ex)
        {
            // Log error
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
        }
    }
    
    // State serialization methods for application persistence
    public Dictionary<string, JsonElement> GetStateForSerialization()
    {
        var state = new Dictionary<string, JsonElement>();
        
        // Convert string values to JsonElement
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            content = _content,
            currentFilePath = _currentFilePath,
            isModified = _isModified
        }));
        
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            state[prop.Name] = prop.Value.Clone();
        }
        
        return state;
    }
    
    public async Task ApplyStateFromSerializationAsync(Dictionary<string, JsonElement> state)
    {
        if (state.TryGetValue("content", out var contentElement))
        {
            _content = contentElement.GetString() ?? string.Empty;
        }
        
        if (state.TryGetValue("currentFilePath", out var pathElement))
        {
            _currentFilePath = pathElement.GetString();
        }
        
        if (state.TryGetValue("isModified", out var modifiedElement))
        {
            _isModified = modifiedElement.GetBoolean();
        }
        
        UpdateWindowTitle();
        await Task.CompletedTask;
    }
}
