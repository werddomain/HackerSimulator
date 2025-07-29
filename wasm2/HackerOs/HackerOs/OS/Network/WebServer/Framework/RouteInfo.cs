using System;
using System.Collections.Generic;
using System.Reflection;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Represents information about a route in the application.
    /// </summary>
    public class RouteInfo
    {
        /// <summary>
        /// Gets the controller type for this route.
        /// </summary>
        public Type ControllerType { get; }

        /// <summary>
        /// Gets the action method for this route.
        /// </summary>
        public MethodInfo ActionMethod { get; }

        /// <summary>
        /// Gets the route template pattern.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Gets the HTTP method for this route.
        /// </summary>
        public string HttpMethod { get; }

        /// <summary>
        /// Gets the name of the route.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the route parameter names extracted from the template.
        /// </summary>
        public List<string> ParameterNames { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteInfo"/> class.
        /// </summary>
        /// <param name="controllerType">The controller type.</param>
        /// <param name="actionMethod">The action method.</param>
        /// <param name="template">The route template.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="name">The route name.</param>
        public RouteInfo(Type controllerType, MethodInfo actionMethod, string template, string httpMethod, string name)
        {
            ControllerType = controllerType;
            ActionMethod = actionMethod;
            Template = template;
            HttpMethod = httpMethod;
            Name = name;
            ParameterNames = ExtractParameterNames(template);
        }

        /// <summary>
        /// Extracts parameter names from a route template.
        /// </summary>
        /// <param name="template">The route template.</param>
        /// <returns>A list of parameter names.</returns>
        private List<string> ExtractParameterNames(string template)
        {
            var parameters = new List<string>();
            
            if (string.IsNullOrEmpty(template))
                return parameters;

            // Split the template by '/'
            var segments = template.Split('/');
            
            foreach (var segment in segments)
            {
                // Check if segment is a parameter (enclosed in {})
                if (segment.StartsWith("{") && segment.EndsWith("}"))
                {
                    // Extract the parameter name
                    var paramName = segment.Substring(1, segment.Length - 2);
                    
                    // Handle optional parameters with ?
                    if (paramName.EndsWith("?"))
                    {
                        paramName = paramName.Substring(0, paramName.Length - 1);
                    }
                    
                    parameters.Add(paramName);
                }
            }
            
            return parameters;
        }
    }
}
