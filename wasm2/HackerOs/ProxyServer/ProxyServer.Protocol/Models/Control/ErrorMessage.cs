using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Control
{
    /// <summary>
    /// Message for reporting errors.
    /// </summary>
    public class ErrorMessage : MessageBase
    {
        /// <summary>
        /// Error severity levels.
        /// </summary>
        public enum ErrorSeverity
        {
            /// <summary>
            /// Information level error (not critical).
            /// </summary>
            Info,

            /// <summary>
            /// Warning level error (potentially problematic).
            /// </summary>
            Warning,

            /// <summary>
            /// Error level (operation failed).
            /// </summary>
            Error,

            /// <summary>
            /// Critical error (system stability affected).
            /// </summary>
            Critical
        }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error code (alias for Code property for backward compatibility).
        /// </summary>
        [JsonIgnore]
        public string ErrorCode
        {
            get => Code;
            set => Code = value;
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error severity.
        /// </summary>
        [JsonPropertyName("severity")]
        public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;

        /// <summary>
        /// Gets or sets the ID of the original message that caused the error.
        /// </summary>
        [JsonPropertyName("relatedMessageId")]
        public string? RelatedMessageId { get; set; }

        /// <summary>
        /// Gets or sets additional error details.
        /// </summary>
        [JsonPropertyName("details")]
        public Dictionary<string, object>? Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMessage"/> class.
        /// </summary>
        public ErrorMessage() : base(MessageType.ERROR_RESPONSE)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMessage"/> class.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="relatedMessageId">The ID of the message that triggered this error.</param>
        public ErrorMessage(
            string code, 
            string message, 
            ErrorSeverity severity = ErrorSeverity.Error,
            string? relatedMessageId = null) : base(MessageType.ERROR_RESPONSE)
        {
            Code = code;
            Message = message;
            Severity = severity;
            RelatedMessageId = relatedMessageId;
        }

        /// <summary>
        /// Creates an error message from an exception.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <param name="relatedMessageId">The ID of the message that triggered this error.</param>
        /// <returns>A new error message.</returns>
        public static ErrorMessage FromException(Exception exception, string? relatedMessageId = null)
        {
            var errorMessage = new ErrorMessage(
                "SYSTEM_ERROR",
                exception.Message,
                ErrorSeverity.Error,
                relatedMessageId
            );

            errorMessage.Details = new Dictionary<string, object>
            {
                ["exceptionType"] = exception.GetType().Name,
                ["stackTrace"] = exception.StackTrace ?? string.Empty
            };

            return errorMessage;
        }

        /// <summary>
        /// Creates a failure error message.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="code">The error code.</param>
        /// <returns>A new error message indicating failure.</returns>
        public static ErrorMessage Failure(string message, string code = "OPERATION_FAILED")
        {
            return new ErrorMessage(code, message, ErrorSeverity.Error);
        }
    }
}
