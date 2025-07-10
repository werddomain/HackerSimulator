using HackerOs.OS.HSystem.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Provides model validation functionality.
    /// </summary>
    public class ModelValidator
    {
        /// <summary>
        /// Validates a model based on data annotations.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <param name="validationResults">The validation results.</param>
        /// <returns>True if the model is valid, false otherwise.</returns>
        public static bool Validate(object model, out Dictionary<string, List<string>> validationResults)
        {
            validationResults = new Dictionary<string, List<string>>();
            
            if (model == null)
            {
                return false;
            }
            
            var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            bool isValid = true;
            
            foreach (var property in properties)
            {
                var value = property.GetValue(model);
                var validationAttributes = property.GetCustomAttributes<ValidationAttribute>();
                
                foreach (var attribute in validationAttributes)
                {
                    var result = attribute.GetValidationResult(value, new ValidationContext(model) { MemberName = property.Name });
                    
                    if (result != ValidationResult.Success)
                    {
                        isValid = false;
                        
                        // Add the error message to the results
                        if (!validationResults.TryGetValue(property.Name, out var errors))
                        {
                            errors = new List<string>();
                            validationResults[property.Name] = errors;
                        }
                        
                        errors.Add(result.ErrorMessage);
                    }
                }
            }
            
            return isValid;
        }
    }
    
    /// <summary>
    /// Attribute to mark a property as required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequiredAttribute"/> class.
        /// </summary>
        public RequiredAttribute()
            : base("The {0} field is required.")
        {
        }
        
        /// <summary>
        /// Validates that the value is not null.
        /// </summary>
        public override bool IsValid(object value)
        {
            return value != null && (!(value is string) || !string.IsNullOrWhiteSpace((string)value));
        }
    }
    
    /// <summary>
    /// Attribute to validate a string length.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class StringLengthAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets the maximum allowed length of the string.
        /// </summary>
        public int MaximumLength { get; }
        
        /// <summary>
        /// Gets the minimum allowed length of the string.
        /// </summary>
        public int MinimumLength { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="StringLengthAttribute"/> class.
        /// </summary>
        /// <param name="maximumLength">The maximum allowed length of the string.</param>
        public StringLengthAttribute(int maximumLength)
            : base("The field {0} must be a string with a maximum length of {1}.")
        {
            MaximumLength = maximumLength;
        }
        
        /// <summary>
        /// Validates the length of the string.
        /// </summary>
        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return true; // Handled by RequiredAttribute
            }
            
            var str = value as string;
            if (str == null)
            {
                return false;
            }
            
            return str.Length <= MaximumLength && str.Length >= MinimumLength;
        }
        
        /// <summary>
        /// Formats the error message.
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            if (MinimumLength > 0)
            {
                return string.Format(
                    "The field {0} must be a string with a minimum length of {1} and a maximum length of {2}.",
                    name, MinimumLength, MaximumLength);
            }
            
            return string.Format(ErrorMessageString, name, MaximumLength);
        }
    }
    
    /// <summary>
    /// Attribute to validate a numeric range.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RangeAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets the minimum value.
        /// </summary>
        public object Minimum { get; }
        
        /// <summary>
        /// Gets the maximum value.
        /// </summary>
        public object Maximum { get; }
        
        /// <summary>
        /// Gets the type of the range.
        /// </summary>
        public Type OperandType { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RangeAttribute"/> class.
        /// </summary>
        /// <param name="minimum">The minimum value.</param>
        /// <param name="maximum">The maximum value.</param>
        public RangeAttribute(int minimum, int maximum)
            : base("The field {0} must be between {1} and {2}.")
        {
            Minimum = minimum;
            Maximum = maximum;
            OperandType = typeof(int);
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RangeAttribute"/> class.
        /// </summary>
        /// <param name="minimum">The minimum value.</param>
        /// <param name="maximum">The maximum value.</param>
        public RangeAttribute(double minimum, double maximum)
            : base("The field {0} must be between {1} and {2}.")
        {
            Minimum = minimum;
            Maximum = maximum;
            OperandType = typeof(double);
        }
        
        /// <summary>
        /// Validates that the value is within the range.
        /// </summary>
        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return true; // Handled by RequiredAttribute
            }
            
            if (OperandType == typeof(int))
            {
                if (value is int intValue)
                {
                    return intValue >= (int)Minimum && intValue <= (int)Maximum;
                }
            }
            else if (OperandType == typeof(double))
            {
                if (value is double doubleValue)
                {
                    return doubleValue >= (double)Minimum && doubleValue <= (double)Maximum;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Formats the error message.
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name, Minimum, Maximum);
        }
    }
    
    /// <summary>
    /// Attribute to validate a regular expression.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RegularExpressionAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets the regular expression pattern.
        /// </summary>
        public string Pattern { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RegularExpressionAttribute"/> class.
        /// </summary>
        /// <param name="pattern">The regular expression pattern.</param>
        public RegularExpressionAttribute(string pattern)
            : base("The field {0} must match the regular expression '{1}'.")
        {
            Pattern = pattern;
        }
        
        /// <summary>
        /// Validates that the value matches the pattern.
        /// </summary>
        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return true; // Handled by RequiredAttribute
            }
            
            var str = value as string;
            if (str == null)
            {
                return false;
            }
            
            return Regex.IsMatch(str, Pattern);
        }
        
        /// <summary>
        /// Formats the error message.
        /// </summary>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(ErrorMessageString, name, Pattern);
        }
    }
}
