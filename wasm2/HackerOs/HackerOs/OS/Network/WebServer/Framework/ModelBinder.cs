using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Provides model binding capabilities for web framework.
    /// </summary>
    public class ModelBinder
    {
        /// <summary>
        /// Binds the HTTP request data to a model object.
        /// </summary>
        /// <typeparam name="T">The type of model to bind to.</typeparam>
        /// <param name="context">The HTTP context containing the request data.</param>
        /// <returns>A new instance of T with properties populated from the request.</returns>
        public static T BindModel<T>(HttpContext context) where T : new()
        {
            return (T)BindModel(typeof(T), context);
        }

        /// <summary>
        /// Binds the HTTP request data to a model object.
        /// </summary>
        /// <param name="modelType">The type of model to bind to.</param>
        /// <param name="context">The HTTP context containing the request data.</param>
        /// <returns>A new instance of the model type with properties populated from the request.</returns>
        public static object BindModel(Type modelType, HttpContext context)
        {
            // Create a new instance of the model
            var model = Activator.CreateInstance(modelType);
            
            // Get all properties that can be set
            var properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite);
            
            // Source dictionaries for binding values
            var sources = new List<Dictionary<string, string>>
            {
                context.Request.RouteData.ToDictionary(k => k.Key, k => k.Value?.ToString() ?? string.Empty),
                new Dictionary<string, string>(context.Request.QueryParameters),
                new Dictionary<string, string>(context.Request.Form)
            };
            
            // Try to bind each property
            foreach (var property in properties)
            {
                // Look for the property in each source
                foreach (var source in sources)
                {
                    if (TryBindProperty(property, model, source))
                    {
                        // Property bound successfully, move to next property
                        break;
                    }
                }
            }
            
            return model;
        }

        /// <summary>
        /// Tries to bind a property from a value source.
        /// </summary>
        private static bool TryBindProperty(PropertyInfo property, object model, Dictionary<string, string> source)
        {
            // Try to find the property value in the source
            if (source.TryGetValue(property.Name, out var value))
            {
                try
                {
                    // Convert the string value to the property type
                    var convertedValue = ConvertValue(value, property.PropertyType);
                    
                    // Set the property value
                    property.SetValue(model, convertedValue);
                    return true;
                }
                catch
                {
                    // Conversion failed, continue to next source
                }
            }
            
            return false;
        }

        /// <summary>
        /// Converts a string value to the specified type.
        /// </summary>
        private static object ConvertValue(string value, Type targetType)
        {
            if (string.IsNullOrEmpty(value))
            {
                return GetDefaultValue(targetType);
            }
            
            if (targetType == typeof(string))
            {
                return value;
            }
            
            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return int.TryParse(value, out var intValue) ? intValue : GetDefaultValue(targetType);
            }
            
            if (targetType == typeof(long) || targetType == typeof(long?))
            {
                return long.TryParse(value, out var longValue) ? longValue : GetDefaultValue(targetType);
            }
            
            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return double.TryParse(value, out var doubleValue) ? doubleValue : GetDefaultValue(targetType);
            }
            
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                // Handle various boolean representations
                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }
                
                // Handle numeric representations (0 = false, non-zero = true)
                if (int.TryParse(value, out var intValue))
                {
                    return intValue != 0;
                }
                
                // Handle string representations
                var lower = value.ToLowerInvariant();
                if (lower == "yes" || lower == "y" || lower == "on")
                {
                    return true;
                }
                
                if (lower == "no" || lower == "n" || lower == "off")
                {
                    return false;
                }
                
                return GetDefaultValue(targetType);
            }
            
            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                return DateTime.TryParse(value, out var dateValue) ? dateValue : GetDefaultValue(targetType);
            }
            
            if (targetType == typeof(Guid) || targetType == typeof(Guid?))
            {
                return Guid.TryParse(value, out var guidValue) ? guidValue : GetDefaultValue(targetType);
            }
            
            if (targetType.IsEnum)
            {
                // Try to parse as enum value
                try
                {
                    return Enum.Parse(targetType, value, true);
                }
                catch
                {
                    return GetDefaultValue(targetType);
                }
            }
            
            // For unsupported types, return default value
            return GetDefaultValue(targetType);
        }

        /// <summary>
        /// Gets the default value for a type.
        /// </summary>
        private static object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            
            return null;
        }
    }
}
