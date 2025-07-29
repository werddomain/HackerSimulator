using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProxyServer.Authentication;
using ProxyServer.Network.WebSockets;

namespace ProxyServer.Demo
{
    /// <summary>
    /// Demonstration program showing the authentication and authorization system integration.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("authentication-config.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            // Add authentication services
            services.AddAuthentication(configuration);

            // Add WebSocket server
            services.AddTransient<WebSocketServer>();

            var serviceProvider = services.BuildServiceProvider();

            // Get services
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            var webSocketServer = serviceProvider.GetRequiredService<WebSocketServer>();

            logger.LogInformation("=== ProxyServer Authentication Demo ===");

            // Demo 1: Test authentication with different API keys
            await DemoAuthentication(authService, logger);

            // Demo 2: Test permission checking
            await DemoPermissions(authService, logger);

            // Demo 3: Show WebSocket server integration
            await DemoWebSocketIntegration(webSocketServer, logger);

            logger.LogInformation("Demo completed. Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task DemoAuthentication(IAuthenticationService authService, ILogger logger)
        {
            logger.LogInformation("\n--- Authentication Demo ---");

            var testCases = new[]
            {
                ("admin-key-1234567890abcdef1234567890abcdef", "admin-client"),
                ("power-key-1234567890abcdef1234567890abcdef", "power-client"),
                ("user-key-1234567890abcdef1234567890abcdef", "user-client"),
                ("invalid-key", "invalid-client")
            };

            foreach (var (apiKey, clientId) in testCases)
            {
                var result = await authService.AuthenticateAsync(apiKey, clientId, "1.0.0");
                
                if (result.IsSuccess)
                {
                    logger.LogInformation("✓ Authentication SUCCESS - Client: {ClientId}, User: {UserId}, Role: {Role}, Permissions: [{Permissions}]",
                        clientId, result.UserId, result.Role, string.Join(", ", result.Permissions));
                }
                else
                {
                    logger.LogWarning("✗ Authentication FAILED - Client: {ClientId}, Error: {Error}",
                        clientId, result.ErrorMessage);
                }
            }
        }

        private static async Task DemoPermissions(IAuthenticationService authService, ILogger logger)
        {
            logger.LogInformation("\n--- Permission Demo ---");

            // First authenticate a user
            var authResult = await authService.AuthenticateAsync(
                "admin-key-1234567890abcdef1234567890abcdef", 
                "test-client", 
                "1.0.0");

            if (authResult.IsSuccess)
            {
                var sessionToken = authResult.SessionToken!;
                var permissions = new[] { "tcp_connect", "file_write", "admin_operation", "nonexistent_permission" };

                foreach (var permission in permissions)
                {
                    var hasPermission = await authService.HasPermissionAsync(sessionToken, permission);
                    var status = hasPermission ? "✓ ALLOWED" : "✗ DENIED";
                    logger.LogInformation("{Status} - Permission: {Permission}", status, permission);
                }

                // Test session validation
                var isValid = await authService.ValidateTokenAsync(sessionToken);
                logger.LogInformation("Session validation: {Status}", isValid ? "✓ VALID" : "✗ INVALID");

                // Revoke session
                await authService.RevokeTokenAsync(sessionToken);
                var isValidAfterRevoke = await authService.ValidateTokenAsync(sessionToken);
                logger.LogInformation("Session validation after revoke: {Status}", isValidAfterRevoke ? "✓ VALID" : "✗ INVALID");
            }
        }

        private static async Task DemoWebSocketIntegration(WebSocketServer webSocketServer, ILogger logger)
        {
            logger.LogInformation("\n--- WebSocket Server Integration Demo ---");
            
            try
            {
                logger.LogInformation("WebSocket server configured with authentication middleware");
                logger.LogInformation("- Authentication required for all operations except AUTHENTICATE messages");
                logger.LogInformation("- Authorization middleware checks permissions for each message type");
                logger.LogInformation("- Session management integrated with WebSocket client lifecycle");
                
                // Show client info (even though no clients are connected)
                var clientInfo = webSocketServer.GetClientInfo();
                logger.LogInformation("Current connected clients: {Count}", clientInfo.Count);
                logger.LogInformation("Current authenticated clients: {Count}", webSocketServer.AuthenticatedClientCount);
                
                logger.LogInformation("WebSocket server is ready to accept authenticated connections on any configured port");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WebSocket demo");
            }
        }
    }
}
