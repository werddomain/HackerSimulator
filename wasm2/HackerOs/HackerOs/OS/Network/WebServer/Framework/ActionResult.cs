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
    /// Represents a view result that renders a view to the response
    /// </summary>
    public class ViewResult : ActionResult
    {
        /// <summary>
        /// Gets or sets the name of the view to render
        /// </summary>
        public string ViewName { get; set; }
        
        /// <summary>
        /// Gets or sets the model object to pass to the view
        /// </summary>
        public object Model { get; set; }
        
        /// <summary>
        /// Gets or sets the view data dictionary
        /// </summary>
        public ViewDataDictionary ViewData { get; set; } = new ViewDataDictionary();
        
        /// <summary>
        /// Gets or sets the layout to use for the view
        /// </summary>
        public string Layout { get; set; } = "_Layout";
        
        /// <summary>
        /// Executes the result operation by rendering the view
        /// </summary>
        public override async Task ExecuteResultAsync(HttpContext context)
        {
            // The actual implementation would use a view engine to render the view
            // For now, we'll just render a simple HTML representation of the view
            
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = HttpStatusCode.OK;
            
            string viewContent = await GetViewContentAsync(context);
            
            // If a layout is specified, render the view inside the layout
            if (!string.IsNullOrEmpty(Layout))
            {
                string layoutContent = await GetLayoutContentAsync(context);
                viewContent = layoutContent.Replace("@RenderBody()", viewContent);
            }
            
            await context.Response.WriteAsync(viewContent);
        }
        
        /// <summary>
        /// Gets the content of the view
        /// </summary>
        private async Task<string> GetViewContentAsync(HttpContext context)
        {
            // In a real implementation, this would load and compile the view template
            // For now, we'll just return a simple representation of the view and model
            
            string viewName = ViewName ?? context.Request.Url.Segments[context.Request.Url.Segments.Length - 1];
            if (string.IsNullOrEmpty(viewName))
            {
                viewName = "Index";
            }
            
            var viewContent = $@"
<div class='view' data-view-name='{viewName}'>
    <h2>View: {viewName}</h2>";
            
            if (Model != null)
            {
                viewContent += "<div class='model'>";
                viewContent += "<h3>Model:</h3>";
                viewContent += $"<pre>{JsonSerializer.Serialize(Model, new JsonSerializerOptions { WriteIndented = true })}</pre>";
                viewContent += "</div>";
            }
            
            if (ViewData != null && ViewData.Count > 0)
            {
                viewContent += "<div class='view-data'>";
                viewContent += "<h3>ViewData:</h3>";
                viewContent += $"<pre>{JsonSerializer.Serialize(ViewData, new JsonSerializerOptions { WriteIndented = true })}</pre>";
                viewContent += "</div>";
            }
            
            viewContent += "</div>";
            
            return viewContent;
        }
        
        /// <summary>
        /// Gets the content of the layout
        /// </summary>
        private async Task<string> GetLayoutContentAsync(HttpContext context)
        {
            // In a real implementation, this would load and compile the layout template
            // For now, we'll just return a simple layout
            
            return $@"<!DOCTYPE html>
<html>
<head>
    <title>HackerOS Web Server - {context.Request.Url.Segments[context.Request.Url.Segments.Length - 1]}</title>
    <style>
        body {{ font-family: 'Consolas', monospace; background-color: #0a0a0a; color: #0f0; padding: 20px; }}
        h1, h2, h3 {{ color: #0f0; }}
        pre {{ background-color: #111; padding: 10px; border: 1px solid #0f0; }}
        .view {{ border: 1px solid #0f0; padding: 20px; margin: 20px 0; }}
        .model, .view-data {{ margin-top: 20px; }}
    </style>
</head>
<body>
    <h1>HackerOS Web Server</h1>
    @RenderBody()
    <hr/>
    <footer>
        <p>Powered by HackerOS Web Server - {DateTime.Now}</p>
    </footer>
</body>
</html>";
        }
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
    
    /// <summary>
    /// Dictionary for storing view data
    /// </summary>
    public class ViewDataDictionary : Dictionary<string, object>
    {
        /// <summary>
        /// Creates a new instance of the ViewDataDictionary class
        /// </summary>
        public ViewDataDictionary() { }
        
        /// <summary>
        /// Creates a new instance of the ViewDataDictionary class with items from another dictionary
        /// </summary>
        public ViewDataDictionary(IDictionary<string, object> dictionary)
            : base(dictionary)
        {
        }
    }
}
