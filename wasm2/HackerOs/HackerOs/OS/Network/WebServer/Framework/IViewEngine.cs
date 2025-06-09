using System.Threading.Tasks;

namespace HackerOs.OS.Network.WebServer.Framework
{
    /// <summary>
    /// Interface for view engines that render templates to HTML.
    /// </summary>
    public interface IViewEngine
    {        /// <summary>
        /// Renders a view template with the specified model data.
        /// </summary>
        /// <param name="viewName">The name of the view to render.</param>
        /// <param name="model">The model data to pass to the view.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <returns>The rendered HTML content.</returns>
        Task<string> RenderViewAsync(string viewName, object model, string? controllerName = null);
        
        /// <summary>
        /// Renders a partial view template with the specified model data.
        /// </summary>
        /// <param name="partialViewName">The name of the partial view to render.</param>
        /// <param name="model">The model data to pass to the partial view.</param>
        /// <returns>The rendered HTML content.</returns>
        Task<string> RenderPartialAsync(string partialViewName, object model);
        
        /// <summary>
        /// Adds a view source to the view engine.
        /// </summary>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="viewContent">The view template content.</param>
        /// <param name="controllerName">Optional controller name for the view.</param>
        void AddViewSource(string viewName, string viewContent, string? controllerName = null);
        
        /// <summary>
        /// Checks if a view exists.
        /// </summary>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="controllerName">Optional controller name for the view.</param>
        /// <returns>True if the view exists, false otherwise.</returns>
        bool ViewExists(string viewName, string? controllerName = null);
        
        /// <summary>
        /// Renders a template string with the specified model data.
        /// </summary>
        /// <param name="template">The template string to render.</param>
        /// <param name="model">The model data to pass to the template.</param>
        /// <param name="bodyContent">Optional body content for layouts.</param>
        /// <param name="modelType">Optional model type name for the template.</param>
        /// <returns>The rendered HTML content.</returns>
        Task<string> RenderTemplateAsync(string template, object model, string? bodyContent = null, string? modelType = null);
    }
}
