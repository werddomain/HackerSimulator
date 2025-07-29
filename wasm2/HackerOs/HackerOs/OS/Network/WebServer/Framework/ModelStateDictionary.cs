using System.Collections.Generic;
using System.Linq;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Represents the state of a model during model binding and validation.
    /// </summary>
    public class ModelStateDictionary
    {
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        /// <summary>
        /// Gets a value indicating whether the model state is valid.
        /// </summary>
        public bool IsValid => _errors.Count == 0;

        /// <summary>
        /// Gets the keys in the dictionary.
        /// </summary>
        public IEnumerable<string> Keys => _errors.Keys;

        /// <summary>
        /// Gets the count of keys in the dictionary.
        /// </summary>
        public int Count => _errors.Count;

        /// <summary>
        /// Gets the error messages for the specified key.
        /// </summary>
        /// <param name="key">The key to get error messages for.</param>
        /// <returns>The error messages for the key.</returns>
        public IEnumerable<string> GetErrors(string key)
        {
            return _errors.TryGetValue(key, out var errors) ? errors : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets all error messages in the dictionary.
        /// </summary>
        /// <returns>All error messages.</returns>
        public IEnumerable<string> GetAllErrors()
        {
            return _errors.SelectMany(e => e.Value);
        }

        /// <summary>
        /// Adds a model error to the dictionary.
        /// </summary>
        /// <param name="key">The key for the error.</param>
        /// <param name="errorMessage">The error message.</param>
        public void AddModelError(string key, string errorMessage)
        {
            if (!_errors.TryGetValue(key, out var errors))
            {
                errors = new List<string>();
                _errors[key] = errors;
            }

            errors.Add(errorMessage);
        }

        /// <summary>
        /// Clears all errors from the dictionary.
        /// </summary>
        public void Clear()
        {
            _errors.Clear();
        }
    }
}
