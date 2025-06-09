using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Base class for controllers in the HackerOS MVC framework
    /// </summary>
    public abstract class ControllerBase : IController
    {
        private HttpContext _httpContext;
        
        /// <summary>
        /// Gets or sets the HTTP context for the current request
        /// </summary>
        public HttpContext HttpContext 
        { 
            get => _httpContext;
            set => _httpContext = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        /// <summary>
        /// Gets the model state dictionary for validation
        /// </summary>
        public ModelStateDictionary ModelState { get; } = new ModelStateDictionary();
        
        /// <summary>
        /// Gets the TempData dictionary for temporary data storage
        /// </summary>
        public Dictionary<string, object> TempData { get; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Gets the ViewData dictionary for view data
        /// </summary>
        public Dictionary<string, object> ViewData { get; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Initializes the controller with the HTTP context
        /// </summary>
        public virtual void Initialize(HttpContext context)
        {
            HttpContext = context;
        }
        
        /// <summary>
        /// Binds the HTTP request data to a model object.
        /// </summary>
        /// <typeparam name="T">The type of model to bind to.</typeparam>
        /// <returns>A new instance of T with properties populated from the request.</returns>
        protected T BindModel<T>() where T : new()
        {
            return ModelBinder.BindModel<T>(HttpContext);
        }
        
        /// <summary>
        /// Validates a model based on data annotations.
        /// </summary>
        /// <param name="model">The model to validate.</param>
        /// <returns>True if the model is valid, false otherwise.</returns>
        protected bool TryValidateModel(object model)
        {
            if (model == null)
                return false;
                
            bool isValid = ModelValidator.Validate(model, out var validationResults);
            
            // Add validation errors to ModelState
            foreach (var property in validationResults)
            {
                foreach (var error in property.Value)
                {
                    ModelState.AddModelError(property.Key, error);
                }
            }
            
            return isValid;
        }
          /// <summary>
        /// Creates a ViewResult for rendering a view
        /// </summary>
        protected ViewResult View(string? viewName = null, object? model = null)
        {
            return new ViewResult
            {
                ViewName = viewName,
                Model = model,
                ViewData = new ViewDataDictionary(ViewData)
            };
        }
        
        /// <summary>
        /// Creates a JsonResult for returning JSON data
        /// </summary>
        protected JsonResult Json(object data)
        {
            return new JsonResult
            {
                Value = data
            };
        }
        
        /// <summary>
        /// Creates a ContentResult for returning raw content
        /// </summary>
        protected ContentResult Content(string content, string contentType = "text/plain")
        {
            return new ContentResult
            {
                Content = content,
                ContentType = contentType
            };
        }
        
        /// <summary>
        /// Creates a RedirectResult for redirecting to a URL
        /// </summary>
        protected RedirectResult Redirect(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }
            
            return new RedirectResult
            {
                Url = url,
                Permanent = false
            };
        }
        
        /// <summary>
        /// Creates a StatusCodeResult for returning a status code
        /// </summary>
        protected StatusCodeResult StatusCode(HttpStatusCode statusCode)
        {
            return new StatusCodeResult
            {
                StatusCode = statusCode
            };
        }
        
        /// <summary>
        /// Creates a NotFoundResult for returning a 404 response
        /// </summary>
        protected NotFoundResult NotFound()
        {
            return new NotFoundResult();
        }
        
        /// <summary>
        /// Creates an OkResult for returning a 200 response
        /// </summary>
        protected OkResult Ok()
        {
            return new OkResult();
        }
        
        /// <summary>
        /// Creates an OkObjectResult for returning a 200 response with an object
        /// </summary>
        protected OkObjectResult Ok(object value)
        {
            return new OkObjectResult(value);
        }
        
        /// <summary>
        /// Creates a BadRequestResult for returning a 400 response
        /// </summary>
        protected BadRequestResult BadRequest()
        {
            return new BadRequestResult();
        }
        
        /// <summary>
        /// Creates a BadRequestObjectResult for returning a 400 response with an object
        /// </summary>
        protected BadRequestObjectResult BadRequest(object error)
        {
            return new BadRequestObjectResult(error);
        }
        
        /// <summary>
        /// Creates a ForbidResult for returning a 403 response
        /// </summary>
        protected ForbidResult Forbid()
        {
            return new ForbidResult();
        }
        
        /// <summary>
        /// Creates an UnauthorizedResult for returning a 401 response
        /// </summary>
        protected UnauthorizedResult Unauthorized()
        {
            return new UnauthorizedResult();
        }    }
}
