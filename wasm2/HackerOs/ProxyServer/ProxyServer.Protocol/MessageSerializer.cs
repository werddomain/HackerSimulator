using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProxyServer.Protocol.Models;
using ProxyServer.Protocol.Models.Control;
using ProxyServer.Protocol.Models.FileSystem;
using ProxyServer.Protocol.Models.Network;

namespace ProxyServer.Protocol
{
    /// <summary>
    /// Handles serialization and deserialization of protocol messages.
    /// </summary>
    public class MessageSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        static MessageSerializer()
        {
            // Register message converters if needed
            SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// Serializes a message to JSON string.
        /// </summary>
        /// <param name="message">The message to serialize.</param>
        /// <returns>A JSON string representation of the message.</returns>
        public static string Serialize(MessageBase message)
        {
            return JsonSerializer.Serialize(message, message.GetType(), SerializerOptions);
        }

        /// <summary>
        /// Serializes a message to UTF-8 bytes.
        /// </summary>
        /// <param name="message">The message to serialize.</param>
        /// <returns>The UTF-8 encoded bytes of the JSON message.</returns>
        public static byte[] SerializeToBytes(MessageBase message)
        {
            return JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), SerializerOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to a message object.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized message object, or null if deserialization failed.</returns>
        public static MessageBase? Deserialize(string json)
        {
            try
            {
                // First deserialize to a basic object to get the type
                var baseMessage = JsonSerializer.Deserialize<MessageBase>(json, SerializerOptions);
                if (baseMessage == null)
                {
                    return null;
                }

                // Then deserialize to the specific message type based on the message type
                return baseMessage.Type switch
                {
                    MessageType.AUTHENTICATE => JsonSerializer.Deserialize<AuthenticationMessage>(json, SerializerOptions),
                    MessageType.HEARTBEAT => JsonSerializer.Deserialize<HeartbeatMessage>(json, SerializerOptions),
                    MessageType.ERROR_RESPONSE => JsonSerializer.Deserialize<ErrorMessage>(json, SerializerOptions),
                    MessageType.CONNECT_TCP => JsonSerializer.Deserialize<TcpConnectMessage>(json, SerializerOptions),
                    MessageType.SEND_DATA => JsonSerializer.Deserialize<DataMessage>(json, SerializerOptions),
                    MessageType.CLOSE_CONNECTION => JsonSerializer.Deserialize<CloseConnectionMessage>(json, SerializerOptions),
                    MessageType.CONNECTION_STATUS => JsonSerializer.Deserialize<ConnectionStatusMessage>(json, SerializerOptions),
                    MessageType.LIST_SHARES => JsonSerializer.Deserialize<ListSharesMessage>(json, SerializerOptions),
                    _ => baseMessage
                };
            }
            catch (JsonException ex)
            {
                // Log the error or handle it as needed
                Console.Error.WriteLine($"Error deserializing message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deserializes a JSON byte array to a message object.
        /// </summary>
        /// <param name="bytes">The UTF-8 encoded JSON bytes to deserialize.</param>
        /// <returns>The deserialized message object, or null if deserialization failed.</returns>
        public static MessageBase? DeserializeFromBytes(byte[] bytes)
        {
            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                // Log the error or handle it as needed
                Console.Error.WriteLine($"Error deserializing message from bytes: {ex.Message}");
                return null;
            }
        }
    }
}
