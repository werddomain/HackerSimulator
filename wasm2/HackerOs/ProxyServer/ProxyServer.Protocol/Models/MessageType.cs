namespace ProxyServer.Protocol.Models
{
    /// <summary>
    /// Defines the different types of messages that can be exchanged in the proxy protocol.
    /// </summary>
    public static class MessageType
    {
        // Network related message types
        public const string CONNECT_TCP = "CONNECT_TCP";
        public const string SEND_DATA = "SEND_DATA";
        public const string CLOSE_CONNECTION = "CLOSE_CONNECTION";
        public const string CONNECTION_STATUS = "CONNECTION_STATUS";
        
        // File system related message types
        public const string LIST_SHARES = "LIST_SHARES";
        public const string MOUNT_FOLDER = "MOUNT_FOLDER";
        public const string FILE_OPERATION = "FILE_OPERATION";
        public const string UNMOUNT_FOLDER = "UNMOUNT_FOLDER";
          // Control message types
        public const string AUTHENTICATE = "AUTHENTICATE";
        public const string AUTHENTICATE_RESPONSE = "AUTHENTICATE_RESPONSE";
        public const string HEARTBEAT = "HEARTBEAT";
        public const string ERROR_RESPONSE = "ERROR_RESPONSE";
    }
}
