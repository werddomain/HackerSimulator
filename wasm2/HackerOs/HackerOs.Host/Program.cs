using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel from appsettings.json
var port = builder.Configuration["HackerOs:Host:Port"] ?? "5000";
builder.WebHost.UseUrls($"http://localhost:{port}");

// Add services for static file hosting
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream", "application/wasm" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseResponseCompression();

// Serve static files with proper MIME types for Blazor WASM
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var headers = context.Context.Response.Headers;
        if (context.File.Name.EndsWith(".wasm"))
        {
            headers["Content-Type"] = "application/wasm";
        }
        else if (context.File.Name.EndsWith(".dll"))
        {
            headers["Content-Type"] = "application/octet-stream";
        }
    }
});

app.UseRouting();

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html");

Console.WriteLine($"🚀 HackerOS Host Server starting on http://localhost:{port}");
Console.WriteLine("📁 Serving static files from wwwroot directory");
Console.WriteLine("⚡ Ready to host Blazor WebAssembly application");
Console.WriteLine("🛑 Press Ctrl+C to stop the server");

app.Run();
