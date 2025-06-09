using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyServer.Protocol.Models.FileSystem;
using System.Collections.ObjectModel;

namespace ProxyServer.GUI.ViewModels
{
    public partial class SharedFolderViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<SharedFolderInfo> _sharedFolders = new();

        [ObservableProperty]
        private SharedFolderInfo? _selectedFolder;

        [ObservableProperty]
        private string _newFolderPath = string.Empty;

        [ObservableProperty]
        private string _newFolderAlias = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isReadOnly = true;

        [ObservableProperty]
        private bool _hasError = false;

        public SharedFolderViewModel()
        {
            // Load shared folders from configuration
            LoadSharedFolders();
        }

        private void LoadSharedFolders()
        {
            // This would be replaced with actual loading from configuration
            SharedFolders.Clear();
            SharedFolders.Add(new SharedFolderInfo
            {
                Id = Guid.NewGuid().ToString(),
                HostPath = @"C:\Users\Documents",
                Alias = "documents",
                Permissions = "read-write"
            });
            
            SharedFolders.Add(new SharedFolderInfo
            {
                Id = Guid.NewGuid().ToString(),
                HostPath = @"C:\Users\Pictures",
                Alias = "pictures",
                Permissions = "read-only"
            });
        }

        [RelayCommand]
        private async Task BrowseFolder()
        {
            try
            {
                // In a real implementation, this would use a folder picker
                // For now, just simulate it
                NewFolderPath = @"C:\Users\Selected\Folder";
                if (string.IsNullOrEmpty(NewFolderAlias))
                {
                    NewFolderAlias = Path.GetFileName(NewFolderPath);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error selecting folder: {ex.Message}";
                HasError = true;
            }
        }

        [RelayCommand]
        private void AddSharedFolder()
        {
            if (string.IsNullOrEmpty(NewFolderPath) || string.IsNullOrEmpty(NewFolderAlias))
            {
                StatusMessage = "Folder path and alias are required";
                HasError = true;
                return;
            }

            try
            {
                var newFolder = new SharedFolderInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    HostPath = NewFolderPath,
                    Alias = NewFolderAlias,
                    Permissions = IsReadOnly ? "read-only" : "read-write"
                };

                SharedFolders.Add(newFolder);
                
                // Clear inputs
                NewFolderPath = string.Empty;
                NewFolderAlias = string.Empty;
                IsReadOnly = true;
                
                StatusMessage = $"Added shared folder '{newFolder.Alias}'";
                HasError = false;
                
                // In a real implementation, save to configuration
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding folder: {ex.Message}";
                HasError = true;
            }
        }

        [RelayCommand]
        private void RemoveSharedFolder()
        {
            if (SelectedFolder != null)
            {
                SharedFolders.Remove(SelectedFolder);
                StatusMessage = $"Removed shared folder '{SelectedFolder.Alias}'";
                HasError = false;
                SelectedFolder = null;
                
                // In a real implementation, save to configuration
            }
        }
    }
}
