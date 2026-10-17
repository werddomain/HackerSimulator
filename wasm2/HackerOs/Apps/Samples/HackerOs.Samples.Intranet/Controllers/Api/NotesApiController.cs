using HackerOs.AppSdk.Web.Mvc;
using HackerOs.Samples.Intranet.Models;
using HackerOs.Samples.Intranet.Services;

namespace HackerOs.Samples.Intranet.Controllers.Api;

/// <summary>
/// The REST API of the notes: every HTTP verb of a resource, as an ASP.NET Core <c>[ApiController]</c>.
/// JSON in and out, <c>400</c> with a problem document when the body breaks the <see cref="Note"/>
/// rules, <c>404</c> problem for an unknown identifier. The <c>/Notes</c> pages call the same store;
/// the script of the notes list (<c>wwwroot/js/notes.js</c>) calls <c>PATCH</c> to pin a note.
/// </summary>
[Route("api/notes")]
public sealed class NotesApiController(NoteStore store) : HackApiController
{
    /// <summary><c>GET /api/notes?q=</c>: every note, or those whose title or text contains <paramref name="q"/>.</summary>
    /// <param name="q">The search text.</param>
    [HttpGet]
    public IReadOnlyList<Note> List([FromQuery] string? q = null) =>
        string.IsNullOrWhiteSpace(q)
            ? store.All()
            : [.. store.All().Where(note => note.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || note.Body.Contains(q, StringComparison.OrdinalIgnoreCase))];

    /// <summary><c>GET /api/notes/{id}</c>.</summary>
    /// <param name="id">The identifier.</param>
    [HttpGet("{id:int}", Name = "GetNote")]
    public IHackActionResult Get(int id) => store.Find(id) is Note note ? Ok(note) : NotFound();

    /// <summary><c>POST /api/notes</c>: <c>201 Created</c> with the <c>Location</c> of the new note.</summary>
    /// <param name="note">The note (JSON body); the identifier is given by the store.</param>
    [HttpPost]
    public IHackActionResult Create(Note note)
    {
        if (string.IsNullOrWhiteSpace(note.Author))
        {
            note.Author = IntranetSession.DisplayName(HttpContext) ?? IntranetSession.Anonymous;
        }

        Note created = store.Add(note);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary><c>PUT /api/notes/{id}</c>: replaces the whole note.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="note">The new content (JSON body).</param>
    [HttpPut("{id:int}")]
    public IHackActionResult Replace(int id, Note note)
    {
        note.Id = id;
        return store.Update(note) is Note saved ? Ok(saved) : NotFound();
    }

    /// <summary><c>PATCH /api/notes/{id}</c>: changes only the fields given, then validates the result.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="patch">The fields to change (JSON body).</param>
    [HttpPatch("{id:int}")]
    public IHackActionResult Patch(int id, NotePatch patch)
    {
        if (store.Find(id) is not Note note)
        {
            return NotFound();
        }

        note.Title = patch.Title ?? note.Title;
        note.Body = patch.Body ?? note.Body;
        note.Pinned = patch.Pinned ?? note.Pinned;

        // The patched note must still follow the rules a full body is checked against.
        if (!TryValidateModel(note))
        {
            return ValidationProblem();
        }

        return Ok(store.Update(note));
    }

    /// <summary><c>DELETE /api/notes/{id}</c>: <c>204 No Content</c>, or <c>404</c>.</summary>
    /// <param name="id">The identifier.</param>
    [HttpDelete("{id:int}")]
    public IHackActionResult Delete(int id) => store.Remove(id) ? NoContent() : NotFound();
}
