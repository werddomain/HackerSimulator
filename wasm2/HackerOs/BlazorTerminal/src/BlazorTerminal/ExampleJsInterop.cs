using Microsoft.JSInterop;

namespace BlazorTerminal
{
    /// <summary>
    /// Example JavaScript interop service for demonstrating how to integrate JavaScript functionality
    /// with Blazor components. This class provides an example of how JavaScript functionality can be wrapped
    /// in a .NET class for easy consumption. The associated JavaScript module is loaded on demand when first needed.
    /// This class can be registered as scoped DI service and then injected into Blazor components for use.
    /// </summary>
    public class ExampleJsInterop : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        /// <summary>
        /// Initializes a new instance of the ExampleJsInterop class
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for interop calls</param>
        public ExampleJsInterop(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorTerminal/exampleJsInterop.js").AsTask());
        }

        /// <summary>
        /// Shows a JavaScript prompt dialog with the specified message
        /// </summary>
        /// <param name="message">The message to display in the prompt</param>
        /// <returns>The user's input from the prompt</returns>
        public async ValueTask<string> Prompt(string message)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("showPrompt", message);
        }

        /// <summary>
        /// Disposes the JavaScript module reference asynchronously
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation</returns>
        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}
