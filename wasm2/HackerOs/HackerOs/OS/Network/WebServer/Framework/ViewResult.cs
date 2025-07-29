using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Network.HTTP;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Dictionary that stores data for a view.
    /// </summary>
    public class ViewDataDictionary : Dictionary<string, object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ViewDataDictionary"/> class.
        /// </summary>
        public ViewDataDictionary()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewDataDictionary"/> class with items copied from the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary to copy items from.</param>
        public ViewDataDictionary(IDictionary<string, object> dictionary)
        {
            if (dictionary != null)
            {
                foreach (var item in dictionary)
                {
                    this[item.Key] = item.Value;
                }
            }
        }
    }

    /// <summary>
    /// Represents an action result that renders a view to the response.
    /// </summary>
    public class ViewResult : IActionResult
    {
        /// <summary>
        /// Gets or sets the name of the view to render.
        /// </summary>
        public string ViewName { get; set; }

        /// <summary>
        /// Gets or sets the model to pass to the view.
        /// </summary>
        public object Model { get; set; }

        /// <summary>
        /// Gets or sets the view data dictionary.
        /// </summary>
        public ViewDataDictionary ViewData { get; set; } = new ViewDataDictionary();

        /// <summary>
        /// Gets or sets the content type of the response.
        /// </summary>
        public string ContentType { get; set; } = "text/html";

        /// <summary>
        /// Executes the result by rendering the view to the response.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteResultAsync(HttpContext context)
        {
            // Get the controller name from the context
            var controllerName = context.Items.TryGetValue("ControllerName", out var name) 
                ? name.ToString() 
                : null;
            
            // Get the view engine from the context
            if (!context.Items.TryGetValue("ViewEngine", out var engineObj) || !(engineObj is IViewEngine viewEngine))
            {
                context.Response.StatusCode = HttpStatusCode.InternalServerError;
                await context.Response.WriteAsync("View engine not available.");
                return;
            }

            try
            {
                // Use the controller name + action name as the default view name
                string resolvedViewName = ViewName;
                if (string.IsNullOrEmpty(resolvedViewName))
                {
                    if (context.Items.TryGetValue("ActionName", out var actionName))
                    {
                        resolvedViewName = actionName.ToString();
                    }
                    else
                    {
                        resolvedViewName = "Index";
                    }
                }

                // Check if the view exists
                if (!viewEngine.ViewExists(resolvedViewName, controllerName))
                {
                    context.Response.StatusCode = HttpStatusCode.NotFound;
                    await context.Response.WriteAsync($"View '{resolvedViewName}' not found.");
                    return;
                }

                // Create a view model that combines the model and view data
                var viewModel = new ViewModelWrapper
                {
                    Model = Model,
                    ViewData = ViewData
                };

                // Render the view
                var renderedContent = await viewEngine.RenderViewAsync(resolvedViewName, viewModel, controllerName);
                
                // Set content type and write the rendered content
                context.Response.ContentType = ContentType;
                await context.Response.WriteAsync(renderedContent);
            }
            catch (global::System.Exception ex)
            {
                context.Response.StatusCode = HttpStatusCode.InternalServerError;
                await context.Response.WriteAsync($"Error rendering view: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Wrapper class for passing both Model and ViewData to the view engine.
    /// </summary>
    public class ViewModelWrapper
    {
        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        public object Model { get; set; }

        /// <summary>
        /// Gets or sets the view data.
        /// </summary>
        public ViewDataDictionary ViewData { get; set; }
    }
}
