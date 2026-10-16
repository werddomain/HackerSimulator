using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HackerOs.AppSdk.Web.Mvc;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerOs.AppSdk.Web.Views;

/// <summary>
/// Finds and renders the views of a site (ASP.NET's <c>IViewEngine</c> and <c>IView</c>, ADR 0083):
/// a name is looked up in the generated <see cref="HackViewTable"/>, from <c>Views/&lt;Controller&gt;/</c>
/// then <c>Views/Shared/</c>; the view is rendered to HTML by Blazor's <c>HtmlRenderer</c> (static,
/// no interactivity), then each layout is rendered around the HTML of what it wraps.
/// </summary>
/// <remarks>
/// The view renders <em>before</em> its layout, in a pass of its own, as ASP.NET MVC executes a
/// <c>.cshtml</c> page before its <c>_Layout</c>: what the view sets (<c>ViewData["Title"]</c>,
/// <see cref="HackSection"/>, <c>Layout</c>) is known when the layout renders. All the passes of a page
/// share one renderer, whose services are the request's scope (<c>@inject</c> works).
/// </remarks>
public sealed class HackViewEngine
{
    /// <summary>Initializes the engine of a site.</summary>
    /// <param name="views">The site's views.</param>
    public HackViewEngine(HackViewTable views)
    {
        ArgumentNullException.ThrowIfNull(views);
        Views = views;
    }

    /// <summary>Gets the site's views.</summary>
    public HackViewTable Views { get; }

    /// <summary>
    /// Finds a view by name: <c>Index</c> is searched as <c>Views/&lt;Controller&gt;/Index.razor</c>, then
    /// <c>Views/Shared/Index.razor</c>; a path (<c>~/Views/Notes/Edit.razor</c>, <c>/Views/Notes/Edit</c>) is taken as is.
    /// </summary>
    /// <param name="context">The action context (its controller names the folder).</param>
    /// <param name="name">The view name or path.</param>
    /// <param name="searchedLocations">The locations tried, for the error message.</param>
    /// <returns>The view, or <see langword="null"/>.</returns>
    public HackViewDescriptor? FindView(HackActionContext context, string name, out IReadOnlyList<string> searchedLocations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(name);
        List<string> candidates = [];
        if (name.StartsWith("~/", StringComparison.Ordinal) || name.StartsWith('/'))
        {
            string path = name.TrimStart('~').TrimStart('/');
            if (path.StartsWith("Views/", StringComparison.OrdinalIgnoreCase))
            {
                path = path["Views/".Length..];
            }

            if (path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                path = path[..^".razor".Length];
            }

            candidates.Add(path);
        }
        else
        {
            string? controller = context.ActionDescriptor?.ControllerName ?? context.RouteValues.GetString("controller");
            if (!string.IsNullOrEmpty(controller))
            {
                candidates.Add($"{controller}/{name}");
            }

            candidates.Add($"Shared/{name}");
        }

        searchedLocations = [.. candidates.Select(candidate => $"/Views/{candidate}.razor")];
        foreach (string candidate in candidates)
        {
            if (Views.Find(candidate) is HackViewDescriptor view)
            {
                return view;
            }
        }

        return null;
    }

    /// <summary>Finds a view by name, or throws with the searched locations (as ASP.NET MVC does).</summary>
    /// <param name="context">The action context.</param>
    /// <param name="name">The view name or path.</param>
    /// <returns>The view.</returns>
    /// <exception cref="InvalidOperationException">No view has this name.</exception>
    public HackViewDescriptor GetView(HackActionContext context, string name) =>
        FindView(context, name, out IReadOnlyList<string> searched) ?? throw new InvalidOperationException(
            $"The view '{name}' was not found. The following locations were searched:{Environment.NewLine}{string.Join(Environment.NewLine, searched)}");

    /// <summary>
    /// Renders a view to HTML: the view, then (unless <paramref name="isPartial"/>) its layouts, each
    /// around the HTML of the previous pass.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="viewType">The view component.</param>
    /// <param name="model">The model (checked against <c>HackView&lt;TModel&gt;</c>).</param>
    /// <param name="viewData">The view data from the controller.</param>
    /// <param name="isPartial">Whether to skip layouts and section checks (<c>PartialView()</c>).</param>
    /// <returns>The HTML.</returns>
    /// <exception cref="InvalidOperationException">Wrong model type, a layout without <c>@Body</c>, a section not rendered or missing.</exception>
    public async Task<string> RenderAsync(
        HackActionContext context,
        [DynamicallyAccessedMembers(HackViewTypes.Component)] Type viewType,
        object? model,
        HackViewDataDictionary viewData,
        bool isPartial)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(viewType);
        ArgumentNullException.ThrowIfNull(viewData);
        EnsureModelType(viewType, model);

        IServiceProvider services = context.HttpContext.RequestServices;
        ILoggerFactory loggers = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        await using HtmlRenderer renderer = new(services, loggers);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HackViewContext viewContext = new(context, this, model, viewData, isPartial)
            {
                Layout = isPartial ? null : LayoutOf(viewType)
            };

