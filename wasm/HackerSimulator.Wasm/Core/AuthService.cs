using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;


namespace HackerSimulator.Wasm.Core
{
    public class AuthService
    {
        private const string UsersKey = "users";
        private const string GroupsKey = "groups";
        private const string AuthTokenKey = "auth_token";
        private const string SettingsApp = "authsettingsapp";
        private const string TokenExpirationSetting = "tokenExpirationMinutes";
        private const string TokenRefreshThresholdSetting = "tokenRefreshThresholdMinutes";

        private int _tokenExpirationMinutes = 60; // Default 1 hour token expiration
        private int _tokenRefreshThresholdMinutes = 10; // Refresh token if less than 10 minutes remaining

        private readonly DatabaseService _db;
        private readonly FileSystemService _fs;
        private readonly IJSRuntime? _jsRuntime;

        private readonly Dictionary<int, UserRecord> _users = new();
        private readonly Dictionary<int, GroupRecord> _groups = new();
        private int _nextId = 1;
        private DateTime _lastTokenRefresh = DateTime.MinValue;

        public UserRecord? CurrentUser { get; private set; }
        public bool Initialized { get; private set; }
        public bool HasUsers => _users.Count > 0;
        public bool IsAuthenticated => CurrentUser != null;
        
        public event EventHandler<UserRecord>? OnUserLogin;
        public event EventHandler? OnAuthInitialised;

        private readonly IServiceProvider? _provider;

        public AuthService(DatabaseService db, FileSystemService fs, IJSRuntime? jsRuntime = null, IServiceProvider? provider = null)
        {
            _db = db;
            _fs = fs;
            _jsRuntime = jsRuntime;
            _provider = provider;
        }

        public async Task InitAsync()
        {
            if (Initialized) return;
            await LoadUsers();
            await LoadGroups();
            if (_users.Count > 0)
                _nextId = _users.Keys.Max() + 1;
            
            // Try to restore session from stored token
            await TryAutoLoginFromStoredToken();
            if (!Initialized)
            {
                Initialized = true;
                OnAuthInitialised?.Invoke(this, EventArgs.Empty);
            }
           
        }

        private async Task TryAutoLoginFromStoredToken()
        {
            var token = await GetTokenFromStorage();
            if (!string.IsNullOrEmpty(token) && ValidateToken(token, out var user) && user != null)
            {
                CurrentUser = user;
                await LoadSessionSettingsAsync();
                _lastTokenRefresh = DateTime.UtcNow;
                
                // Register activity listeners for automatic token refresh
                await RegisterActivityListeners();
                
                OnUserLogin?.Invoke(this, user);
            }
        }

        private async Task LoadUsers()
        {
            await _db.InitTable<UserRecord>(UsersKey, 1, null);
            var all = await _db.GetAll<UserRecord>(UsersKey);
            foreach (var user in all)
                _users[user.Id] = user;
        }

        private async Task LoadGroups()
        {
            await _db.InitTable<GroupRecord>(GroupsKey, 1, null);
            var all = await _db.GetAll<GroupRecord>(GroupsKey);
            foreach (var grp in all)
                _groups[grp.Id] = grp;
            if (_groups.Count == 0)
            {
                _groups[1] = new GroupRecord { Id = 1, Name = "admin" };
                _groups[2] = new GroupRecord { Id = 2, Name = "users" };
                await SaveGroups();
            }
        }

        private async Task SaveUsers()
        {
            await _db.Clear(UsersKey);
            foreach (var u in _users.Values)
                await _db.Set(UsersKey, u.Id.ToString(), u);
        }

        private async Task SaveGroups()
        {
            await _db.Clear(GroupsKey);
            foreach (var g in _groups.Values)
                await _db.Set(GroupsKey, g.Id.ToString(), g);
        }

        private async Task LoadSessionSettingsAsync()
        {
            if (_provider == null || CurrentUser == null) return;

            var settingsService = _provider.GetService<SettingsService>();
            if (settingsService == null) return;

            var settings = await settingsService.Load(SettingsApp);
            if (settings.TryGetValue(TokenExpirationSetting, out var expStr) && int.TryParse(expStr, out var exp))
                _tokenExpirationMinutes = exp;
            if (settings.TryGetValue(TokenRefreshThresholdSetting, out var thrStr) && int.TryParse(thrStr, out var thr))
                _tokenRefreshThresholdMinutes = thr;
        }

        public IEnumerable<GroupRecord> GetGroups() => _groups.Values;

        public int? GetUserId() => CurrentUser?.Id;
        public int? GetUserGroup() => CurrentUser?.GroupId;

        public async Task<UserRecord> CreateUser(string username, string password, int groupId)
        {
            var salt = Guid.NewGuid().ToString("N");
            var hash = HashPassword(password, salt);
            var user = new UserRecord
            {
                Id = _nextId++,
                UserName = username,
                PasswordHash = hash,
                Salt = salt,
                GroupId = groupId
            };
            _users[user.Id] = user;
            await SaveUsers();

            var home = $"/home/{username}";
            if (!await _fs.Exists(home))
                await _fs.CreateDirectory(home);
            return user;
        }

