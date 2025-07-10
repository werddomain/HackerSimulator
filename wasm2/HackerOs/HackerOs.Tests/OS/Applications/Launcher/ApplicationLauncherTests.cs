using BlazorWindowManager;
using HackerOs.OS.Applications.Launcher;
using HackerOs.OS.Applications.Lifecycle;
using HackerOs.OS.Applications.Registry;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using HackerOs.Tests.OS.Applications.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HackerOs.Tests.OS.Applications.Launcher
{
    public class ApplicationLauncherTests
    {
        private readonly Mock<IApplicationRegistry> _registryMock;
        private readonly Mock<IWindowManager> _windowManagerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<ILogger<ApplicationLauncher>> _loggerMock;
        private readonly Mock<IVirtualFileSystem> _fileSystemMock;
        private readonly Mock<User> _userMock;
        
        public ApplicationLauncherTests()
        {
            _registryMock = new Mock<IApplicationRegistry>();
            _windowManagerMock = new Mock<IWindowManager>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _loggerMock = new Mock<ILogger<ApplicationLauncher>>();
            _fileSystemMock = new Mock<IVirtualFileSystem>();
            _userMock = new Mock<User>();
            
            // Setup service provider to create mock applications
            var mockBasicApp = new MockBasicApplication();
            var mockWindowApp = new MockWindowApplication();
            
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockBasicApplication)))
                .Returns(mockBasicApp);
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(MockWindowApplication)))
                .Returns(mockWindowApp);
                
            // Setup file system
            _fileSystemMock.Setup(fs => fs.FileExistsAsync(It.IsAny<string>(), It.IsAny<User>()))
                .ReturnsAsync(true);
                
            // Setup registry to return mock applications
            _registryMock.Setup(r => r.GetApplicationById("mock.BasicApp"))
                .Returns(new ApplicationMetadata
                {
                    Id = "mock.BasicApp",
                    Name = "Basic Mock App",
                    IconPath = "fa-solid:cube",
                    Type = typeof(MockBasicApplication)
                });
                
            _registryMock.Setup(r => r.GetApplicationById("mock.WindowApp"))
                .Returns(new ApplicationMetadata
                {
                    Id = "mock.WindowApp",
                    Name = "Window Mock App",
                    IconPath = "fa-solid:window-maximize",
                    Type = typeof(MockWindowApplication)
                });
        }
        
        [Fact]
        public async Task LaunchApplicationAsync_ShouldCreateAndStartApplication()
        {
            // Arrange
            var launcher = CreateLauncher();
            
            // Act
            var app = await launcher.LaunchApplicationAsync("mock.BasicApp");
            
            // Assert
            Assert.NotNull(app);
            Assert.IsType<MockBasicApplication>(app);
            var mockApp = app as MockBasicApplication;
            Assert.True(mockApp?.StartCalled);
        }
        
        [Fact]
        public async Task LaunchApplicationAsync_ShouldCreateWindowForWindowApplication()
        {
            // Arrange
            var launcher = CreateLauncher();
            
            // Setup window manager to return a mock window
            var mockWindow = new Mock<IWindow>();
            _windowManagerMock.Setup(wm => wm.CreateWindowAsync(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(mockWindow.Object);
            
            // Act
            var app = await launcher.LaunchApplicationAsync("mock.WindowApp");
            
            // Assert
            Assert.NotNull(app);
            Assert.IsType<MockWindowApplication>(app);
            
            // Verify window was created
            _windowManagerMock.Verify(wm => wm.CreateWindowAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()), Times.Once);
        }
        
        [Fact]
        public async Task LaunchApplicationAsync_ShouldTrackRunningApplications()
        {
            // Arrange
            var launcher = CreateLauncher();
            
            // Act
            var app1 = await launcher.LaunchApplicationAsync("mock.BasicApp");
            var app2 = await launcher.LaunchApplicationAsync("mock.WindowApp");
            var runningApps = launcher.GetRunningApplications();
            
            // Assert
            Assert.Equal(2, runningApps.Count);
            Assert.Contains(app1, runningApps);
            Assert.Contains(app2, runningApps);
        }
        
        [Fact]
        public async Task CloseApplicationAsync_ShouldRemoveFromRunningList()
        {
            // Arrange
            var launcher = CreateLauncher();
            var app = await launcher.LaunchApplicationAsync("mock.BasicApp");
            
            // Act
            await launcher.CloseApplicationAsync(app);
            var runningApps = launcher.GetRunningApplications();
            
            // Assert
            Assert.Empty(runningApps);
            var mockApp = app as MockBasicApplication;
            Assert.True(mockApp?.CloseCalled);
        }
        
        [Fact]
        public async Task CloseApplicationAsync_ShouldRespectCloseRequest()
        {
            // Arrange
            var launcher = CreateLauncher();
            var app = await launcher.LaunchApplicationAsync("mock.BasicApp") as MockBasicApplication;
            if (app != null)
            {
                app.AllowClose = false;
            }
            
            // Act
            await launcher.CloseApplicationAsync(app);
            var runningApps = launcher.GetRunningApplications();
            
            // Assert
            Assert.Single(runningApps); // Should still be running
            Assert.Contains(app, runningApps);
            Assert.True(app?.CloseRequestCalled);
            Assert.False(app?.CloseCalled);
        }
        
        [Fact]
        public async Task LaunchApplicationAsync_ShouldReturnNullForNonExistingApplication()
        {
            // Arrange
            var launcher = CreateLauncher();
            
            // Act
            var app = await launcher.LaunchApplicationAsync("non.existing.app");
            
            // Assert
            Assert.Null(app);
        }
        
        private ApplicationLauncher CreateLauncher()
        {
            return new ApplicationLauncher(
                _registryMock.Object,
                _windowManagerMock.Object,
                _serviceProviderMock.Object,
                _loggerMock.Object,
                _fileSystemMock.Object,
                _userMock.Object);
        }
    }
}