            string html = await RenderPassAsync(renderer, viewType, viewContext, body: null).ConfigureAwait(false);
            if (isPartial)
            {
                return html;
            }

            // Layouts from the inside out; a layout that names itself (an @layout in _Imports.razor
            // reaches the layout too) or an earlier one ends the chain instead of looping.
            HashSet<Type> applied = [];
            while (viewContext.Layout is Type layout && applied.Add(layout))
            {
                viewContext.Layout = LayoutOf(layout);
                bool bodyRendered = false;
                string inner = html;
                RenderFragment body = builder =>
                {
                    bodyRendered = true;
                    builder.AddMarkupContent(0, inner);
                };

                html = await RenderPassAsync(renderer, layout, viewContext, body).ConfigureAwait(false);
                if (!bodyRendered)
                {
                    throw new InvalidOperationException($"The layout '{layout.Name}' did not render the body: write @Body where the page goes.");
                }
            }

            string[] unrendered = [.. viewContext.UnrenderedSections];
            if (unrendered.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The following sections have been defined but have not been rendered by the layout: '{string.Join("', '", unrendered)}'. " +
                    "Add <HackRenderSection Name=\"...\" Required=\"false\" /> to the layout.");
            }

            return html;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks that <paramref name="model"/> suits the view, like ASP.NET MVC's "The model item passed into
    /// the ViewDataDictionary is of type X, but this ViewDataDictionary instance requires a model item of type Y".
    /// </summary>
    /// <param name="viewType">The view component.</param>
    /// <param name="model">The model.</param>
    /// <exception cref="InvalidOperationException">The view derives from <c>HackView&lt;TModel&gt;</c> with another model type.</exception>
    public static void EnsureModelType(Type viewType, object? model)
    {
        ArgumentNullException.ThrowIfNull(viewType);
        for (Type? type = viewType; type is not null; type = type.BaseType)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(HackView<>))
            {
                continue;
            }

            Type expected = type.GetGenericArguments()[0];
            bool suits = model is null
                ? !expected.IsValueType || Nullable.GetUnderlyingType(expected) is not null
                : expected.IsInstanceOfType(model);
            if (!suits)
            {
                throw new InvalidOperationException(
                    $"The model passed to the view '{viewType.Name}' is {(model is null ? "null" : $"of type '{model.GetType().FullName}'")}, " +
                    $"but this view requires a model of type '{expected.FullName}'.");
            }

            return;
        }
    }

    [return: DynamicallyAccessedMembers(HackViewTypes.Component)]
    [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "A layout is a component the Razor compiler named in [Layout(typeof(...))] of the same assembly; Blazor's own LayoutView renders it the same way.")]
    private static Type? LayoutOf(Type component) => component.GetCustomAttribute<LayoutAttribute>()?.LayoutType;

    private static async Task<string> RenderPassAsync(
        HtmlRenderer renderer,
        [DynamicallyAccessedMembers(HackViewTypes.Component)] Type component,
        HackViewContext viewContext,
        RenderFragment? body)
    {
        ParameterView parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(HackViewHost.Pass)] = new HackViewPass(component, viewContext, body)
        });
        HtmlRootComponent root = await renderer.RenderComponentAsync<HackViewHost>(parameters).ConfigureAwait(false);
        return root.ToHtmlString();
    }
}

/// <summary>One rendering pass: the component to render, the page context and the layout's body.</summary>
/// <remarks>Carried as one object so the component type keeps its trimming annotation (a parameter set by reflection could not).</remarks>
internal sealed class HackViewPass(
    [DynamicallyAccessedMembers(HackViewTypes.Component)] Type component,
    HackViewContext viewContext,
    RenderFragment? body)
{
    [DynamicallyAccessedMembers(HackViewTypes.Component)]
    public Type Component { get; } = component;

    public HackViewContext ViewContext { get; } = viewContext;

    public RenderFragment? Body { get; } = body;
}

/// <summary>
/// The root of one rendering pass: gives the page's <see cref="HackViewContext"/> to every component
/// as a fixed cascading value, then renders the view (or a layout, with its <c>Body</c>).
/// </summary>
internal sealed class HackViewHost : IComponent
{
    private RenderHandle _handle;

    [Parameter]
    public HackViewPass Pass { get; set; } = default!;

    public void Attach(RenderHandle renderHandle) => _handle = renderHandle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        _handle.Render(BuildRenderTree);
        return Task.CompletedTask;
    }

    private void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<HackViewContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<HackViewContext>.Value), Pass.ViewContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<HackViewContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<HackViewContext>.ChildContent), (RenderFragment)RenderComponent);
        builder.CloseComponent();
    }

    private void RenderComponent(RenderTreeBuilder builder)
    {
        builder.OpenComponent(0, Pass.Component);
        if (Pass.Body is not null)
        {
            builder.AddComponentParameter(1, nameof(HackLayout.Body), Pass.Body);
        }

        builder.CloseComponent();
    }
}