        public async Task<UserRecord?> Login(string username, string password, string? totp = null)
        {
            await InitAsync();
            var user = _users.Values.FirstOrDefault(u => u.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null) return null;
            if (user.PasswordHash != HashPassword(password, user.Salt))
                return null;
            if (!string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                if (string.IsNullOrEmpty(totp) || !ValidateTotp(user.TwoFactorSecret, totp))
                    return null;
            }
            
            CurrentUser = user;

            await LoadSessionSettingsAsync();

            // Generate and save authentication token
            var token = GenerateToken(user);
            await SaveTokenToStorage(token);
            _lastTokenRefresh = DateTime.UtcNow;
            
            // Register activity listeners for automatic token refresh
            await RegisterActivityListeners();

            OnUserLogin?.Invoke(this, user);
            return user;
        }

        public async Task Logout()
        {
            CurrentUser = null;
            // Remove token from storage
            await RemoveTokenFromStorage();
        }

        private static string HashPassword(string password, string salt)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + password));
            return Convert.ToBase64String(bytes);
        }        // Method to refresh the auth token
        public async Task<bool> RefreshAuthToken()
        {
            if (CurrentUser == null)
                return false;
            
            var token = GenerateToken(CurrentUser!);
            await SaveTokenToStorage(token);
            _lastTokenRefresh = DateTime.UtcNow;
            return true;
        }

        // Called when user activity is detected
        public async Task NotifyUserActivity()
        {
            if (CurrentUser == null)
                return;

            await RefreshTokenIfNeeded();
        }

        // Only refresh token if needed based on time threshold
        private async Task<bool> RefreshTokenIfNeeded()
        {
            // If not authenticated, nothing to do
            if (CurrentUser == null)
                return false;

            // If token was refreshed recently, skip refresh
            if ((DateTime.UtcNow - _lastTokenRefresh).TotalMinutes < _tokenRefreshThresholdMinutes)
                return false;

            // Get current token and check if it needs refresh
            var token = await GetTokenFromStorage();
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                var tokenBytes = Convert.FromBase64String(token);
                var tokenJson = Encoding.UTF8.GetString(tokenBytes);
                var authToken = JsonSerializer.Deserialize<AuthToken>(tokenJson);

                if (authToken == null)
                    return false;

                // If token is going to expire soon, refresh it
                var minutesRemaining = (authToken.ExpiresAt - DateTime.UtcNow).TotalMinutes;
                if (minutesRemaining < _tokenRefreshThresholdMinutes)
                {
                    return await RefreshAuthToken();
                }
            }
            catch
            {
                // If token is invalid, create a new one
                return await RefreshAuthToken();
            }

            return false;
        }

        // Register activity listeners in the browser
        public async Task RegisterActivityListeners()
        {
            if (_jsRuntime == null || CurrentUser == null)
                return;

            try
            {
                // Use null-forgiving operator since we've already checked _jsRuntime != null
                await _jsRuntime!.InvokeVoidAsync("authActivityTracker.registerActivityListeners", 
                    DotNetObjectReference.Create(this));
            }
            catch (Exception)
            {
                // Fail silently if JavaScript interop fails
            }
        }

        // Method that will be called from JavaScript when activity is detected
        [JSInvokable("OnUserActivity")]
        public async Task OnUserActivityFromJS()
        {
            await NotifyUserActivity();
        }

        private static bool ValidateTotp(string secret, string code)
        {
            // TODO: implement real TOTP validation
            return true;
        }

        // Token model for JWT
        private class AuthToken
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }        // JWT token generation and validation methods
        private string GenerateToken(UserRecord user)
        {
            var token = new AuthToken
            {
                UserId = user.Id,
                UserName = user.UserName,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_tokenExpirationMinutes)
            };

            var tokenJson = JsonSerializer.Serialize(token);
            var tokenBytes = Encoding.UTF8.GetBytes(tokenJson);
            // Simple "JWT" token for simulation purposes
            return Convert.ToBase64String(tokenBytes);
        }

        private async Task SaveTokenToStorage(string token)
        {
            if (_jsRuntime == null) return;
            
            try
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", AuthTokenKey, token);
            }
            catch (Exception)
            {
                // Fail silently if sessionStorage isn't available
            }
        }

        private async Task<string?> GetTokenFromStorage()
        {
            if (_jsRuntime == null) return null;
            
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", AuthTokenKey);
            }
            catch (Exception)
            {
                // Fail silently if sessionStorage isn't available
                return null;
            }
        }

        private async Task RemoveTokenFromStorage()
        {
            if (_jsRuntime == null) return;
            
            try
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", AuthTokenKey);
            }
            catch (Exception)
            {
                // Fail silently if sessionStorage isn't available
            }
        }        private bool ValidateToken(string token, out UserRecord? user)
        {
            user = null;
            try
            {
                var tokenBytes = Convert.FromBase64String(token);
                var tokenJson = Encoding.UTF8.GetString(tokenBytes);
                var authToken = JsonSerializer.Deserialize<AuthToken>(tokenJson);

                if (authToken == null || authToken.ExpiresAt < DateTime.UtcNow)
                    return false;

                if (!_users.TryGetValue(authToken.UserId, out var foundUser))
                    return false;

                user = foundUser;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public record UserRecord
        {
            public int Id { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string PasswordHash { get; set; } = string.Empty;
            public string Salt { get; set; } = string.Empty;
            public int GroupId { get; set; }
            public string? TwoFactorSecret { get; set; }
        }

        public record GroupRecord
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
