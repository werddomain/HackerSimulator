using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HackerOs.OS.System.Text.Json
{
    /// <summary>
    /// Provides functionality for serializing and deserializing objects to and from JSON.
    /// This class wraps System.Text.Json functionality to provide JSON handling in the HackerOS environment.
    /// </summary>
    public static class JsonSerializer
    {
        private static readonly System.Text.Json.JsonSerializerOptions _defaultOptions = new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Converts the provided value into a JSON string.
        /// </summary>
        /// <param name="value">The value to convert to JSON.</param>
        /// <returns>A JSON string representation of the value.</returns>
        public static string Serialize(object value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value, _defaultOptions);
        }

        /// <summary>
        /// Converts the provided value into a JSON string using specified options.
        /// </summary>
        /// <param name="value">The value to convert to JSON.</param>
        /// <param name="options">Options to control serialization behavior.</param>
        /// <returns>A JSON string representation of the value.</returns>
        public static string Serialize(object value, JsonSerializerOptions options)
        {
            var wrappedOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase
                    ? System.Text.Json.JsonNamingPolicy.CamelCase
                    : null,
                WriteIndented = options.WriteIndented,
                IgnoreNullValues = options.IgnoreNullValues,
                PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive
            };

            return System.Text.Json.JsonSerializer.Serialize(value, wrappedOptions);
        }

        /// <summary>
        /// Parses the text representing a single JSON value into an instance of the type specified by a generic type parameter.
        /// </summary>
        /// <typeparam name="T">The target type of the JSON value.</typeparam>
        /// <param name="json">The JSON text to parse.</param>
        /// <returns>A deserialized instance of type T.</returns>
        public static T Deserialize<T>(string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, _defaultOptions);
        }

        /// <summary>
        /// Parses the text representing a single JSON value into an instance of the type specified by a generic type parameter.
        /// </summary>
        /// <typeparam name="T">The target type of the JSON value.</typeparam>
        /// <param name="json">The JSON text to parse.</param>
        /// <param name="options">Options to control deserialization behavior.</param>
        /// <returns>A deserialized instance of type T.</returns>
        public static T Deserialize<T>(string json, JsonSerializerOptions options)
        {
            var wrappedOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase
                    ? System.Text.Json.JsonNamingPolicy.CamelCase
                    : null,
                WriteIndented = options.WriteIndented,
                IgnoreNullValues = options.IgnoreNullValues,
                PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive
            };

            return System.Text.Json.JsonSerializer.Deserialize<T>(json, wrappedOptions);
        }
    }

    /// <summary>
    /// Provides options to be used with JsonSerializer.
    /// </summary>
    public class JsonSerializerOptions
    {
        /// <summary>
        /// Gets or sets the policy used to convert property names to JSON property names.
        /// </summary>
        public JsonNamingPolicy PropertyNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;

        /// <summary>
        /// Gets or sets a value that indicates whether JSON should use pretty printing.
        /// </summary>
        public bool WriteIndented { get; set; } = true;

        /// <summary>
        /// Gets or sets a value that indicates whether null values should be ignored during serialization.
        /// </summary>
        public bool IgnoreNullValues { get; set; } = true;

        /// <summary>
        /// Gets or sets a value that determines whether property names are matched case-insensitively during deserialization.
        /// </summary>
        public bool PropertyNameCaseInsensitive { get; set; } = true;
    }

    /// <summary>
    /// Provides naming policies for JSON property names.
    /// </summary>
    public enum JsonNamingPolicy
    {
        /// <summary>
        /// No naming policy (keep property names as-is).
        /// </summary>
        None,

        /// <summary>
        /// Use camelCase for property names.
        /// </summary>
        CamelCase
    }
}
