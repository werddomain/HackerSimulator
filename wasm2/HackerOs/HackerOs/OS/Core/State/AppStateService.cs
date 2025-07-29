using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using HackerOs.OS.Security;

namespace HackerOs.OS.Core.State
{
    /// <summary>
    /// Implementation of the IAppStateService interface for managing global application state.
    /// </summary>
    public class AppStateService : IAppStateService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<AppStateService> _logger;
        private readonly IAuthenticationService _authService;
        private readonly ITokenService tokenService;
        private const string STATE_STORAGE_KEY = "hackeros_app_state";
        
        private bool _isAuthenticated;
        private string? _currentUsername;
        private bool _isLoading;
        private string? _errorMessage;
        private bool _isInitialized;
        private bool _isLocked;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AppStateService"/> class.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for browser storage access.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="authService">The authentication service.</param>
        public AppStateService(
            IJSRuntime jsRuntime,
            ILogger<AppStateService> logger,
            IAuthenticationService authService,
            ITokenService tokenService)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.tokenService = tokenService;

            // Subscribe to authentication state changes
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }
        
        /// <summary>
        /// Event triggered when the application state changes.
        /// </summary>
        public event EventHandler<AppStateChangedEventArgs>? StateChanged;
        
        /// <summary>
        /// Gets whether the user is authenticated.
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;
        
        /// <summary>
        /// Gets the current username, or null if not authenticated.
        /// </summary>
        public string? CurrentUsername => _currentUsername;
        
        /// <summary>
        /// Gets whether the application is in the process of loading.
        /// </summary>
        public bool IsLoading => _isLoading;
        
        /// <summary>
        /// Gets or sets the current error message, if any.
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                var oldValue = _errorMessage;
                _errorMessage = value;
                OnStateChanged(nameof(ErrorMessage), oldValue, value);
            }
        }
        
        /// <summary>
        /// Gets or sets whether the application has completed initial setup.
        /// </summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            set
            {
                var oldValue = _isInitialized;
                _isInitialized = value;
                OnStateChanged(nameof(IsInitialized), oldValue, value);
            }
        }
        
        /// <summary>
        /// Gets or sets whether the system is locked.
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                var oldValue = _isLocked;
                _isLocked = value;
                OnStateChanged(nameof(IsLocked), oldValue, value);
            }
        }
        
        /// <summary>
        /// Initializes the application state.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing application state");
                
                // Set loading state
                SetLoadingState(true);
                
                // Load state from browser storage
                await LoadStateAsync();
                
                // Check authentication status
                var isAuthenticated = await _authService.IsAuthenticatedAsync();
                if (isAuthenticated)
                {
                    // Get current session
                    var sessionManager = new SessionManager(tokenService, _jsRuntime);
                    var session = await sessionManager.GetActiveSessionAsync();
                    
                    if (session != null)
                    {
                        _currentUsername = session.User?.Username;
                        _isAuthenticated = true;
                        
                        _logger.LogInformation("User {Username} is authenticated", _currentUsername);
                    }
                }
                
                // Mark as initialized
                IsInitialized = true;
                
                // Clear loading state
                SetLoadingState(false);
                
                _logger.LogInformation("Application state initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing application state");
                ErrorMessage = "Failed to initialize the application. Please try again.";
                SetLoadingState(false);
            }
        }
        
        /// <summary>
        /// Sets the authentication state.
        /// </summary>
        /// <param name="isAuthenticated">Whether the user is authenticated.</param>
        /// <param name="username">The username, or null if not authenticated.</param>
        public void SetAuthenticationState(bool isAuthenticated, string? username = null)
        {
            var oldAuthenticated = _isAuthenticated;
            var oldUsername = _currentUsername;
            
            _isAuthenticated = isAuthenticated;
            _currentUsername = isAuthenticated ? username : null;
            
            OnStateChanged(nameof(IsAuthenticated), oldAuthenticated, isAuthenticated);
            OnStateChanged(nameof(CurrentUsername), oldUsername, _currentUsername);
            
            // Persist state changes
            _ = PersistStateAsync();
        }
        
        /// <summary>
        /// Sets the loading state.
        /// </summary>
        /// <param name="isLoading">Whether the application is loading.</param>
        public void SetLoadingState(bool isLoading)
        {
            var oldValue = _isLoading;
            _isLoading = isLoading;
            OnStateChanged(nameof(IsLoading), oldValue, isLoading);
        }
        
        /// <summary>
        /// Persists the current state to browser storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task PersistStateAsync()
        {
            try
            {
                var state = new
                {
                    IsAuthenticated = _isAuthenticated,
                    CurrentUsername = _currentUsername,
                    IsInitialized = _isInitialized,
                    IsLocked = _isLocked
                };
                
                var json = JsonSerializer.Serialize(state);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", STATE_STORAGE_KEY, json);
                
                _logger.LogDebug("Application state persisted to local storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting application state");
            }
        }
        
        /// <summary>
        /// Loads the application state from browser storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task LoadStateAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", STATE_STORAGE_KEY);
                
                if (!string.IsNullOrEmpty(json))
                {
                    var state = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (state.TryGetProperty("IsInitialized", out var isInitialized))
                    {
                        _isInitialized = isInitialized.GetBoolean();
                    }
                    
                    if (state.TryGetProperty("IsLocked", out var isLocked))
                    {
                        _isLocked = isLocked.GetBoolean();
                    }
                    
                    _logger.LogInformation("Application state loaded from local storage");
                }
                else
                {
                    _logger.LogInformation("No saved application state found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application state");
            }
        }
        
        /// <summary>
        /// Handles authentication state changed events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnAuthenticationStateChanged(object? sender, AuthenticationStateChangedEventArgs e)
        {
            SetAuthenticationState(e.IsAuthenticated, e.Username);
        }
        
        /// <summary>
        /// Raises the state changed event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        /// <param name="oldValue">The old value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        private void OnStateChanged(string propertyName, object? oldValue, object? newValue)
        {
            _logger.LogDebug("Application state changed: {PropertyName} from {OldValue} to {NewValue}", 
                propertyName, oldValue, newValue);
                
            StateChanged?.Invoke(this, new AppStateChangedEventArgs(propertyName, oldValue, newValue));
        }
    }
}
