using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Base class for action results returned by controller actions
    /// </summary>
    public abstract class ActionResult : IActionResult
    {
        /// <summary>
        /// Executes the result operation by writing to the response
        /// </summary>
        public abstract Task ExecuteResultAsync(HttpContext context);
    }
      /// <summary>
    /// Represents a JSON result that writes JSON-formatted content to the response
    /// </summary>
    public class JsonResult : ActionResult
    {
        /// <summary>
        /// Gets or sets the object to serialize as JSON
        /// </summary>
        public object Value { get; set; }
        
        /// <summary>
        /// Gets or sets the JSON serializer options
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        /// <summary>
        /// Executes the result operation by writing JSON to the response
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = HttpStatusCode.OK;
            
            await context.Response.WriteJsonAsync(Value);
        }
    }
    
    /// <summary>
    /// Represents a content result that writes raw content to the response
    /// </summary>
    public class ContentResult : ActionResult
    {
        /// <summary>
        /// Gets or sets the content to write to the response
        /// </summary>
        public string Content { get; set; }
        
        /// <summary>
        /// Gets or sets the content type of the response
        /// </summary>
        public string ContentType { get; set; } = "text/plain; charset=utf-8";
        
        /// <summary>
        /// Gets or sets the status code of the response
        /// </summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        
        /// <summary>
        /// Executes the result operation by writing content to the response
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.ContentType = ContentType;
            context.Response.StatusCode = StatusCode;
            
            await context.Response.WriteAsync(Content ?? string.Empty);
        }
    }
    
    /// <summary>
    /// Represents a redirect result that redirects to a URL
    /// </summary>
    public class RedirectResult : ActionResult
    {
        /// <summary>
        /// Gets or sets the URL to redirect to
        /// </summary>
        public string Url { get; set; }
        
        /// <summary>
        /// Gets or sets whether the redirect is permanent
        /// </summary>
        public bool Permanent { get; set; }
        
        /// <summary>
        /// Executes the result operation by redirecting to a URL
        /// </summary>
        public override Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.Redirect(Url, Permanent);
            return Task.CompletedTask;
        }
    }
    
    /// <summary>
    /// Represents a status code result that returns a specific status code
    /// </summary>
    public class StatusCodeResult : ActionResult
    {
        /// <summary>
        /// Gets or sets the status code to return
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }
        
        /// <summary>
        /// Executes the result operation by setting the status code
        /// </summary>
        public override Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCode;
            return Task.CompletedTask;
        }
    }
    
    /// <summary>
    /// Represents a 404 Not Found result
    /// </summary>
    public class NotFoundResult : StatusCodeResult
    {
        /// <summary>
        /// Creates a new instance of the NotFoundResult class
        /// </summary>
        public NotFoundResult()
        {
            StatusCode = HttpStatusCode.NotFound;
        }
        
        /// <summary>
        /// Executes the result operation by setting the status code to 404
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            await context.SetErrorAsync(HttpStatusCode.NotFound, "The requested resource was not found.");
        }
    }
    
    /// <summary>
    /// Represents a 200 OK result
    /// </summary>
    public class OkResult : StatusCodeResult
    {
        /// <summary>
        /// Creates a new instance of the OkResult class
        /// </summary>
        public OkResult()
        {
            StatusCode = HttpStatusCode.OK;
        }
    }
    
    /// <summary>
    /// Represents a 200 OK result with an object
    /// </summary>
    public class OkObjectResult : JsonResult
    {
        /// <summary>
        /// Creates a new instance of the OkObjectResult class
        /// </summary>
        public OkObjectResult(object value)
        {
            Value = value;
        }
        
        /// <summary>
        /// Executes the result operation by writing the object as JSON with a 200 status code
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.OK;
            await base.ExecuteResultAsync(context);
        }
    }
    
    /// <summary>
    /// Represents a 400 Bad Request result
    /// </summary>
    public class BadRequestResult : StatusCodeResult
    {
        /// <summary>
        /// Creates a new instance of the BadRequestResult class
        /// </summary>
        public BadRequestResult()
        {
            StatusCode = HttpStatusCode.BadRequest;
        }
        
        /// <summary>
        /// Executes the result operation by setting the status code to 400
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.BadRequest;
            await context.SetErrorAsync(HttpStatusCode.BadRequest, "The request was invalid.");
        }
    }
    
    /// <summary>
    /// Represents a 400 Bad Request result with an object
    /// </summary>
    public class BadRequestObjectResult : JsonResult
    {
        /// <summary>
        /// Creates a new instance of the BadRequestObjectResult class
        /// </summary>
        public BadRequestObjectResult(object error)
        {
            Value = error;
        }
        
        /// <summary>
        /// Executes the result operation by writing the object as JSON with a 400 status code
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.BadRequest;
            await base.ExecuteResultAsync(context);
        }
    }
    
    /// <summary>
    /// Represents a 403 Forbidden result
    /// </summary>
    public class ForbidResult : StatusCodeResult
    {
        /// <summary>
        /// Creates a new instance of the ForbidResult class
        /// </summary>
        public ForbidResult()
        {
            StatusCode = HttpStatusCode.Forbidden;
        }
        
        /// <summary>
        /// Executes the result operation by setting the status code to 403
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.Forbidden;
            await context.SetErrorAsync(HttpStatusCode.Forbidden, "You do not have permission to access this resource.");
        }
    }
    
    /// <summary>
    /// Represents a 401 Unauthorized result
    /// </summary>
    public class UnauthorizedResult : StatusCodeResult
    {
        /// <summary>
        /// Creates a new instance of the UnauthorizedResult class
        /// </summary>
        public UnauthorizedResult()
        {
            StatusCode = HttpStatusCode.Unauthorized;
        }
        
        /// <summary>
        /// Executes the result operation by setting the status code to 401
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            context.Response.StatusCode = HttpStatusCode.Unauthorized;
            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"HackerOS\"";
            await context.SetErrorAsync(HttpStatusCode.Unauthorized, "Authentication is required to access this resource.");
        }
    }
  }
