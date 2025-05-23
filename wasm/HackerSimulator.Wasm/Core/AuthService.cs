using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HackerSimulator.Wasm.Core
{
    public class AuthService
    {
        private const string UsersKey = "hacker-os-users";
        private const string GroupsKey = "hacker-os-groups";

        private readonly IJSRuntime _js;
        private readonly FileSystemService _fs;

        private readonly Dictionary<int, UserRecord> _users = new();
        private readonly Dictionary<int, GroupRecord> _groups = new();
        private int _nextId = 1;

        public UserRecord? CurrentUser { get; private set; }
        public bool Initialized { get; private set; }
        public bool HasUsers => _users.Count > 0;
        public bool IsAuthenticated => CurrentUser != null;

        public event Action<UserRecord>? OnUserLogin;

        public AuthService(IJSRuntime js, FileSystemService fs)
        {
            _js = js;
            _fs = fs;
        }

        public async Task InitAsync()
        {
            if (Initialized) return;
            await LoadUsers();
            await LoadGroups();
            if (_users.Count > 0)
                _nextId = _users.Keys.Max() + 1;
            Initialized = true;
        }

        private async Task LoadUsers()
        {
            var json = await _js.InvokeAsync<string>("localStorage.getItem", UsersKey);
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<int, UserRecord>>(json);
                if (loaded != null)
                    foreach (var kv in loaded) _users[kv.Key] = kv.Value;
            }
        }

        private async Task LoadGroups()
        {
            var json = await _js.InvokeAsync<string>("localStorage.getItem", GroupsKey);
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<int, GroupRecord>>(json);
                if (loaded != null)
                    foreach (var kv in loaded) _groups[kv.Key] = kv.Value;
            }
            if (_groups.Count == 0)
            {
                _groups[1] = new GroupRecord { Id = 1, Name = "admin" };
                _groups[2] = new GroupRecord { Id = 2, Name = "users" };
                await SaveGroups();
            }
        }

        private Task SaveUsers()
        {
            var json = JsonSerializer.Serialize(_users);
            return _js.InvokeVoidAsync("localStorage.setItem", UsersKey, json).AsTask();
        }

        private Task SaveGroups()
        {
            var json = JsonSerializer.Serialize(_groups);
            return _js.InvokeVoidAsync("localStorage.setItem", GroupsKey, json).AsTask();
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
            OnUserLogin?.Invoke(user);
            return user;
        }

        public Task Logout()
        {
            CurrentUser = null;
            return Task.CompletedTask;
        }

        private static string HashPassword(string password, string salt)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + password));
            return Convert.ToBase64String(bytes);
        }

        private static bool ValidateTotp(string secret, string code)
        {
            // TODO: implement real TOTP validation
            return true;
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
