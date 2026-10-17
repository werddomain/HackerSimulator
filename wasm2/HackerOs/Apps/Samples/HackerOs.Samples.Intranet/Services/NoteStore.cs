using HackerOs.Samples.Intranet.Models;

namespace HackerOs.Samples.Intranet.Services;

/// <summary>
/// The notes of the site, kept in the memory of the site process (a singleton of its DI container).
/// </summary>
/// <remarks>
/// A site has no <c>filesystem.*</c> capability (ADR 0073 §3): its data never lands in the OS files.
/// The store lives as long as the site process, like an in-memory database of a real web server; the
/// OS stops an idle site and the notes come back to their seed on the next start. Every method copies
/// the notes in and out, so a caller never edits the stored instance behind the lock.
/// </remarks>
public sealed class NoteStore
{
    private readonly Lock _gate = new();
    private readonly List<Note> _notes = [];
    private int _nextId = 1;

    /// <summary>Creates the store with the notes every new site process starts with.</summary>
    /// <param name="time">The clock of the site (the tests pass a fixed one).</param>
    public NoteStore(TimeProvider time)
    {
        Time = time;
        Add(new Note { Title = "Welcome to the intranet", Body = "Notes are kept by the site process: they reset when the OS stops the site.", Pinned = true, Author = "it-ops" });
        Add(new Note { Title = "VPN maintenance", Body = "The gateway at 10.0.0.1 reboots Friday at 22:00.", Author = "it-ops" });
        Add(new Note { Title = "Coffee machine", Body = "Second floor machine fixed. Do not feed it tea bags.", Author = "facilities" });
    }

    private TimeProvider Time { get; }

    /// <summary>Returns every note, pinned ones first, then the most recent.</summary>
    public IReadOnlyList<Note> All()
    {
        lock (_gate)
        {
            return [.. _notes.OrderByDescending(note => note.Pinned).ThenByDescending(note => note.UpdatedAt).ThenByDescending(note => note.Id).Select(Copy)];
        }
    }

    /// <summary>Returns the number of notes.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _notes.Count;
            }
        }
    }

    /// <summary>Returns a copy of the note <paramref name="id"/>, or <see langword="null"/>.</summary>
    /// <param name="id">The identifier.</param>
    public Note? Find(int id)
    {
        lock (_gate)
        {
            return _notes.Find(note => note.Id == id) is Note note ? Copy(note) : null;
        }
    }

    /// <summary>Stores a new note and returns it with its identifier and date.</summary>
    /// <param name="note">The note to add; its <see cref="Note.Id"/> is ignored.</param>
    public Note Add(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        lock (_gate)
        {
            Note stored = Copy(note);
            stored.Id = _nextId++;
            stored.UpdatedAt = Time.GetUtcNow();
            _notes.Add(stored);
            return Copy(stored);
        }
    }

    /// <summary>Replaces the note of the same identifier; returns <see langword="null"/> when there is none.</summary>
    /// <param name="note">The new content.</param>
    public Note? Update(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);
        lock (_gate)
        {
            int index = _notes.FindIndex(existing => existing.Id == note.Id);
            if (index < 0)
            {
                return null;
            }

            Note stored = Copy(note);
            stored.UpdatedAt = Time.GetUtcNow();
            _notes[index] = stored;
            return Copy(stored);
        }
    }

    /// <summary>Removes the note <paramref name="id"/>; returns whether it existed.</summary>
    /// <param name="id">The identifier.</param>
    public bool Remove(int id)
    {
        lock (_gate)
        {
            return _notes.RemoveAll(note => note.Id == id) > 0;
        }
    }

    private static Note Copy(Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Body = note.Body,
        Pinned = note.Pinned,
        Author = note.Author,
        UpdatedAt = note.UpdatedAt,
    };
}
