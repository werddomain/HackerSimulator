using System;
using System.IO;
using System.Reflection;

namespace HackerSimulator.Wasm.Web
{
    /// <summary>
    /// Extremely simple view renderer used for demonstration purposes.
    /// Looks for views under the Views folder relative to the application base path
    /// and performs placeholder replacement using @Model.Property syntax.
    /// </summary>
    public static class SimpleViewRenderer
    {
        public static string Render(string viewPath, object model)
        {
            var basePath = Path.Combine(AppContext.BaseDirectory, "Views");
            var full = Path.Combine(basePath, viewPath + ".cshtml");
            if (!File.Exists(full))
                return $"<!-- View {viewPath} not found -->";

            var template = File.ReadAllText(full);
            var type = model.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var placeholder = "@Model." + prop.Name;
                var value = prop.GetValue(model)?.ToString() ?? string.Empty;
                template = template.Replace(placeholder, value);
            }
            return template;
        }
    }
}
