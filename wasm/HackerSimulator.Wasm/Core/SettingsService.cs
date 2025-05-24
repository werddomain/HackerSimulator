using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Provides access to per-user and machine-wide configuration files.
    /// Settings are stored as simple key/value pairs in JSON files located
    /// under /home/&lt;user&gt;/.config and /etc/&lt;app&gt;.
    /// </summary>
    public class SettingsService
    {
        private readonly FileSystemService _fs;
        private readonly AuthService _auth;

        public SettingsService(FileSystemService fs, AuthService auth)
        {
            _fs = fs;
            _auth = auth;
        }

        private static string UserConfigPath(string user, string app)
            => $"/home/{user}/.config/{app}.config";

        private static string MachineConfigPath(string app)
            => $"/etc/{app}/{app}.config";

        private async Task EnsureDir(string path)
        {
            var dir = path[..path.LastIndexOf('/')];
            if (!await _fs.Exists(dir))
                await _fs.CreateDirectory(dir);
        }

        /// <summary>
        /// Loads the merged settings for the specified application.
        /// User settings override machine settings when present.
        /// </summary>
        public async Task<Dictionary<string, string>> Load(string app)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var user = _auth.CurrentUser?.UserName;
            if (user != null)
            {
                var userPath = UserConfigPath(user, app);
                if (await _fs.Exists(userPath))
                {
                    var json = await _fs.ReadFile(userPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (data != null)
                    {
                        foreach (var kv in data)
                            result[kv.Key] = kv.Value;
                    }
                }
            }

            var machinePath = MachineConfigPath(app);
            if (await _fs.Exists(machinePath))
            {
                var json = await _fs.ReadFile(machinePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data != null)
                {
                    foreach (var kv in data)
                        if (!result.ContainsKey(kv.Key))
                            result[kv.Key] = kv.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Loads the raw user settings without merging.
        /// </summary>
        public async Task<Dictionary<string, string>> LoadUser(string app)
        {
            var user = _auth.CurrentUser?.UserName ?? "user";
            var path = UserConfigPath(user, app);
            if (!await _fs.Exists(path)) return new();
            var json = await _fs.ReadFile(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }

        /// <summary>
        /// Loads the raw machine settings.
        /// </summary>
        public async Task<Dictionary<string, string>> LoadMachine(string app)
        {
            var path = MachineConfigPath(app);
            if (!await _fs.Exists(path)) return new();
            var json = await _fs.ReadFile(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }

        /// <summary>
        /// Saves settings for the current user.
        /// </summary>
        public async Task SaveUser(string app, Dictionary<string, string> data)
        {
            var user = _auth.CurrentUser?.UserName ?? "user";
            var path = UserConfigPath(user, app);
            await EnsureDir(path);
            var json = JsonSerializer.Serialize(data);
            await _fs.WriteFile(path, json);
        }

        /// <summary>
        /// Saves machine-wide settings. Only admin users are allowed.
        /// </summary>
        public async Task SaveMachine(string app, Dictionary<string, string> data)
        {
            if (_auth.GetUserGroup() != 1)
                throw new InvalidOperationException("Only admin can modify machine settings.");
            var path = MachineConfigPath(app);
            await EnsureDir(path);
            var json = JsonSerializer.Serialize(data);
            await _fs.WriteFile(path, json);
        }
    }
}
