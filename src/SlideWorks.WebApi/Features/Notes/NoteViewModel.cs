using SlideWorks.Core;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Notes;

// What the browser gets: the date is already a display string, because the SPA
// never builds Date objects (.claude/skills/no-browser-datetime).
public record NoteViewModel(int Id, string Text, string Created) : IHasId
{
    public static NoteViewModel FromEntity(Note note) =>
        new(note.Id, note.Text, DisplayDate.Format(note.CreatedAt));
}
