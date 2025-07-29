# Authentication and Authorization Layer

This document describes the implementation of Task 2.5: Authentication and Authorization Layer for the ProxyServer application.

## Overview

The authentication and authorization system provides secure access control for WebSocket connections, implementing:

- **API Key Authentication**: Secure authentication using configurable API keys
- **Session Management**: Token-based sessions with automatic cleanup
- **Role-Based Permissions**: Hierarchical permission system (admin, power_user, user)
- **Authorization Middleware**: Message-level authorization checks
- **WebSocket Integration**: Seamless integration with WebSocket server

## Architecture

### Core Components

1. **IAuthenticationService**: Main authentication service interface
2. **AuthenticationService**: Default implementation with session management
3. **AuthorizationMiddleware**: Middleware for checking permissions before message processing
4. **AuthenticationConfiguration**: Configuration model for API keys and settings
5. **WebSocketServer Integration**: Enhanced WebSocket server with authentication support

### Authentication Flow

```
Client Request -> API Key Validation -> Session Creation -> Token Response
     ↓
WebSocket Messages -> Authorization Check -> Message Processing
```

## Configuration

### appsettings.json Example

```json
{
  "Authentication": {
    "ApiKeys": [
      {
        "Key": "admin-key-1234567890abcdef1234567890abcdef",
        "UserId": "admin",
        "Role": "admin",
        "DisplayName": "Administrator",
        "IsEnabled": true
      },
      {
        "Key": "user-key-1234567890abcdef1234567890abcdef",
        "UserId": "regular-user",
        "Role": "user",
        "DisplayName": "Regular User",
        "IsEnabled": true,
        "CustomPermissions": ["tcp_connect", "file_read"]
      }
    ],
    "SessionTimeoutMinutes": 60,
    "SessionCleanupIntervalMinutes": 10,
    "MaxSessionsPerClient": 5,
    "RequireStrongApiKeys": true,
    "MinApiKeyLength": 32
  }
}
```

### Dependency Injection Setup

```csharp
// Program.cs or Startup.cs
services.AddAuthentication(configuration);

// Or with custom configuration
services.AddAuthentication(config =>
{
    config.SessionTimeoutMinutes = 30;
    config.MaxSessionsPerClient = 3;
    // ... other settings
});

// Or use defaults for development
services.AddDefaultAuthentication();
```

## Usage

### 1. WebSocket Authentication

Clients must authenticate before performing any operations:

```json
// Authentication Request
{
  "type": "AUTHENTICATE",
  "apiKey": "admin-key-1234567890abcdef1234567890abcdef",
  "clientVersion": "1.0.0"
}

// Authentication Response (Success)
{
  "type": "AUTHENTICATE_RESPONSE",
  "success": true,
  "sessionToken": "base64-encoded-session-token",
  "expiresAt": "2024-01-01T12:00:00Z",
  "userInfo": {
    "userId": "admin",
    "role": "admin",
    "permissions": ["*"]
  }
}
```

### 2. Message Authorization

After authentication, all messages are automatically checked for authorization:

```json
// Example: TCP Connection (requires 'tcp_connect' permission)
{
  "type": "CONNECT_TCP",
  "host": "example.com",
  "port": 80
}

// If unauthorized, receives error response:
{
  "type": "ERROR_RESPONSE",
  "success": false,
  "errorCode": "INSUFFICIENT_PERMISSIONS",
  "errorMessage": "Operation requires permission: tcp_connect"
}
```

### 3. Permission System

#### Built-in Roles

- **admin**: All permissions (`*`)
- **power_user**: tcp_connect, tcp_send, file_read, file_write, file_list
- **user**: tcp_connect, tcp_send, file_read, file_list

#### Permission Mapping

- `CONNECT_TCP` → `tcp_connect`
- `SEND_DATA` → `tcp_send` 
- `FILE_OPERATION` (read) → `file_read`
- `FILE_OPERATION` (write) → `file_write`
- `LIST_SHARES` → `file_list`

### 4. Session Management

```csharp
// Validate session
bool isValid = await authService.ValidateTokenAsync(sessionToken);

// Check specific permission
bool canConnect = await authService.HasPermissionAsync(sessionToken, "tcp_connect");

// Revoke session
await authService.RevokeTokenAsync(sessionToken);

// Get session info
var session = await authService.GetSessionInfoAsync(sessionToken);
```

### 5. Client Monitoring

```csharp
// Get client information
var clients = webSocketServer.GetClientInfo();
var authenticatedCount = webSocketServer.AuthenticatedClientCount;

// Revoke specific client
await webSocketServer.RevokeClientAuthenticationAsync(clientId);

// Broadcast to authorized clients only
await webSocketServer.BroadcastToAuthorizedClientsAsync(message, "admin_permission");
```

## Security Features

1. **Secure Token Generation**: Cryptographically secure session tokens
2. **Automatic Session Cleanup**: Expired sessions are automatically removed
3. **Session Limits**: Configurable maximum sessions per client
4. **API Key Validation**: Configurable key strength requirements
5. **Permission Isolation**: Fine-grained access control
6. **Audit Logging**: Comprehensive authentication and authorization logging

## Demo Application

Run the included demo to see the authentication system in action:

```bash
cd ProxyServer.Demo
dotnet run
```

The demo shows:
- Authentication with different API keys
- Permission checking for various operations
- Session validation and revocation
- WebSocket server integration

## Testing

### Manual Testing with WebSocket Client

1. Connect to WebSocket server
2. Send authentication message with valid API key
3. Verify successful authentication response
4. Send operation messages and verify authorization
5. Test with invalid API key and verify rejection

### Unit Testing

Create tests for:
- Authentication service methods
- Authorization middleware
- Session management
- Permission checking
- Configuration loading

## Production Considerations

1. **Secure API Key Storage**: Use secure configuration providers
2. **Token Storage**: Consider Redis for distributed scenarios
3. **Rate Limiting**: Add authentication attempt rate limiting
4. **Audit Logging**: Enhanced security event logging
5. **Key Rotation**: Implement API key rotation strategies
6. **HTTPS**: Always use secure WebSocket connections (WSS)

## Error Handling

The system provides detailed error responses for:

- Invalid API keys
- Expired sessions
- Insufficient permissions
- Session limit exceeded
- Configuration errors

All errors are logged with appropriate severity levels and include correlation IDs for debugging.

## Future Enhancements

- JWT token support
- OAuth2/OpenID Connect integration
- Multi-factor authentication
- Advanced rate limiting
- Geographic access restrictions
- API key expiration and rotation
