using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Simple template engine for rendering views with a syntax similar to Razor.
    /// </summary>
    public class ViewEngine : IViewEngine
    {
        private readonly Dictionary<string, ViewSourceInfo> _viewSources = new Dictionary<string, ViewSourceInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _viewCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _layoutView = null;

        /// <summary>
        /// Represents information about a view source including its content and model type.
        /// </summary>
        private class ViewSourceInfo
        {
            public string Content { get; set; }
            public string ModelType { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewEngine"/> class.
        /// </summary>
        public ViewEngine()
        {
        }

        /// <summary>
        /// Sets the layout view template.
        /// </summary>
        /// <param name="layoutContent">The layout template content.</param>
        public void SetLayout(string layoutContent)
        {
            _layoutView = layoutContent;
        }

        /// <summary>
        /// Adds a view source to the view engine.
        /// </summary>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="viewContent">The view template content.</param>
        /// <param name="controllerName">Optional controller name for the view.</param>
        public void AddViewSource(string viewName, string viewContent, string controllerName = null)
        {
            var key = GetViewKey(viewName, controllerName);
            
            // Parse model directive
            var modelType = ExtractModelType(viewContent);
            
            _viewSources[key] = new ViewSourceInfo
            {
                Content = viewContent,
                ModelType = modelType
            };
            
            // Clear the cache for this view
            if (_viewCache.ContainsKey(key))
            {
                _viewCache.Remove(key);
            }
        }

        /// <summary>
        /// Extracts the model type from a view template containing an @model directive.
        /// </summary>
        /// <param name="viewContent">The view template content.</param>
        /// <returns>The model type name, or null if no @model directive is found.</returns>
        private string ExtractModelType(string viewContent)
        {
            // Look for @model directive at the beginning of the view
            var modelRegex = new Regex(@"@model\s+([A-Za-z0-9_\.]+)");
            var match = modelRegex.Match(viewContent);
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            
            return null;
        }

        /// <summary>
        /// Checks if a view exists.
        /// </summary>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="controllerName">Optional controller name for the view.</param>
        /// <returns>True if the view exists, false otherwise.</returns>
        public bool ViewExists(string viewName, string controllerName = null)
        {
            var key = GetViewKey(viewName, controllerName);
            return _viewSources.ContainsKey(key);
        }

        /// <summary>
        /// Renders a view template with the specified model data.
        /// </summary>
        /// <param name="viewName">The name of the view to render.</param>
        /// <param name="model">The model data to pass to the view.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <returns>The rendered HTML content.</returns>
        public async Task<string> RenderViewAsync(string viewName, object model, string controllerName = null)
        {
            var viewKey = GetViewKey(viewName, controllerName);
            
            if (!_viewSources.TryGetValue(viewKey, out var viewSourceInfo))
            {
                throw new Exception($"View '{viewKey}' not found.");
            }

            // First render the view content
            var renderedContent = await RenderTemplateAsync(viewSourceInfo.Content, model, null, viewSourceInfo.ModelType);
            
            // Then apply layout if it exists
            if (_layoutView != null)
            {
                // Replace @RenderBody() in the layout with the rendered view content
                var result = await RenderTemplateAsync(_layoutView, model, renderedContent);
                return result;
            }
            
            return renderedContent;
        }

        /// <summary>
        /// Renders a partial view template with the specified model data.
        /// </summary>
        /// <param name="partialViewName">The name of the partial view to render.</param>
        /// <param name="model">The model data to pass to the partial view.</param>
        /// <returns>The rendered HTML content.</returns>
        public async Task<string> RenderPartialAsync(string partialViewName, object model)
        {
            var viewKey = $"Partial_{partialViewName}";
            
            if (!_viewSources.TryGetValue(viewKey, out var viewSourceInfo))
            {
                throw new Exception($"Partial view '{partialViewName}' not found.");
            }

            return await RenderTemplateAsync(viewSourceInfo.Content, model, null, viewSourceInfo.ModelType);
        }

        /// <summary>
        /// Renders a template with the specified model data.
        /// </summary>
        /// <param name="template">The template to render.</param>
        /// <param name="model">The model data.</param>
        /// <param name="bodyContent">Optional body content for layouts.</param>
        /// <param name="modelType">Optional model type for the view.</param>
        /// <returns>The rendered content.</returns>
        public async Task<string> RenderTemplateAsync(string template, object model, string bodyContent = null, string modelType = null)
        {
            // Remove @model directive
            if (modelType != null)
            {
                var modelRegex = new Regex(@"@model\s+[A-Za-z0-9_\.]+");
                template = modelRegex.Replace(template, "");
            }
            
            // Replace @RenderBody() with body content if provided
            if (bodyContent != null)
            {
                template = template.Replace("@RenderBody()", bodyContent);
            }
            
            // Process include directives @Html.Partial("partialName")
            template = await ProcessPartialIncludesAsync(template, model);
            
            // Process expressions @Model.Property
            template = ProcessExpressions(template, model);
            
            // Process code blocks @{ ... }
            template = ProcessCodeBlocks(template, model);
            
            // Process if statements @if (condition) { ... } else { ... }
            template = ProcessConditionals(template, model);
            
            // Process foreach loops @foreach (var item in items) { ... }
            template = ProcessLoops(template, model);
            
            return template;
        }

        /// <summary>
        /// Processes partial view includes in a template.
        /// </summary>
        private async Task<string> ProcessPartialIncludesAsync(string template, object model)
        {
            var partialRegex = new Regex(@"@Html\.Partial\(""([^""]+)""\)");
            var matches = partialRegex.Matches(template);
            
            foreach (Match match in matches)
            {
                var partialName = match.Groups[1].Value;
                var partialContent = await RenderPartialAsync(partialName, model);
                template = template.Replace(match.Value, partialContent);
            }
            
            return template;
        }

        /// <summary>
        /// Processes expressions in a template.
        /// </summary>
        private string ProcessExpressions(string template, object model)
        {
            // Basic expression pattern: @Model.Property
            var regex = new Regex(@"@Model\.([A-Za-z0-9_\.]+)");
            
            return regex.Replace(template, match =>
            {
                var propertyPath = match.Groups[1].Value;
                var propertyValue = GetPropertyValueByPath(model, propertyPath);
                return propertyValue?.ToString() ?? string.Empty;
            });
        }

        /// <summary>
        /// Gets the value of a property from an object, supporting nested properties.
        /// </summary>
        /// <param name="obj">The object to get the property from.</param>
        /// <param name="propertyPath">The property path, which can include dots for nested properties.</param>
        /// <returns>The property value.</returns>
        private object GetPropertyValueByPath(object obj, string propertyPath)
        {
            if (obj == null) return null;
            
            // For ViewModelWrapper, extract the Model property first
            if (obj is ViewModelWrapper wrapper)
            {
                obj = wrapper.Model;
                if (obj == null) return null;
            }
            
            // Split the path for nested properties
            var parts = propertyPath.Split('.');
            var current = obj;
            
            foreach (var part in parts)
            {
                if (current == null) return null;
                
                var property = current.GetType().GetProperty(part);
                if (property == null) return null;
                
                current = property.GetValue(current);
            }
            
            return current;
        }

        /// <summary>
        /// Processes code blocks in a template.
        /// </summary>
        private string ProcessCodeBlocks(string template, object model)
        {
            // This is a simplified version that just removes code blocks
            // In a real implementation, this would execute C# code
            var regex = new Regex(@"@\{[^}]*\}");
            return regex.Replace(template, "");
        }

        /// <summary>
        /// Processes conditional statements in a template.
        /// </summary>
        private string ProcessConditionals(string template, object model)
        {
            // Simple if statement processing with nested property support
            var regex = new Regex(@"@if \(Model\.([A-Za-z0-9_\.]+)\) \{([^}]*)\} else \{([^}]*)\}");
            
            return regex.Replace(template, match =>
            {
                var propertyPath = match.Groups[1].Value;
                var trueContent = match.Groups[2].Value;
                var falseContent = match.Groups[3].Value;
                
                var propertyValue = GetPropertyValueByPath(model, propertyPath);
                
                if (propertyValue is bool boolValue)
                {
                    return boolValue ? trueContent : falseContent;
                }
                
                return falseContent;
            });
        }

        /// <summary>
        /// Processes loop statements in a template.
        /// </summary>
        private string ProcessLoops(string template, object model)
        {
            // Simple foreach processing for lists with nested property support
            var regex = new Regex(@"@foreach \(var item in Model\.([A-Za-z0-9_\.]+)\) \{([^}]*)\}");
            
            return regex.Replace(template, match =>
            {
                var propertyPath = match.Groups[1].Value;
                var loopContent = match.Groups[2].Value;
                
                var propertyValue = GetPropertyValueByPath(model, propertyPath);
                
                if (propertyValue is IEnumerable<object> items)
                {
                    var result = new StringBuilder();
                    
                    foreach (var item in items)
                    {
                        // Replace @item.Property with item property values
                        var itemContent = loopContent;
                        var itemRegex = new Regex(@"@item\.([A-Za-z0-9_\.]+)");
                        
                        itemContent = itemRegex.Replace(itemContent, itemMatch =>
                        {
                            var itemPropPath = itemMatch.Groups[1].Value;
                            var itemPropValue = GetPropertyValueByPath(item, itemPropPath);
                            return itemPropValue?.ToString() ?? string.Empty;
                        });
                        
                        result.Append(itemContent);
                    }
                    
                    return result.ToString();
                }
                
                return string.Empty;
            });
        }
    }
}
