using ProxyServer.Network.WebSockets;
using ProxyServer.Protocol.Models;
using ProxyServer.Protocol.Models.Control;
using ProxyServer.Authentication;
using Microsoft.Extensions.Logging;

namespace ProxyServer.Network
{
    /// <summary>
    /// Middleware for handling authorization checks on incoming messages.
    /// </summary>
    public class AuthorizationMiddleware
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthorizationMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationMiddleware"/> class.
        /// </summary>
        /// <param name="authService">The authentication service.</param>
        /// <param name="logger">The logger.</param>
        public AuthorizationMiddleware(IAuthenticationService authService, ILogger<AuthorizationMiddleware> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Checks if a client is authorized to perform the requested operation.
        /// </summary>
        /// <param name="message">The message being processed.</param>
        /// <param name="client">The WebSocket client.</param>
        /// <returns>Authorization result containing success status and error message if applicable.</returns>
        public async Task<AuthorizationResult> CheckAuthorizationAsync(MessageBase message, WebSocketClient client)
        {
            try
            {
                // Authentication messages don't require authorization
                if (message.Type == Protocol.Models.MessageType.AUTHENTICATE)
                {
                    return AuthorizationResult.Successful();
                }

                // Check if client is authenticated
                if (!client.IsAuthenticated || string.IsNullOrEmpty(client.AuthToken))
                {
                    _logger.LogWarning("Unauthorized access attempt from client {ClientId} for message type {MessageType}",
                        client.ClientId, message.Type);
                    return AuthorizationResult.Failure("Authentication required");
                }

                // Validate session token
                if (!await _authService.ValidateTokenAsync(client.AuthToken))
                {
                    _logger.LogWarning("Invalid or expired token used by client {ClientId}",
                        client.ClientId);
                    
                    // Mark client as unauthenticated
                    client.IsAuthenticated = false;
                    client.AuthToken = null;
                    
                    return AuthorizationResult.Failure("Session expired or invalid");
                }

                // Check specific operation permissions
                var permission = GetRequiredPermission(message.Type);
                if (!string.IsNullOrEmpty(permission))
                {
                    var hasPermission = await _authService.HasPermissionAsync(client.AuthToken, permission);
                    if (!hasPermission)
                    {
                        _logger.LogWarning("Client {ClientId} denied access to operation {Operation} (missing permission: {Permission})",
                            client.ClientId, message.Type, permission);
                        return AuthorizationResult.Failure($"Insufficient permissions for operation: {message.Type}");
                    }
                }

                _logger.LogDebug("Client {ClientId} authorized for operation {Operation}",
                    client.ClientId, message.Type);

                return AuthorizationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authorization check for client {ClientId}, message type {MessageType}",
                    client.ClientId, message.Type);
                return AuthorizationResult.Failure("Authorization check failed");
            }
        }

        /// <summary>
        /// Gets the required permission for a message type.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <returns>The required permission, or null if no specific permission is required.</returns>
        private string? GetRequiredPermission(string messageType)
        {
            return messageType switch
            {
                Protocol.Models.MessageType.CONNECT_TCP => "tcp_connect",
                Protocol.Models.MessageType.SEND_DATA => "tcp_send",
                Protocol.Models.MessageType.CLOSE_CONNECTION => "tcp_close",
                Protocol.Models.MessageType.LIST_SHARES => "file_list",
                Protocol.Models.MessageType.MOUNT_FOLDER => "file_mount",
                Protocol.Models.MessageType.FILE_OPERATION => "file_operation",
                Protocol.Models.MessageType.UNMOUNT_FOLDER => "file_unmount",
                Protocol.Models.MessageType.HEARTBEAT => null, // No permission required for heartbeat
                _ => "unknown_operation" // Require specific permission for unknown operations
            };
        }

        /// <summary>
        /// Creates an authentication required error message.
        /// </summary>
        /// <param name="reason">The reason for the authentication requirement.</param>
        /// <returns>An error message indicating authentication is required.</returns>
        public static ErrorMessage CreateAuthenticationRequiredError(string reason = "Authentication required")
        {            return new ErrorMessage
            {
                ErrorCode = "AUTH_REQUIRED",
                Message = reason,
                Timestamp = DateTime.UtcNow.ToString("o")
            };
        }

        /// <summary>
        /// Creates an authorization denied error message.
        /// </summary>
        /// <param name="reason">The reason for the authorization denial.</param>
        /// <returns>An error message indicating authorization was denied.</returns>
        public static ErrorMessage CreateAuthorizationDeniedError(string reason = "Insufficient permissions")
        {            return new ErrorMessage
            {
                ErrorCode = "AUTH_DENIED",
                Message = reason,
                Timestamp = DateTime.UtcNow.ToString("o")
            };
        }
    }

    /// <summary>
    /// Result of an authorization check.
    /// </summary>
    public class AuthorizationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether authorization was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if authorization failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Creates a successful authorization result.
        /// </summary>
        /// <returns>A successful authorization result.</returns>
        public static AuthorizationResult Successful()
        {
            return new AuthorizationResult { Success = true };
        }

        /// <summary>
        /// Creates a failed authorization result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed authorization result.</returns>
        public static AuthorizationResult Failure(string errorMessage)
        {
            return new AuthorizationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
