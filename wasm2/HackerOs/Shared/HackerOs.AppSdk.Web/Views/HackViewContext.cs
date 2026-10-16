using System.Diagnostics.CodeAnalysis;
using HackerOs.AppSdk.Web.Antiforgery;
using HackerOs.AppSdk.Web.Http;
using HackerOs.AppSdk.Web.Mvc;
using HackerOs.AppSdk.Web.Routing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.AppSdk.Web.Views;

/// <summary>
/// Values a controller hands to its view and that the view hands to its layout (ASP.NET's
/// <c>ViewDataDictionary</c>): <c>ViewData["Title"] = "Notes"</c>. Keys compare without case.
/// </summary>
public sealed class HackViewDataDictionary : Dictionary<string, object?>
{
    /// <summary>Initializes an empty dictionary.</summary>
    public HackViewDataDictionary()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>Gets or sets the page title (<c>ViewData["Title"]</c>), which layouts usually put in <c>&lt;title&gt;</c>.</summary>
    public string? Title
    {
        get => TryGetValue("Title", out object? value) ? value?.ToString() : null;
        set => this["Title"] = value;
    }
}

/// <summary>
/// Everything a view, its layout and its helpers see of the request (ASP.NET's <c>ViewContext</c>,
/// ADR 0083 §3): the request, the action, the model, the model state, the view data, the URL helper
/// and the layout to apply. The view engine gives it to every component of the page as a cascading value.
/// </summary>
public sealed class HackViewContext
{
    private readonly HackViewEngine _engine;
    private readonly Dictionary<string, RenderFragment> _sections;
    private readonly HashSet<string> _renderedSections;
    private IHackUrlHelper? _url;

    internal HackViewContext(
        HackActionContext actionContext,
        HackViewEngine engine,
        object? model,
        HackViewDataDictionary viewData,
        bool isPartial)
    {
        ActionContext = actionContext;
        _engine = engine;
        Model = model;
        ViewData = viewData;
        IsPartial = isPartial;
        _sections = new Dictionary<string, RenderFragment>(StringComparer.OrdinalIgnoreCase);
        _renderedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private HackViewContext(HackViewContext parent, object? model)
    {
        ActionContext = parent.ActionContext;
        _engine = parent._engine;
        _url = parent._url;
        Model = model;
        ViewData = parent.ViewData;
        IsPartial = true;
        _sections = parent._sections;
        _renderedSections = parent._renderedSections;
    }

    /// <summary>Gets the action context (request, route values, action, model state).</summary>
    public HackActionContext ActionContext { get; }

    /// <summary>Gets the request.</summary>
    public HackHttpContext HttpContext => ActionContext.HttpContext;

    /// <summary>Gets the route values of the request.</summary>
    public HackRouteValueDictionary RouteValues => ActionContext.RouteValues;

    /// <summary>Gets the controller action, or <see langword="null"/> for a minimal route.</summary>
    public HackControllerActionDescriptor? ActionDescriptor => ActionContext.ActionDescriptor;

    /// <summary>Gets the binding and validation state (errors shown by <see cref="HackValidationMessage"/>).</summary>
    public HackModelStateDictionary ModelState => ActionContext.ModelState;

    /// <summary>Gets the model the action passed (<c>View(model)</c>), or that a partial received.</summary>
    public object? Model { get; }

    /// <summary>Gets the view data, shared by the view, its partials and its layouts.</summary>
    public HackViewDataDictionary ViewData { get; }

    /// <summary>Gets whether this is a partial view (no layout).</summary>
    public bool IsPartial { get; }

    /// <summary>Gets the URL helper.</summary>
    public IHackUrlHelper Url => _url ??= new HackUrlHelper(ActionContext);

    /// <summary>
    /// Gets or sets the layout applied once the view (or the current layout) rendered: the
    /// component's <c>@layout</c> by default; a view may change it or set <see langword="null"/>,
    /// like <c>Layout = null</c> in a <c>.cshtml</c>.
    /// </summary>
    [DynamicallyAccessedMembers(HackViewTypes.Component)]
    public Type? Layout { get; set; }

    /// <summary>Gets the anti-forgery tokens of the request (the cookie is set when first asked).</summary>
    public HackAntiforgeryTokenSet GetAntiforgeryTokens() =>
        HttpContext.RequestServices.GetRequiredService<HackAntiforgery>().GetAndStoreTokens(HttpContext);

    /// <summary>Finds a partial view by name, from the current controller's folder then <c>Shared/</c>.</summary>
    /// <param name="name">A name (<c>_NoteCard</c>) or a path (<c>~/Views/Notes/_NoteCard.razor</c>).</param>
    /// <returns>The view.</returns>
    /// <exception cref="InvalidOperationException">No such view; the message lists the searched locations.</exception>
    public HackViewDescriptor FindPartial(string name) => _engine.GetView(ActionContext, name);

    /// <summary>Creates the context of a partial view: same request and view data, another model.</summary>
    /// <param name="model">The partial's model.</param>
    public HackViewContext ForPartial(object? model) => new(this, model);

    internal void DefineSection(string name, RenderFragment content)
    {
        if (!_sections.TryAdd(name, content))
        {
            throw new InvalidOperationException($"The section '{name}' is already defined.");
        }
    }

    internal bool TryRenderSection(string name, [NotNullWhen(true)] out RenderFragment? content)
    {
        if (_sections.TryGetValue(name, out content))
        {
            _renderedSections.Add(name);
            return true;
        }

        return false;
    }

    internal bool IsSectionDefined(string name) => _sections.ContainsKey(name);

    internal IEnumerable<string> UnrenderedSections => _sections.Keys.Where(name => !_renderedSections.Contains(name));
}
