using Bunit;
using HackerOs.OS.Applications.BuiltIn.Notepad;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace HackerOs.Tests.OS.Applications.BuiltIn.Notepad
{
    public class NotepadComponentTests : TestContext
    {
        private readonly Mock<IVirtualFileSystem> _fileSystemMock;
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<IJSObjectReference> _jsModuleMock;
        private readonly Mock<User> _userMock;
        
        public NotepadComponentTests()
        {
            _fileSystemMock = new Mock<IVirtualFileSystem>();
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _jsModuleMock = new Mock<IJSObjectReference>();
            _userMock = new Mock<User>();
            
            // Setup JSRuntime to return a module
            _jsRuntimeMock.Setup(js => js.InvokeAsync<IJSObjectReference>(
                    It.Is<string>(s => s == "import"),
                    It.IsAny<object[]>()))
                .ReturnsAsync(_jsModuleMock.Object);
                
            // Setup JSModule
            _jsModuleMock.Setup(module => module.InvokeVoidAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>()))
                .Returns(ValueTask.CompletedTask);
                
            // Setup file system
            _fileSystemMock.Setup(fs => fs.FileExistsAsync(It.IsAny<string>(), It.IsAny<User>()))
                .ReturnsAsync(true);
                
            _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<User>()))
                .ReturnsAsync("Test file content");
                
            _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<User>()))
                .Returns(Task.CompletedTask);
                
            _fileSystemMock.Setup(fs => fs.GetDirectoryEntriesAsync(It.IsAny<string>(), It.IsAny<User>()))
                .ReturnsAsync(new List<HackerOs.OS.IO.FileSystem.FileSystemEntry>
                {
                    new HackerOs.OS.IO.FileSystem.FileSystemEntry
                    {
                        Name = "testfile.txt",
                        Path = "/home/user/testfile.txt",
                        IsDirectory = false
                    },
                    new HackerOs.OS.IO.FileSystem.FileSystemEntry
                    {
                        Name = "testdir",
                        Path = "/home/user/testdir",
                        IsDirectory = true
                    }
                });
                
            // Register services
            Services.AddSingleton(_jsRuntimeMock.Object);
            Services.AddSingleton(_fileSystemMock.Object);
        }
        
        [Fact]
        public void Render_ShouldDisplayNotepadInterface()
        {
            // Arrange & Act
            var component = RenderComponent<NotepadComponent>(parameters => parameters
                .Add(p => p.Content, "Test content")
                .Add(p => p.FilePath, "/path/to/test.txt")
                .Add(p => p.FileSystem, _fileSystemMock.Object)
                .Add(p => p.CurrentUser, _userMock.Object));
                
            // Assert
            Assert.Contains("Test content", component.Markup);
            Assert.Contains("/path/to/test.txt", component.Markup);
            Assert.Contains("notepad-container", component.Markup);
            Assert.Contains("notepad-toolbar", component.Markup);
            Assert.Contains("notepad-editor", component.Markup);
        }
        
        [Fact]
        public async Task NewFile_ShouldClearContentAndFilePath()
        {
            // Arrange
            var component = RenderComponent<NotepadComponent>(parameters => parameters
                .Add(p => p.Content, "Existing content")
                .Add(p => p.FilePath, "/path/to/file.txt")
                .Add(p => p.IsModified, true)
                .Add(p => p.FileSystem, _fileSystemMock.Object)
                .Add(p => p.CurrentUser, _userMock.Object));
                
            // Setup confirmation dialog to return true
            _jsRuntimeMock.Setup(js => js.InvokeAsync<bool>(
                    It.Is<string>(s => s == "confirm"),
                    It.IsAny<object[]>()))
                .ReturnsAsync(true);
                
            // Act
            var newButton = component.Find("button.toolbar-button:nth-of-type(1)");
            await newButton.ClickAsync(new MouseEventArgs());
            
            // Assert
            Assert.Equal(string.Empty, component.Instance.Content);
            Assert.Null(component.Instance.FilePath);
            Assert.False(component.Instance.IsModified);
        }
        
        [Fact]
        public async Task OpenFile_ShouldShowOpenDialog()
        {
            // Arrange
            var component = RenderComponent<NotepadComponent>(parameters => parameters
                .Add(p => p.Content, "")
                .Add(p => p.FileSystem, _fileSystemMock.Object)
                .Add(p => p.CurrentUser, _userMock.Object));
                
            // Setup user home directory
            _userMock.Setup(u => u.HomeDirectory).Returns("/home/user");
                
            // Act
            var openButton = component.Find("button.toolbar-button:nth-of-type(2)");
            await openButton.ClickAsync(new MouseEventArgs());
            
            // Assert
            Assert.Contains("dialog-overlay", component.Markup);
            Assert.Contains("Open File", component.Markup);
            Assert.Contains("testfile.txt", component.Markup);
            Assert.Contains("testdir", component.Markup);
        }
        
        [Fact]
        public async Task SaveFile_ShouldSaveContentToFile()
        {
            // Arrange
            var filePath = "/home/user/test.txt";
            var content = "Content to save";
            
            var component = RenderComponent<NotepadComponent>(parameters => parameters
                .Add(p => p.Content, content)
                .Add(p => p.FilePath, filePath)
                .Add(p => p.FileSystem, _fileSystemMock.Object)
                .Add(p => p.CurrentUser, _userMock.Object));
                
            // Act
            var saveButton = component.Find("button.toolbar-button:nth-of-type(3)");
            await saveButton.ClickAsync(new MouseEventArgs());
            
            // Assert
            _fileSystemMock.Verify(fs => fs.WriteAllTextAsync(
                It.Is<string>(s => s == filePath),
                It.Is<string>(s => s == content),
                It.IsAny<User>()),
                Times.Once);
                
            Assert.False(component.Instance.IsModified);
        }
        
        [Fact]
        public async Task SaveFileAs_ShouldShowSaveDialog()
        {
            // Arrange
            var component = RenderComponent<NotepadComponent>(parameters => parameters
                .Add(p => p.Content, "Content to save")
                .Add(p => p.FileSystem, _fileSystemMock.Object)
                .Add(p => p.CurrentUser, _userMock.Object));
                
            // Setup user home directory
            _userMock.Setup(u => u.HomeDirectory).Returns("/home/user");
                
            // Act
            var saveAsButton = component.Find("button.toolbar-button:nth-of-type(4)");
            await saveAsButton.ClickAsync(new MouseEventArgs());
            
            // Assert
            Assert.Contains("dialog-overlay", component.Markup);
            Assert.Contains("Save File", component.Markup);
            Assert.Contains("file-name-input", component.Markup);
            Assert.Contains("untitled.txt", component.Markup);
        }
        
        [Fact]
        public void ContentChange_ShouldUpdateModifiedFlag()
        {
            // Arrange
            var component = RenderComponent<NotepadComponent>(parameters => parameters
                .Add(p => p.Content, "Initial content")
                .Add(p => p.FilePath, "/path/to/file.txt")
                .Add(p => p.IsModified, false)
                .Add(p => p.FileSystem, _fileSystemMock.Object)
                .Add(p => p.CurrentUser, _userMock.Object));
                
            // Setup event callback to update component state
            var contentChangedCalled = false;
            var contentChanged = EventCallback.Factory.Create<string>(this, (string newContent) => {
                component.SetParametersAndRender(parameters => parameters
                    .Add(p => p.Content, newContent));
                contentChangedCalled = true;
            });
                
            var isModifiedChangedCalled = false;
            var isModifiedChanged = EventCallback.Factory.Create<bool>(this, (bool newValue) => {
                component.SetParametersAndRender(parameters => parameters
                    .Add(p => p.IsModified, newValue));
                isModifiedChangedCalled = true;
            });
                
            component.SetParametersAndRender(parameters => parameters
                .Add(p => p.ContentChanged, contentChanged)
                .Add(p => p.IsModifiedChanged, isModifiedChanged));
                
            // Act
            var textarea = component.Find("textarea");
            textarea.Change("Updated content");
            
            // Assert
            Assert.Equal("Updated content", component.Instance.Content);
            Assert.True(contentChangedCalled);
            Assert.True(isModifiedChangedCalled);
        }
    }
}
