using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Dispatching;
using ProxyServer.Network.TCP;
using ProxyServer.Network.WebSockets;
using ProxyServer.Protocol;
using ProxyServer.Protocol.Models.Network;
using System.Collections.ObjectModel;

namespace ProxyServer.GUI.ViewModels
{
    public partial class ProxyServerViewModel : ObservableObject
    {
        private readonly WebSocketServer _webSocketServer;
        private readonly TcpConnectionManager _tcpConnectionManager;
        private readonly ILogger _logger;

        [ObservableProperty]
        private bool _isServerRunning;

        [ObservableProperty]
        private string _webSocketHost = "127.0.0.1";

        [ObservableProperty]
        private int _webSocketPort = 8080;

        [ObservableProperty]
        private string _statusMessage = "Server stopped";

        [ObservableProperty]
        private int _activeConnections;

        [ObservableProperty]
        private long _totalBytesReceived;

        [ObservableProperty]
        private long _totalBytesSent;

        [ObservableProperty]
        private ObservableCollection<TcpConnectionViewModel> _activeConnectionsList = new();

        [ObservableProperty]
        private ObservableCollection<LogEntryViewModel> _recentLogEntries = new();        public ProxyServerViewModel(WebSocketServer webSocketServer, TcpConnectionManager tcpConnectionManager, ILogger logger)
        {
            _webSocketServer = webSocketServer;
            _tcpConnectionManager = tcpConnectionManager;
            _logger = logger;

            // Subscribe to connection events
            _tcpConnectionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            _tcpConnectionManager.DataReceived += OnDataReceived;
        }        private void OnConnectionStatusChanged(object? sender, TcpConnectionStatusEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (e.Status == ConnectionStatusMessage.Status.Connected)
                {
                    // Connection was added
                    ActiveConnectionsList.Add(new TcpConnectionViewModel
                    {
                        ConnectionId = e.ConnectionId,
                        RemoteEndpoint = GetRemoteEndpoint(e.ConnectionId),
                        StartTime = DateTime.Now,
                        LastActivity = DateTime.Now,
                        Status = "Connected"
                    });
                    ActiveConnections = ActiveConnectionsList.Count;
                    AddLogEntry("Info", $"Client connected: {e.ConnectionId}");
                }
                else if (e.Status == ConnectionStatusMessage.Status.Closed)
                {
                    // Connection was removed
                    var connection = ActiveConnectionsList.FirstOrDefault(c => c.ConnectionId == e.ConnectionId);
                    if (connection != null)
                    {
                        ActiveConnectionsList.Remove(connection);
                    }
                    ActiveConnections = ActiveConnectionsList.Count;
                    AddLogEntry("Info", $"Client disconnected: {e.ConnectionId}");
                }
            });
        }

        private void OnDataReceived(object? sender, TcpDataReceivedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TotalBytesReceived += e.Data.Length;
                UpdateConnectionStats(e.ConnectionId, bytesReceived: e.Data.Length);
            });
        }

        private void AddLogEntry(string level, string message)
        {
            var logEntry = new LogEntryViewModel
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Source = "ProxyServer"
            };

            RecentLogEntries.Insert(0, logEntry);

            // Keep only the most recent 100 log entries
            while (RecentLogEntries.Count > 100)
            {
                RecentLogEntries.RemoveAt(RecentLogEntries.Count - 1);
            }
        }

        private string GetRemoteEndpoint(string connectionId)
        {
            var connection = _tcpConnectionManager.GetConnection(connectionId);
            return connection?.RemoteEndPoint ?? "Unknown";
        }

        private void UpdateConnectionStats(string connectionId, int bytesReceived = 0, int bytesSent = 0)
        {
            var connection = ActiveConnectionsList.FirstOrDefault(c => c.ConnectionId == connectionId);
            if (connection != null)
            {
                connection.BytesReceived += bytesReceived;
                connection.BytesSent += bytesSent;
                connection.LastActivity = DateTime.Now;
            }
        }        [RelayCommand]
        private async Task StartServer()
        {        try
            {
                await _webSocketServer.StartAsync(WebSocketPort);
                IsServerRunning = true;
                StatusMessage = $"Server running on ws://{WebSocketHost}:{WebSocketPort}";
                AddLogEntry("Info", $"Server started on ws://{WebSocketHost}:{WebSocketPort}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting server: {ex.Message}";
                AddLogEntry("Error", $"Failed to start server: {ex.Message}");
                _logger.LogError(ex, "Failed to start server");
            }
        }        [RelayCommand]
        private void StopServer()
        {try
            {
                _webSocketServer.Stop();
                IsServerRunning = false;
                StatusMessage = "Server stopped";
                AddLogEntry("Info", "Server stopped");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error stopping server: {ex.Message}";
                AddLogEntry("Error", $"Failed to stop server: {ex.Message}");
                _logger.LogError(ex, "Failed to stop server");
            }
        }

        [RelayCommand]
        private void ClearStatistics()
        {
            TotalBytesReceived = 0;
            TotalBytesSent = 0;
        }

        [RelayCommand]
        private void ClearLogs()
        {
            RecentLogEntries.Clear();
        }        [RelayCommand]
        private async Task DisconnectClient(string connectionId)
        {
            var connection = _tcpConnectionManager.GetConnection(connectionId);
            if (connection != null)
            {
                await connection.CloseAsync();
            }
        }
    }

    public class TcpConnectionViewModel
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string RemoteEndpoint { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class LogEntryViewModel
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }
}
