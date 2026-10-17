using HackerOs.AppSdk.Web.Antiforgery;
using HackerOs.AppSdk.Web.Mvc;
using HackerOs.Samples.Intranet.Models;
using HackerOs.Samples.Intranet.Services;

namespace HackerOs.Samples.Intranet.Controllers;

/// <summary>
/// The pages of the notes wiki: a classic MVC CRUD over HTML forms with anti-forgery tokens,
/// validation messages and post/redirect/get. Its views are <c>.razor</c> components
/// (<c>Views/Notes/</c>, ADR 0083) rendered to static HTML.
/// </summary>
public sealed class NotesController(NoteStore store) : HackController
{
    /// <summary><c>GET /Notes</c>: the list.</summary>
    public IHackActionResult Index() => View(store.All());

    /// <summary><c>GET /Notes/Details/{id}</c>.</summary>
    /// <param name="id">The identifier.</param>
    public IHackActionResult Details(int id) => store.Find(id) is Note note ? View(note) : NotFound();

    /// <summary><c>GET /Notes/Create</c>: the empty form (the <c>Edit</c> view).</summary>
    [HttpGet]
    public IHackActionResult Create() => View("Edit", new Note());

    /// <summary><c>POST /Notes/Create</c>.</summary>
    /// <param name="note">The posted form.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IHackActionResult Create(Note note)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", note);
        }

        note.Author = IntranetSession.DisplayName(HttpContext) ?? IntranetSession.Anonymous;
        Note created = store.Add(note);
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    /// <summary><c>GET /Notes/Edit/{id}</c>.</summary>
    /// <param name="id">The identifier.</param>
    [HttpGet]
    public IHackActionResult Edit(int id) => store.Find(id) is Note note ? View(note) : NotFound();

    /// <summary><c>POST /Notes/Edit/{id}</c>: the form shown again with its errors, or the saved note.</summary>
    /// <param name="id">The identifier (route).</param>
    /// <param name="note">The posted form.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IHackActionResult Edit(int id, Note note)
    {
        note.Id = id;
        if (!ModelState.IsValid)
        {
            return View(note);
        }

        note.Author = IntranetSession.DisplayName(HttpContext) ?? IntranetSession.Anonymous;
        return store.Update(note) is null ? NotFound() : RedirectToAction(nameof(Details), new { id });
    }

    /// <summary><c>POST /Notes/Delete/{id}</c>.</summary>
    /// <param name="id">The identifier.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IHackActionResult Delete(int id)
    {
        store.Remove(id);
        return RedirectToAction(nameof(Index));
    }
}
