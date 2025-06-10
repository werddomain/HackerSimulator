using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HackerOs.OS.Network.HTTP;
using HttpMethod = HackerOs.OS.Network.HTTP.HttpMethod;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Manages routes and handles route matching for the web server framework.
    /// </summary>
    public class Router
    {
        private readonly List<RouteInfo> _routes = new List<RouteInfo>();

        /// <summary>
        /// Registers a controller's routes in the routing system.
        /// </summary>
        /// <param name="controllerType">The controller type to register.</param>
        public void RegisterController(Type controllerType)
        {
            // Skip types that aren't controllers
            if (!typeof(IController).IsAssignableFrom(controllerType))
                return;

            // Get public instance methods
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                // Skip methods that don't return IActionResult
                if (!typeof(IActionResult).IsAssignableFrom(method.ReturnType))
                    continue;

                // Look for route attributes
                var routeAttributes = method.GetCustomAttributes<RouteAttribute>();
                
                if (!routeAttributes.Any())
                {
                    // If no route attributes, create a default route based on method name
                    var defaultTemplate = method.Name;
                    
                    // Convert CamelCase to kebab-case for default route
                    if (defaultTemplate.EndsWith("Async"))
                        defaultTemplate = defaultTemplate.Substring(0, defaultTemplate.Length - 5);
                    
                    // If method is named "Index", make it the root route
                    if (defaultTemplate.Equals("Index", StringComparison.OrdinalIgnoreCase))
                        defaultTemplate = string.Empty;
                    
                    // Create a default route info for GET requests
                    var defaultRouteInfo = new RouteInfo(
                        controllerType, 
                        method, 
                        defaultTemplate, 
                        HttpMethod.GET.ToString(),
                        $"{controllerType.Name}.{method.Name}");
                    
                    _routes.Add(defaultRouteInfo);
                }
                else
                {
                    // Add routes for each attribute
                    foreach (var attr in routeAttributes)
                    {
                        var routeInfo = new RouteInfo(
                            controllerType,
                            method,
                            attr.Template,
                            attr.HttpMethod ?? HttpMethod.GET.ToString(),
                            attr.Name ?? $"{controllerType.Name}.{method.Name}");
                        
                        _routes.Add(routeInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Matches a request URL and HTTP method to a registered route.
        /// </summary>
        /// <param name="path">The request URL path.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="routeParameters">Output dictionary of route parameters.</param>
        /// <returns>The matched route info, or null if no match was found.</returns>
        public RouteInfo MatchRoute(string path, string httpMethod, out Dictionary<string, string> routeParameters)
        {
            routeParameters = new Dictionary<string, string>();

            // Normalize path by trimming leading/trailing slashes
            path = path.Trim('/');
            
            // First, try to find an exact match with the correct HTTP method
            foreach (var route in _routes)
            {
                if (route.HttpMethod != null && 
                    !string.Equals(route.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                var routeTemplate = route.Template.Trim('/');
                
                // Check for exact match first (faster)
                if (string.Equals(routeTemplate, path, StringComparison.OrdinalIgnoreCase))
                {
                    return route;
                }

                // If the route has parameters, try to match with regex
                if (route.ParameterNames.Count > 0)
                {
                    var match = MatchWithParameters(path, routeTemplate, out var parameters);
                    if (match)
                    {
                        routeParameters = parameters;
                        return route;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Matches a path against a route template with parameters.
        /// </summary>
        private bool MatchWithParameters(string path, string routeTemplate, out Dictionary<string, string> parameters)
        {
            parameters = new Dictionary<string, string>();
            
            // Split both by '/'
            var pathSegments = path.Split('/');
            var templateSegments = routeTemplate.Split('/');
            
            // If segments don't match in count (and the template doesn't have optional params), fail quickly
            if (pathSegments.Length != templateSegments.Length &&
                !templateSegments.Any(s => s.Contains("?")))
            {
                return false;
            }
            
            // Check each segment
            for (int i = 0; i < templateSegments.Length; i++)
            {
                var template = templateSegments[i];
                
                // If we've run out of path segments
                if (i >= pathSegments.Length)
                {
                    // If this is an optional parameter, it's fine
                    if (template.StartsWith("{") && template.EndsWith("}") && template.Contains("?"))
                        continue;
                    
                    // Otherwise this is a mismatch
                    return false;
                }
                
                var pathSegment = pathSegments[i];
                
                // If this is a parameter segment
                if (template.StartsWith("{") && template.EndsWith("}"))
                {
                    // Extract parameter name
                    var paramName = template.Substring(1, template.Length - 2);
                    
                    // Check if it's optional
                    bool isOptional = paramName.EndsWith("?");
                    if (isOptional)
                    {
                        paramName = paramName.Substring(0, paramName.Length - 1);
                    }
                    
                    // Add the parameter value
                    parameters[paramName] = pathSegment;
                }
                else
                {
                    // If this is a literal segment, it must match exactly
                    if (!string.Equals(template, pathSegment, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            
            // If we have more path segments than template segments, it's a mismatch
            if (pathSegments.Length > templateSegments.Length)
            {
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Generates a URL for a named route with the provided route values.
        /// </summary>
        /// <param name="routeName">The name of the route.</param>
        /// <param name="routeValues">The route parameter values.</param>
        /// <returns>The generated URL.</returns>
        public string GenerateUrl(string routeName, Dictionary<string, string> routeValues)
        {
            // Find the route by name
            var route = _routes.FirstOrDefault(r => 
                string.Equals(r.Name, routeName, StringComparison.OrdinalIgnoreCase));
            
            if (route == null)
                return null;
            
            var template = route.Template;
            
            // Replace parameters in the template with values
            foreach (var paramName in route.ParameterNames)
            {
                string paramValue = null;
                
                // Try to get value from routeValues
                if (routeValues != null && routeValues.TryGetValue(paramName, out var value))
                {
                    paramValue = value;
                }
                
                // Replace in template
                var pattern = $"{{{paramName}}}";
                var optionalPattern = $"{{{paramName}?}}";
                
                if (paramValue != null)
                {
                    template = template.Replace(pattern, paramValue)
                                      .Replace(optionalPattern, paramValue);
                }
                else
                {
                    // If param is optional, remove it
                    if (template.Contains(optionalPattern))
                    {
                        template = template.Replace(optionalPattern, "");
                    }
                    else
                    {
                        // If param is required but not provided, return null
                        return null;
                    }
                }
            }
            
            // Clean up any double slashes
            template = Regex.Replace(template, "//+", "/");
            
            return template;
        }
    }
}
