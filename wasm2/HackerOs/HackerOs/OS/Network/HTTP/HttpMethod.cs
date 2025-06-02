using System;

namespace HackerOs.OS.Network.HTTP
{
    /// <summary>
    /// HTTP methods used in HackerOS web server requests
    /// </summary>
    public enum HttpMethod
    {
        /// <summary>
        /// GET method: Requests a representation of the specified resource
        /// </summary>
        GET,
        
        /// <summary>
        /// POST method: Submits data to be processed to the identified resource
        /// </summary>
        POST,
        
        /// <summary>
        /// PUT method: Uploads a representation of the specified resource
        /// </summary>
        PUT,
        
        /// <summary>
        /// DELETE method: Deletes the specified resource
        /// </summary>
        DELETE,
        
        /// <summary>
        /// HEAD method: Same as GET but returns only HTTP headers and no document body
        /// </summary>
        HEAD,
        
        /// <summary>
        /// OPTIONS method: Returns the HTTP methods that the server supports
        /// </summary>
        OPTIONS,
        
        /// <summary>
        /// PATCH method: Applies partial modifications to a resource
        /// </summary>
        PATCH,
        
        /// <summary>
        /// TRACE method: Performs a message loop-back test along the path to the target resource
        /// </summary>
        TRACE
    }
    
    /// <summary>
    /// Extension methods for HttpMethod enum
    /// </summary>
    public static class HttpMethodExtensions
    {
        /// <summary>
        /// Converts HttpMethod enum to its string representation
        /// </summary>
        public static string ToMethodString(this HttpMethod method)
        {
            return method.ToString();
        }
        
        /// <summary>
        /// Tries to parse a string into an HttpMethod enum
        /// </summary>
        public static bool TryParse(string methodString, out HttpMethod method)
        {
            if (string.IsNullOrWhiteSpace(methodString))
            {
                method = HttpMethod.GET;
                return false;
            }
            
            if (Enum.TryParse<HttpMethod>(methodString.Trim().ToUpper(), out var result))
            {
                method = result;
                return true;
            }
            
            method = HttpMethod.GET;
            return false;
        }
    }
}
