using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace HackerSimulator.Wasm.Core
{
    public class AuthService
    {
        private const string UsersKey = "users";
        private const string GroupsKey = "groups";

        private readonly DatabaseService _db;
        private readonly FileSystemService _fs;

        private readonly Dictionary<int, UserRecord> _users = new();
        private readonly Dictionary<int, GroupRecord> _groups = new();
        private int _nextId = 1;

        public UserRecord? CurrentUser { get; private set; }
        public bool Initialized { get; private set; }
        public bool HasUsers => _users.Count > 0;
        public bool IsAuthenticated => CurrentUser != null;

        public event Action<UserRecord>? OnUserLogin;
        public event Action OnAuthInitialised;

        public AuthService(DatabaseService db, FileSystemService fs)
        {
            _db = db;
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
            OnAuthInitialised?.Invoke();
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
