using System;
using System.Threading.Tasks;
using HackerOs.OS.Security;

namespace HackerOs.OS.Core.State
{
    /// <summary>
    /// Interface for application state management, providing a centralized way to track global application state.
    /// </summary>
    public interface IAppStateService
    {
        /// <summary>
        /// Gets whether the user is authenticated.
        /// </summary>
        bool IsAuthenticated { get; }
        
        /// <summary>
        /// Gets the current username, or null if not authenticated.
        /// </summary>
        string? CurrentUsername { get; }
        
        /// <summary>
        /// Gets whether the application is in the process of loading.
        /// </summary>
        bool IsLoading { get; }
        
        /// <summary>
        /// Gets or sets the current error message, if any.
        /// </summary>
        string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Gets or sets whether the application has completed initial setup.
        /// </summary>
        bool IsInitialized { get; set; }
        
        /// <summary>
        /// Gets or sets whether the system is locked.
        /// </summary>
        bool IsLocked { get; set; }
        
        /// <summary>
        /// Sets the authentication state.
        /// </summary>
        /// <param name="isAuthenticated">Whether the user is authenticated.</param>
        /// <param name="username">The username, or null if not authenticated.</param>
        void SetAuthenticationState(bool isAuthenticated, string? username = null);
        
        /// <summary>
        /// Sets the loading state.
        /// </summary>
        /// <param name="isLoading">Whether the application is loading.</param>
        void SetLoadingState(bool isLoading);
        
        /// <summary>
        /// Initializes the application state.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync();
        
        /// <summary>
        /// Persists the current state to browser storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PersistStateAsync();
        
        /// <summary>
        /// Event triggered when the application state changes.
        /// </summary>
        event EventHandler<AppStateChangedEventArgs> StateChanged;
    }
    
    /// <summary>
    /// Event arguments for application state changes.
    /// </summary>
    public class AppStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the property that changed.
        /// </summary>
        public string PropertyName { get; }
        
        /// <summary>
        /// Gets the old value of the property.
        /// </summary>
        public object? OldValue { get; }
        
        /// <summary>
        /// Gets the new value of the property.
        /// </summary>
        public object? NewValue { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AppStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        /// <param name="oldValue">The old value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        public AppStateChangedEventArgs(string propertyName, object? oldValue, object? newValue)
        {
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
