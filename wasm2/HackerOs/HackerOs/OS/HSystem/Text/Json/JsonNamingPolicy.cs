using System;

namespace HackerOs.OS.HSystem.Text.Json
{
    /// <summary>
    /// Provides a base class for custom property naming policies for JSON serialization.
    /// </summary>
    public abstract class JsonNamingPolicy
    {
        /// <summary>
        /// Gets a camelCase naming policy.
        /// </summary>
        public static JsonNamingPolicy CamelCase { get; } = new CamelCaseNamingPolicy();

        /// <summary>
        /// When overridden in a derived class, converts the specified name according to the policy.
        /// </summary>
        /// <param name="name">The name to convert.</param>
        /// <returns>The converted name.</returns>
        public abstract string ConvertName(string name);

        private sealed class CamelCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
                {
                    return name;
                }

                return char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
        }
    }
}
