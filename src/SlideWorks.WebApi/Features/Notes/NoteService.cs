using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Notes;

[RegisterPerRequest(Concrete = true)]
public class NoteService(AppDbContext db, TimeProvider clock)
{
    public const int MAX_TEXT_LENGTH = 500;

    // Newest first. By id rather than CreatedAt: SQLite cannot ORDER BY a
    // DateTimeOffset column, and ids are handed out in insert order anyway.
    public async Task<IReadOnlyList<NoteViewModel>> ListAsync() =>
        (await db.Notes.OrderByDescending(n => n.Id).ToListAsync())
        .Select(NoteViewModel.FromEntity)
        .ToList();

    public async Task<NoteViewModel> GetAsync(int id) => NoteViewModel.FromEntity(await FindAsync(id));

    public async Task<NoteViewModel> CreateAsync(CreateNoteRequest request)
    {
        var note = new Note { Text = request.Text.Trim(), CreatedAt = clock.GetUtcNow() };
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return NoteViewModel.FromEntity(note);
    }

    public async Task DeleteAsync(int id)
    {
        db.Notes.Remove(await FindAsync(id));
        await db.SaveChangesAsync();
    }

    private async Task<Note> FindAsync(int id) =>
        await db.Notes.FindAsync(id) ?? throw new NotFoundException($"Note {id} not found.");
}
