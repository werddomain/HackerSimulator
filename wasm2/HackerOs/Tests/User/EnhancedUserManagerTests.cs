using System;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HackerOs.Tests.User
{
    /// <summary>
    /// Tests for the EnhancedUserManager class
    /// </summary>
    [TestClass]
    public class EnhancedUserManagerTests
    {
        private Mock<IVirtualFileSystem> _mockFileSystem;
        private Mock<ILogger<EnhancedUserManager>> _mockLogger;
        private EnhancedUserManager _enhancedUserManager;
        
        [TestInitialize]
        public void Initialize()
        {
            _mockFileSystem = new Mock<IVirtualFileSystem>();
            _mockLogger = new Mock<ILogger<EnhancedUserManager>>();
            
            // Setup basic file system operations
            _mockFileSystem.Setup(fs => fs.DirectoryExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
            _mockFileSystem.Setup(fs => fs.CreateDirectoryAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);
            _mockFileSystem.Setup(fs => fs.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            
            // Create a mock UserManager
            var mockUserManager = new Mock<IUserManager>();
            mockUserManager.Setup(um => um.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string username, string fullName, string password, bool isAdmin) => 
                    new User
                    {
                        Username = username,
                        FullName = fullName,
                        IsAdmin = isAdmin,
                        UserId = 1000,
                        PrimaryGroupId = isAdmin ? 0 : 1000,
                        HomeDirectory = $"/home/{username}",
                        Shell = "/bin/bash"
                    });
                    
            // Create the enhanced user manager
            _enhancedUserManager = new EnhancedUserManager(mockUserManager.Object, _mockFileSystem.Object, _mockLogger.Object);
        }
        
        [TestMethod]
        public async Task CreateUserAsync_ShouldCreateHomeDirectory()
        {
            // Arrange
            string username = "testuser";
            string fullName = "Test User";
            string password = "password";
            bool isAdmin = false;
            
            // Act
            var user = await _enhancedUserManager.CreateUserAsync(username, fullName, password, isAdmin);
            
            // Assert
            Assert.IsNotNull(user);
            Assert.AreEqual(username, user.Username);
            
            // Verify home directory creation was attempted
            _mockFileSystem.Verify(fs => fs.CreateDirectoryAsync(It.Is<string>(s => s.Contains($"/home/{username}")), It.IsAny<bool>()), Times.AtLeastOnce);
        }
        
        [TestMethod]
        public async Task ResetUserHomeDirectoryAsync_ShouldResetHomeDirectory()
        {
            // Arrange
            string username = "testuser";
            
            _mockFileSystem.Setup(fs => fs.DirectoryExistsAsync($"/home/{username}")).ReturnsAsync(true);
            _mockFileSystem.Setup(fs => fs.DeleteDirectoryAsync($"/home/{username}", true)).ReturnsAsync(true);
            
            // Mock the user manager to return a user
            var mockUserManager = new Mock<IUserManager>();
            mockUserManager.Setup(um => um.GetUserAsync(username))
                .ReturnsAsync(new User
                {
                    Username = username,
                    FullName = "Test User",
                    UserId = 1000,
                    PrimaryGroupId = 1000,
                    HomeDirectory = $"/home/{username}",
                    Shell = "/bin/bash"
                });
                
            // Create a new enhanced user manager with the mocked user manager
            var enhancedUserManager = new EnhancedUserManager(mockUserManager.Object, _mockFileSystem.Object, _mockLogger.Object);
            
            // Act
            bool result = await enhancedUserManager.ResetUserHomeDirectoryAsync(username);
            
            // Assert
            Assert.IsTrue(result);
            
            // Verify directory operations
            _mockFileSystem.Verify(fs => fs.DeleteDirectoryAsync($"/home/{username}", true), Times.Once);
            _mockFileSystem.Verify(fs => fs.CreateDirectoryAsync(It.Is<string>(s => s.Contains($"/home/{username}")), It.IsAny<bool>()), Times.AtLeastOnce);
        }
        
        [TestMethod]
        public async Task DeleteUserAsync_ShouldBackupAndDeleteHomeDirectory()
        {
            // Arrange
            string username = "testuser";
            
            _mockFileSystem.Setup(fs => fs.DirectoryExistsAsync($"/home/{username}")).ReturnsAsync(true);
            _mockFileSystem.Setup(fs => fs.DeleteDirectoryAsync($"/home/{username}", true)).ReturnsAsync(true);
            _mockFileSystem.Setup(fs => fs.CopyDirectoryAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            
            // Mock the user manager to handle deletion
            var mockUserManager = new Mock<IUserManager>();
            mockUserManager.Setup(um => um.DeleteUserAsync(username)).ReturnsAsync(true);
            
            // Create a new enhanced user manager with the mocked user manager
            var enhancedUserManager = new EnhancedUserManager(mockUserManager.Object, _mockFileSystem.Object, _mockLogger.Object);
            
            // Act
            bool result = await enhancedUserManager.DeleteUserAsync(username);
            
            // Assert
            Assert.IsTrue(result);
            
            // Verify backup and deletion
            _mockFileSystem.Verify(fs => fs.CreateDirectoryAsync(It.Is<string>(s => s.Contains("/var/backups/home/")), It.IsAny<bool>()), Times.Once);
            _mockFileSystem.Verify(fs => fs.CopyDirectoryAsync(It.Is<string>(s => s == $"/home/{username}"), It.IsAny<string>()), Times.Once);
            _mockFileSystem.Verify(fs => fs.DeleteDirectoryAsync($"/home/{username}", true), Times.Once);
        }
    }
}
