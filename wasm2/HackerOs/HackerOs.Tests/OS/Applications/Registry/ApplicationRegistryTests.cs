using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Icons;
using HackerOs.OS.Applications.Registry;
using HackerOs.Tests.OS.Applications.Mocks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace HackerOs.Tests.OS.Applications.Registry
{
    public class ApplicationRegistryTests
    {
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IIconFactory> _iconFactoryMock;
        private readonly Mock<ILogger<ApplicationRegistry>> _loggerMock;
        
        public ApplicationRegistryTests()
        {
            _serviceProviderMock = new Mock<IServiceProvider>();
            _iconFactoryMock = new Mock<IIconFactory>();
            _loggerMock = new Mock<ILogger<ApplicationRegistry>>();
            
            // Setup icon factory to return a simple RenderFragment
            _iconFactoryMock.Setup(f => f.GetIcon(It.IsAny<string>()))
                .Returns((string iconPath) => builder =>
                {
                    builder.OpenElement(0, "i");
                    builder.AddAttribute(1, "class", iconPath);
                    builder.CloseElement();
                });
        }
        
        [Fact]
        public async Task Initialize_ShouldDiscoverApplicationsWithAppAttribute()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            
            // Act
            await registry.InitializeAsync();
            var applications = registry.GetAllApplications();
            
            // Assert
            Assert.NotEmpty(applications);
            Assert.Contains(applications, app => app.Id == "mock.BasicApp");
            Assert.Contains(applications, app => app.Id == "mock.WindowApp");
            Assert.Contains(applications, app => app.Id == "mock.CategoryApp1");
            Assert.Contains(applications, app => app.Id == "mock.CategoryApp2");
            
            // Should not include non-ApplicationBase classes
            Assert.DoesNotContain(applications, app => app.Id == "mock.NonApp");
        }
        
        [Fact]
        public async Task GetApplicationById_ShouldReturnCorrectApplication()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            await registry.InitializeAsync();
            
            // Act
            var app = registry.GetApplicationById("mock.WindowApp");
            
            // Assert
            Assert.NotNull(app);
            Assert.Equal("mock.WindowApp", app.Id);
            Assert.Equal("Window Mock App", app.Name);
            Assert.Equal("fa-solid:window-maximize", app.IconPath);
            Assert.Equal("A mock window application for testing", app.Description);
        }
        
        [Fact]
        public async Task GetApplicationById_ShouldReturnNullForNonExistingId()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            await registry.InitializeAsync();
            
            // Act
            var app = registry.GetApplicationById("non.existing.app");
            
            // Assert
            Assert.Null(app);
        }
        
        [Fact]
        public async Task GetApplicationsByCategory_ShouldReturnCorrectApplications()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            await registry.InitializeAsync();
            
            // Act
            var category1Apps = registry.GetApplicationsByCategory("Category1");
            var category2Apps = registry.GetApplicationsByCategory("Category2");
            var testApps = registry.GetApplicationsByCategory("Test");
            
            // Assert
            Assert.Single(category1Apps);
            Assert.Equal("mock.CategoryApp1", category1Apps.First().Id);
            
            Assert.Single(category2Apps);
            Assert.Equal("mock.CategoryApp2", category2Apps.First().Id);
            
            Assert.Equal(2, testApps.Count());
            Assert.Contains(testApps, app => app.Id == "mock.CategoryApp1");
            Assert.Contains(testApps, app => app.Id == "mock.CategoryApp2");
        }
        
        [Fact]
        public async Task SearchApplications_ShouldReturnMatchingApplications()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            await registry.InitializeAsync();
            
            // Act
            var results1 = registry.SearchApplications("Mock");
            var results2 = registry.SearchApplications("Window");
            var results3 = registry.SearchApplications("Category");
            
            // Assert
            Assert.Equal(4, results1.Count()); // All mock apps
            Assert.Single(results2); // Only window app
            Assert.Equal(2, results3.Count()); // Both category apps
        }
        
        [Fact]
        public async Task GetIcon_ShouldReturnIconRenderFragment()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            await registry.InitializeAsync();
            var app = registry.GetApplicationById("mock.BasicApp");
            
            // Act
            var iconFragment = app?.Icon;
            
            // Assert
            Assert.NotNull(iconFragment);
            // Verify the icon factory was called
            _iconFactoryMock.Verify(f => f.GetIcon("fa-solid:cube"), Times.AtLeastOnce);
        }
        
        [Fact]
        public async Task Initialize_ShouldCacheApplications()
        {
            // Arrange
            var registry = CreateApplicationRegistry();
            
            // Act
            await registry.InitializeAsync();
            var firstCall = registry.GetAllApplications();
            var secondCall = registry.GetAllApplications();
            
            // Assert
            Assert.Same(firstCall, secondCall); // Should return the same instance (cached)
        }
        
        private ApplicationRegistry CreateApplicationRegistry()
        {
            // Set up service provider to return our mock dependencies
            var services = new ServiceCollection();
            services.AddSingleton(_iconFactoryMock.Object);
            services.AddSingleton(_loggerMock.Object);
            
            var serviceProvider = services.BuildServiceProvider();
            
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IIconFactory)))
                .Returns(_iconFactoryMock.Object);
            
            // Create registry with the current assembly to find our mock applications
            return new ApplicationRegistry(
                serviceProvider,
                _loggerMock.Object,
                new[] { typeof(MockApplications).Assembly });
        }
    }
}
