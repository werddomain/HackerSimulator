using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProxyServer.GUI.ViewModels
{
    public partial class ProxySettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _useAuthentication;

        [ObservableProperty]
        private bool _useEncryption;

        [ObservableProperty]
        private string _authToken = string.Empty;

        [ObservableProperty]
        private int _connectionTimeout = 30;

        [ObservableProperty]
        private int _maxConnections = 100;

        [ObservableProperty]
        private bool _enableLogging = true;

        [ObservableProperty]
        private string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyServer", "logs");

        [ObservableProperty]
        private bool _enableTrafficMonitoring = true;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError = false;

        public ProxySettingsViewModel()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            // In a real implementation, this would load from a config file
            // Currently using default values
        }

        [RelayCommand]
        private void GenerateAuthToken()
        {
            AuthToken = Guid.NewGuid().ToString("N");
        }

        [RelayCommand]
        private void SaveSettings()
        {
            try
            {
                // In a real implementation, this would save to a config file
                StatusMessage = "Settings saved successfully";
                HasError = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
                HasError = true;
            }
        }

        [RelayCommand]
        private void BrowseLogPath()
        {
            // In a real implementation, this would open a folder picker
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyServer", "logs");
        }

        [RelayCommand]
        private void ResetSettings()
        {
            UseAuthentication = false;
            UseEncryption = false;
            AuthToken = string.Empty;
            ConnectionTimeout = 30;
            MaxConnections = 100;
            EnableLogging = true;
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProxyServer", "logs");
            EnableTrafficMonitoring = true;
            
            StatusMessage = "Settings reset to defaults";
            HasError = false;
        }
    }
}
