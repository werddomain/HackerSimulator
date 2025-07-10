using HackerOs.OS.Applications.Lifecycle;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using HackerOs.Tests.OS.Applications.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace HackerOs.Tests.OS.Applications.Lifecycle
{
    public class ApplicationLifecycleTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IVirtualFileSystem> _fileSystemMock;
        private readonly Mock<User> _userMock;
        
        public ApplicationLifecycleTests()
        {
            _loggerMock = new Mock<ILogger>();
            _fileSystemMock = new Mock<IVirtualFileSystem>();
            _userMock = new Mock<User>();
        }
        
        [Fact]
        public async Task OnStartAsync_ShouldInitializeApplication()
        {
            // Arrange
            var app = new MockBasicApplication();
            var context = CreateLifecycleContext();
            
            // Act
            await app.OnStartAsync(context);
            
            // Assert
            Assert.True(app.StartCalled);
        }
        
        [Fact]
        public async Task OnCloseRequestAsync_ShouldReturnTrueWhenAllowCloseIsTrue()
        {
            // Arrange
            var app = new MockBasicApplication { AllowClose = true };
            var context = CreateLifecycleContext();
            
            // Act
            var result = await app.OnCloseRequestAsync(context);
            
            // Assert
            Assert.True(result);
            Assert.True(app.CloseRequestCalled);
        }
        
        [Fact]
        public async Task OnCloseRequestAsync_ShouldReturnFalseWhenAllowCloseIsFalse()
        {
            // Arrange
            var app = new MockBasicApplication { AllowClose = false };
            var context = CreateLifecycleContext();
            
            // Act
            var result = await app.OnCloseRequestAsync(context);
            
            // Assert
            Assert.False(result);
            Assert.True(app.CloseRequestCalled);
        }
        
        [Fact]
        public async Task OnCloseAsync_ShouldCleanupApplication()
        {
            // Arrange
            var app = new MockBasicApplication();
            var context = CreateLifecycleContext();
            
            // Act
            await app.OnCloseAsync(context);
            
            // Assert
            Assert.True(app.CloseCalled);
        }
        
        [Fact]
        public async Task SaveStateAsync_ShouldSerializeApplicationState()
        {
            // Arrange
            var app = new MockBasicApplication();
            
            // Act
            var state = await app.SaveStateAsync();
            var deserialized = JsonSerializer.Deserialize<JsonElement>(state);
            
            // Assert
            Assert.True(deserialized.TryGetProperty("testValue", out var testValue));
            Assert.Equal("serialized-value", testValue.GetString());
        }
        
        [Fact]
        public async Task LoadStateAsync_ShouldDeserializeApplicationState()
        {
            // Arrange
            var app = new MockWindowApplication();
            var state = @"{""testData"":""Updated Value""}";
            
            // Act
            await app.LoadStateAsync(state);
            
            // Assert
            Assert.Equal("Updated Value", app.TestData);
            Assert.True(app.LastState.ContainsKey("testData"));
            Assert.Equal("Updated Value", app.LastState["testData"]);
        }
        
        [Fact]
        public void GetWindowContent_ShouldReturnRenderFragment()
        {
            // Arrange
            var app = new MockWindowApplication();
            
            // Act
            var content = app.GetWindowContentInternal();
            
            // Assert
            Assert.NotNull(content);
            Assert.True(app.ContentRequested);
        }
        
        private ApplicationLifecycleContext CreateLifecycleContext(string[]? args = null)
        {
            return new ApplicationLifecycleContext
            {
                Logger = _loggerMock.Object,
                FileSystem = _fileSystemMock.Object,
                User = _userMock.Object,
                Arguments = args ?? Array.Empty<string>()
            };
        }
    }
}
