using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using static HackerSimulator.Wasm.Core.AuthService;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Runs startup tasks once the UI has rendered for the first time.
    /// </summary>
    public class AutoRunService
    {
        private readonly IServiceProvider _services;
        private bool _started;
        private AuthService? _auth;

        public AutoRunService(IServiceProvider services)
        {
            _services = services;
        }

        /// <summary>
        /// Executes the startup sequence a single time.
        /// </summary>
        public async Task StartAsync()
        {
            if (_started)
                return;

            _started = true;
            var fs = _services.GetRequiredService<FileSystemService>();
            await fs.InitAsync();
 var ft = _services.GetRequiredService<FileTypeService>();
            ft.RegisterFromAttributes();
          

            _auth = _services.GetRequiredService<AuthService>();
            await _auth.InitAsync();
            _auth.OnUserLogin += OnUserLogin;
        }

        private void OnUserLogin(AuthService.UserRecord user)
        {
            // Placeholder for tasks after user login
        }
    }
}
