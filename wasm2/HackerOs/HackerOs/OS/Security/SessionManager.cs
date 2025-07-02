using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using HackerOs.OS.User.Models;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Implementation of the ISessionManager interface for managing user sessions.
    /// </summary>
    public class SessionManager : ISessionManager
    {
        private readonly ITokenService _tokenService;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<SessionManager>? _logger;
        private readonly Dictionary<string, User.Models.UserSession> _sessions;
        private readonly object _lock = new();
        private string? _activeSessionId;
        private readonly TimeSpan _sessionTimeout;
        private const string SessionKeyPrefix = "hackeros_session_";
        private const string CurrentSessionKey = "hackeros_current_session";
        private const string SessionsKey = "hackeros_sessions";

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionManager"/> class.
        /// </summary>
        /// <param name="tokenService">The token service.</param>
        /// <param name="jsRuntime">The JavaScript runtime for browser storage access.</param>
        public SessionManager(ITokenService tokenService, IJSRuntime jsRuntime)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _sessions = new Dictionary<string, HackerOs.OS.User.Models.UserSession>();
            _sessionTimeout = TimeSpan.FromMinutes(15); // Default to 15 minutes
        }

        /// <summary>
        /// Event triggered when the session state changes.
        /// </summary>
        public event EventHandler<SessionChangedEventArgs>? SessionChanged;

        /// <summary>
        /// Initializes the session manager by loading existing sessions from LocalStorage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitializeAsync()
        {
            try
            {
                // Load sessions from LocalStorage
                var sessionsJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "hackerOs.sessions");
                if (!string.IsNullOrEmpty(sessionsJson))
                {
                    var sessionDtos = System.Text.Json.JsonSerializer.Deserialize<List<SessionDto>>(sessionsJson);
                    if (sessionDtos != null)
                    {
                        foreach (var dto in sessionDtos)
                        {
                            if (_tokenService.ValidateToken(dto.Token))
                            {
                                var user = _tokenService.GetUserFromToken(dto.Token);
                                if (user != null)
                                {
                                    var session = new UserSession
                                    {
                                        SessionId = dto.SessionId,
                                        User = user,
                                        Token = dto.Token,
                                        StartTime = dto.StartTime,
                                        LastActivity = dto.LastActivity,
                                        State = dto.State
                                    };

                                    _sessions[dto.SessionId] = session;
                                }
                            }
                        }
                    }
                }

                // Load active session ID from LocalStorage
                _activeSessionId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "hackerOs.activeSessionId");
                if (!string.IsNullOrEmpty(_activeSessionId) && !_sessions.ContainsKey(_activeSessionId))
                {
                    _activeSessionId = null;
                }

                // Clean up expired sessions
                await CleanupExpiredSessionsAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error initializing SessionManager: {ex.Message}");
                _sessions.Clear();
                _activeSessionId = null;
            }
        }

        /// <summary>
        /// Creates a new user session.
        /// </summary>
        /// <param name="user">The user for whom to create a session.</param>
        /// <returns>The newly created user session.</returns>
        public async Task<User.Models.UserSession> CreateSessionAsync(HackerOs.OS.User.User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            // Generate a new token for the user
            string token = _tokenService.GenerateToken(user);

            // Convert Security.User to User.User
            var userModel = new HackerOs.OS.User.User
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.Username, // Use username as fallback for full name
                IsActive = user.IsActive,
                HomeDirectory = user.HomeDirectory ?? $"/home/{user.Username}"
                // Set other properties as needed
            };

            // Create a new session with the User as HackerOs.OS.User.User
            var session = new HackerOs.OS.User.Models.UserSession
            {
                SessionId = Guid.NewGuid().ToString(),
                User = userModel,
                Token = token,
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                State = SessionState.Active
            };

            // Add the session to the dictionary
            _sessions[session.SessionId] = session;

            // Set this as the active session if no active session exists
            if (string.IsNullOrEmpty(_activeSessionId))
            {
                _activeSessionId = session.SessionId;
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "hackerOs.activeSessionId", _activeSessionId);
            }

            // Save sessions to LocalStorage
            await SaveSessionsAsync();

            // Raise session changed event
            OnSessionChanged(session.SessionId, SessionState.Active, session.User);

            return session;
        }

        /// <summary>
        /// Ends a user session with the specified ID.
        /// </summary>
        /// <param name="sessionId">The ID of the session to end.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task EndSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            if (!_sessions.TryGetValue(sessionId, out HackerOs.OS.User.Models.UserSession? session))
            {
                throw new ArgumentException($"Session with ID {sessionId} not found.", nameof(sessionId));
            }

            // Remove the session from the dictionary
            _sessions.Remove(sessionId);

            // If this was the active session, clear the active session ID
            if (sessionId == _activeSessionId)
            {
                _activeSessionId = null;
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "hackerOs.activeSessionId");
            }

            // Save sessions to LocalStorage
            await SaveSessionsAsync();

            // Raise session changed event
            OnSessionChanged(sessionId, SessionState.Terminated, session.User);
        }

        /// <summary>
        /// Gets the currently active user session.
        /// </summary>
        /// <returns>The active user session, or null if no session is active.</returns>
        public Task<User.Models.UserSession?> GetActiveSessionAsync()
        {
            if (string.IsNullOrEmpty(_activeSessionId) || !_sessions.TryGetValue(_activeSessionId, out HackerOs.OS.User.Models.UserSession? session))
            {
                return Task.FromResult<HackerOs.OS.User.Models.UserSession?>(null);
            }

            // Update last activity
            session.UpdateActivity();

            return Task.FromResult<HackerOs.OS.User.Models.UserSession?>(session);
        }

        /// <summary>
        /// Gets all active user sessions.
        /// </summary>
        /// <returns>A collection of all active user sessions.</returns>
        public Task<IEnumerable<User.Models.UserSession>> GetAllSessionsAsync()
        {
            return Task.FromResult<IEnumerable<HackerOs.OS.User.Models.UserSession>>(_sessions.Values);
        }

        /// <summary>
        /// Switches to a different user session.
        /// </summary>
        /// <param name="sessionId">The ID of the session to switch to.</param>
        /// <returns>True if the switch was successful, false otherwise.</returns>
        public async Task<bool> SwitchSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            if (!_sessions.TryGetValue(sessionId, out HackerOs.OS.User.Models.UserSession? session))
            {
                return false;
            }

            // Validate that the session is still valid
            if (!_tokenService.ValidateToken(session.Token))
            {
                // Session token is invalid, remove the session
                _sessions.Remove(sessionId);
                await SaveSessionsAsync();
                return false;
            }

            // Update the active session ID
            _activeSessionId = sessionId;
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "hackerOs.activeSessionId", _activeSessionId);

            // Update last activity
            session.UpdateActivity();
            session.State = SessionState.Active;
            await SaveSessionsAsync();

            // Raise session changed event
            OnSessionChanged(sessionId, SessionState.Active, session.User);

            return true;
        }

        /// <summary>
        /// Locks the current session, requiring re-authentication to unlock.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LockSessionAsync()
        {
            if (string.IsNullOrEmpty(_activeSessionId) || !_sessions.TryGetValue(_activeSessionId, out HackerOs.OS.User.Models.UserSession? session))
            {
                throw new InvalidOperationException("No active session to lock.");
            }

            // Change session state to locked
            session.State = SessionState.Locked;
            await SaveSessionsAsync();

            // Raise session changed event
            OnSessionChanged(_activeSessionId, SessionState.Locked, session.User);
        }

        /// <summary>
        /// Unlocks a locked session.
        /// </summary>
        /// <param name="password">The password to authenticate with.</param>
        /// <returns>True if the unlock was successful, false otherwise.</returns>
        public async Task<bool> UnlockSessionAsync(string password)
        {
            if (string.IsNullOrEmpty(_activeSessionId) || !_sessions.TryGetValue(_activeSessionId, out HackerOs.OS.User.Models.UserSession? session))
            {
                throw new InvalidOperationException("No active session to unlock.");
            }

            if (session.State != SessionState.Locked)
            {
                throw new InvalidOperationException("Session is not locked.");
            }

            // In a real implementation, we would verify the password here
            // For now, we just simulate success
            bool success = !string.IsNullOrEmpty(password);

            if (success)
            {
                // Change session state to active
                session.State = SessionState.Active;
                session.UpdateActivity();
                await SaveSessionsAsync();

                // Raise session changed event
                OnSessionChanged(_activeSessionId, SessionState.Active, session.User);
            }

            return success;
        }

        /// <summary>
        /// Cleans up expired sessions.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task CleanupExpiredSessionsAsync()
        {
            var expiredSessions = new List<string>();

            // Find expired sessions
            foreach (var kvp in _sessions)
            {
                string sessionId = kvp.Key;
                HackerOs.OS.User.Models.UserSession session = kvp.Value;

                // Check if session has timed out
                if (session.HasTimedOut(_sessionTimeout))
                {
                    expiredSessions.Add(sessionId);
                }
                // Check if token is no longer valid
                else if (!_tokenService.ValidateToken(session.Token))
                {
                    expiredSessions.Add(sessionId);
                }
            }

            // Remove expired sessions
            foreach (string sessionId in expiredSessions)
            {
                _sessions.Remove(sessionId);

                // If this was the active session, clear the active session ID
                if (sessionId == _activeSessionId)
                {
                    _activeSessionId = null;
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "hackerOs.activeSessionId");
                }
            }

            // Save sessions to LocalStorage if any were removed
            if (expiredSessions.Count > 0)
            {
                await SaveSessionsAsync();
            }
        }

        /// <summary>
        /// Saves sessions to LocalStorage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SaveSessionsAsync()
        {
            try
            {
                var sessionDtos = _sessions.Values.Select(s => new SessionDto
                {
                    SessionId = s.SessionId,
                    Token = s.Token,
                    StartTime = s.StartTime,
                    LastActivity = s.LastActivity,
                    State = s.State
                }).ToList();

                string sessionsJson = System.Text.Json.JsonSerializer.Serialize(sessionDtos);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "hackerOs.sessions", sessionsJson);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving sessions: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the SessionChanged event.
        /// </summary>
        /// <param name="sessionId">The ID of the session that changed.</param>
        /// <param name="state">The new state of the session.</param>
        /// <param name="user">The user associated with the session.</param>
        private void OnSessionChanged(string sessionId, SessionState state, HackerOs.OS.User.User? user)
        {
            SessionChanged?.Invoke(this, new SessionChangedEventArgs(sessionId, state, user));
        }

        /// <summary>
        /// DTO for serializing session information to LocalStorage.
        /// </summary>
        private class SessionDto
        {
            /// <summary>
            /// Gets or sets the session ID.
            /// </summary>
            public string SessionId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the authentication token.
            /// </summary>
            public string Token { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the start time of the session.
            /// </summary>
            public DateTime StartTime { get; set; }

            /// <summary>
            /// Gets or sets the last activity time of the session.
            /// </summary>
            public DateTime LastActivity { get; set; }

            /// <summary>
            /// Gets or sets the state of the session.
            /// </summary>
            public SessionState State { get; set; }
        }
    }
}
